using GomokuGame.AI;
using GomokuGame.AI.Configuration;
using GomokuGame.Models;
using System.Diagnostics;
using static GomokuGame.AI.AiPlayer;
using static GomokuGame.Trainer.PathHelper;
using static GomokuGame.Trainer.TopologyPrinter;

namespace GomokuGame.Trainer
{
    /// <summary>
    /// Оркестратор обучения — выносит логику запуска и управления сессиями из Program.cs.
    /// </summary>
    internal static class TrainingOrchestrator
    {
        // ============ ОБУЧЕНИЕ (перенесено из Program.cs) ============

        private static readonly object SaveLock = new object();

        /// <summary>
        /// Диспетчер обучения — выбирает алгоритм и запускает соответствующий метод.
        /// </summary>
        public static void RunTraining(TrainingArguments arguments)
        {
            if (arguments.Algorithm.Equals("genetic", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException(
                    "Создание генетической популяции по топологии " +
                    "ещё не подключено.");
            }

            SingleNetworkFactory.ValidateTopology(arguments.Topology);

            PrintRequestedTopology(arguments.Topology);

            switch (arguments.Algorithm.ToLowerInvariant())
            {
                case "genetic":
                    RunGeneticTraining(arguments);
                    break;

                case "league":
                    RunLeagueTraining(arguments);
                    break;

                case "bot":
                    RunBotTraining(arguments);
                    break;

                default:
                    RunStandardTraining(arguments);
                    break;
            }
        }

        /// <summary>
        /// Запускает стандартное обучение (gradient descent).
        /// </summary>
        private static void RunStandardTraining(TrainingArguments arguments)
        {
            RunGradientTraining(arguments, TrainingExecutors.TrainStandardChunk);
        }

        /// <summary>
        /// Запускает градиентное обучение (gradient descent).
        /// </summary>
        public static void RunGradientTraining(
            TrainingArguments arguments,
            Func<AiPlayer, TrainingChunkOptions, TrainingStats> trainChunk)
        {
            GradientTrainingArguments options = arguments.Gradient;

            int boardSize = arguments.Game.BoardSize;
            int winLength = arguments.Game.WinLength;

            int games = options.Games;
            bool infinite = games <= 0;

            double lr = options.LearningRate;
            double epsilon = options.Epsilon;

            double decayTargetFraction = options.DecayTargetFraction;
            bool decayExplicit = options.DecayExplicit;
            double decay = options.Decay;

            string saveName = arguments.SaveName;
            bool showDemo = options.ShowDemo;
            string algorithm = arguments.Algorithm;
            int chunk = arguments.Chunk;
            
            if (chunk <= 0)
            {
                Console.WriteLine("Размер чанка должен быть больше нуля.");
                return;
            }

            int warmupGames = options.League.WarmupGames;
            int snapshotEvery = options.League.SnapshotEvery;

            bool leagueMode = algorithm.Equals(
                "league",
                StringComparison.OrdinalIgnoreCase);

            string savePath = GetSavePath(saveName);
            bool continuing = File.Exists(savePath);

            if (!decayExplicit && !infinite && games > 0)
            {
                const double epsilonFloor = 0.1;
                long targetGames = Math.Max(1, (long)(games * decayTargetFraction));
                decay = Math.Pow(epsilonFloor / epsilon, 1.0 / targetGames);
            }

            var ai = continuing
                        ? AiPlayer.LoadOrCreate(savePath, arguments.Seed, arguments.Topology)
                        : AiPlayer.CreateFresh(arguments.Seed, arguments.Topology);

            const int SnapshotInterval = 25_000;
            const int PlateauWindowChunks = 20;
            const double PlateauThreshold = 0.001;

            bool plateauNoted = false;

            long gamesDoneBeforeThisSession = ai.TotalGamesPlayed;
            long totalXWins = ai.TotalXWins;
            long totalOWins = ai.TotalOWins;
            long totalDraws = ai.TotalDraws;
            long totalMovies = ai.TotalMoves;
            long totalMissedWins = ai.TotalMissedWins;
            long lastSnapshotAt = gamesDoneBeforeThisSession;

            int sessionXWins = 0, sessionOWins = 0, sessionDraws = 0, sessionTotalMovies = 0, sessionMissedWins = 0;

            Console.Title = $"Gomoku Trainer — {saveName} ({algorithm}) — {boardSize}x{boardSize} win{winLength}";

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n=== Ctrl+C — сохраняем текущее состояние и выходим ===");
                lock (SaveLock)
                {
                    try
                    {
                        ai.Save();
                        Console.WriteLine($"Saved to: {ai.NetworkPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ОШИБКА при сохранении: {ex.Message}");
                        Console.WriteLine("Внимание: последнее состояние НЕ сохранено.");
                    }
                }
                Program.LogShutdown();
                Environment.Exit(0);
            };

            ai.LearningRate = lr;
            ai.Epsilon = continuing ? 0.1 : epsilon;
            ai.EpsilonMin = 0.1;
            ai.EpsilonDecay = decay;
            ai.NetworkPath = savePath;

            if (continuing)
                Console.WriteLine($"Продолжаем обучение с Epsilon = {ai.Epsilon} (сеть загружена из {savePath})");
            else
                Console.WriteLine($"Новая сеть, старт с Epsilon = {epsilon} (min 0.1, decay {decay})");

            if (gamesDoneBeforeThisSession > 0)
            {
                Console.WriteLine($"Восстановлена статистика: {gamesDoneBeforeThisSession:N0} партий сыграно ранее " +
                    $"(X {totalXWins:N0} / O {totalOWins:N0} / Draw {totalDraws:N0}, missed wins {totalMissedWins:N0})");
            }

            ai.Mode = arguments.Mode;
            ai.Correction = options.Correction;

            Console.WriteLine("=== GomokuGame Trainer / TRAIN ===");
            Console.WriteLine($"Board:   {boardSize}x{boardSize}");
            Console.WriteLine($"Win len: {winLength}");
            Console.WriteLine($"Games:   {(infinite ? "∞ (стоп — закрыть окно / Ctrl+C)" : games.ToString())}");
            Console.WriteLine($"LR:      {lr}");
            Console.WriteLine($"Epsilon: {ai.Epsilon} (min 0.1, decay {decay})");
            Console.WriteLine($"Mode:    {ai.Mode}");
            Console.WriteLine($"Correction: {ai.Correction}");
            Console.WriteLine($"Chunk:    {chunk}");
            Console.WriteLine($"Algorithm: {algorithm}");
            Console.WriteLine(arguments.Seed == 0
                        ? "Seed:     случайный"
                        : $"Seed:     {arguments.Seed}");
            if (leagueMode)
            {
                Console.WriteLine($"WarmUp:   {warmupGames}");
                Console.WriteLine($"Snapshot: {snapshotEvery}");
            }
            Console.WriteLine($"Save as: {saveName}.json");
            Console.WriteLine();

            double lastLoss = ai.LastAverageLoss, avg_movies = 0;

            var sw = Stopwatch.StartNew();
            long sessionGames = 0;

            long lastCriticalBufferSize = ai.CriticalBuffer.BufferSize;
            long lastCriticalSamplesLearnedCount = ai.CriticalBuffer.LearnedSamplesCount;

            Console.WriteLine($"{"Done",12} | {"X",10} {"O",10} {"Draw",8} | {"Loss",7} | {"Eps",5} | {"Missed",10} | {"Time",8} | {"PerParty time",7} | {"PerParty movies",7}");
            Console.WriteLine(new string('-', 130));

            var chunkOptions = new TrainingChunkOptions(
                algorithm: algorithm,
                game: arguments.Game,
                games: chunk,
                league: options.League);

            int b = 0;
            
            while (infinite || sessionGames < games)
            {
                int currentChunkSize = infinite ? chunk : (int)Math.Min((long)chunk, games - sessionGames);
                chunkOptions.Games = currentChunkSize;

                TimeSpan before = sw.Elapsed;

                TrainingStats stats = trainChunk(ai, chunkOptions);

                long playedInChunk = (long)stats.XWins + stats.OWins + stats.Draws;

                if (playedInChunk <= 0)
                {
                    Console.WriteLine("Чанк не вернул ни одной завершённой партии. Обучение остановлено.");
                    break;
                }

                sessionGames += playedInChunk;

                TimeSpan after = sw.Elapsed;

                totalMovies += stats.totalMoves;
                totalMissedWins += stats.MissedWins;
                totalXWins += stats.XWins;
                totalOWins += stats.OWins;
                totalDraws += stats.Draws;

                sessionTotalMovies += stats.totalMoves;
                sessionMissedWins += stats.MissedWins;
                sessionXWins += stats.XWins;
                sessionOWins += stats.OWins;
                sessionDraws += stats.Draws;

                avg_movies = stats.AverageMovies;
                lastLoss = stats.LastAverageLoss;

                // Дельта к последнему значению из истории (прошлый чанк),
                // а не ко второму с конца. На первом чанке истории нет — дельта 0.
                double previousLoss = ai.RecentLossHistory.Count > 0
                    ? ai.RecentLossHistory[^1]
                    : lastLoss;
                double deltaLoss = previousLoss - lastLoss;

                long done = gamesDoneBeforeThisSession + sessionGames;

                ai.TotalGamesPlayed = done;
                ai.TotalXWins = totalXWins;
                ai.TotalOWins = totalOWins;
                ai.TotalDraws = totalDraws;
                ai.TotalMissedWins = totalMissedWins;
                ai.TotalMoves = totalMovies;

                Console.WriteLine(
                    $"{done,12:N0} | {totalXWins,10:N0} {totalOWins,10:N0} {totalDraws,8:N0} | " +
                    $"{lastLoss,7:F4} ({deltaLoss,6:+#,##0;-#,##0;0}) | {ai.Epsilon,5:F3} | {totalMissedWins,10:N0} | " +
                    $"{sw.Elapsed.TotalSeconds,7:F1}s | { (after - before).TotalMilliseconds / playedInChunk,6:F1}ms | {avg_movies,6:F1}");

                long deltaCriticalBufferSize = ai.CriticalBuffer.BufferSize - lastCriticalBufferSize;
                long deltaCriticalSamplesLearnedCount = ai.CriticalBuffer.LearnedSamplesCount - lastCriticalSamplesLearnedCount;

                Console.WriteLine(
                    $"CriticalBufferSize {ai.CriticalBuffer.BufferSize,12:N0} {deltaCriticalBufferSize,10:+#,##0;-#,##0;0} | " +
                    $"CriticalSamplesLearnedCount {ai.CriticalBuffer.LearnedSamplesCount,10:N0} {deltaCriticalSamplesLearnedCount,10:+#,##0;-#,##0;0} | " +
                    $"AverageAttemptsToLearn {ai.CriticalBuffer.AverageAttemptsToLearn,10:F2}");

                var patternValues = PatternProbe.EvaluateAll(ai, boardSize, winLength);
                PatternProbe.LogToConsole(b + 1, done, patternValues);
                PatternProbe.AppendCsv(saveName, gamesDoneBeforeThisSession, done, stats, lastLoss, ai.Epsilon, sw.Elapsed.TotalSeconds, (after - before).TotalMilliseconds / playedInChunk, avg_movies, ai.CriticalBuffer.BufferSize, ai.CriticalBuffer.LearnedSamplesCount, ai.CriticalBuffer.AverageAttemptsToLearn, patternValues);

                if (showDemo)
                {
                    var demoO = ai.Clone(seed: 111 + b);
                    demoO.Epsilon = 0.1;
                    var (board, winner, demMoves, winLine) = GameSession.PlayDemoGame(ai, demoO, boardSize, winLength);
                    Console.WriteLine();
                    Console.WriteLine($"=== Demo game (chunk {b + 1}, after {done:N0} games) ===");
                    Console.WriteLine(board.ToAsciiString(winLine));
                    Console.WriteLine($"Winner: {(winner == CellState.Empty ? "Draw" : winner.ToString())}, moves: {demMoves}");
                    Console.WriteLine();
                }

                if (done - lastSnapshotAt >= SnapshotInterval)
                {
                    SaveSnapshotAtomic(ai, saveName, done);
                    lastSnapshotAt = done;
                }

                ai.AppendLossHistory(lastLoss);
                if (ai.RecentLossHistory.Count >= PlateauWindowChunks * 2)
                {
                    var history = ai.RecentLossHistory;
                    var recentWindow = history.Skip(history.Count - PlateauWindowChunks).Take(PlateauWindowChunks);
                    var previousWindow = history.Skip(history.Count - PlateauWindowChunks * 2).Take(PlateauWindowChunks);
                    double recentAvg = recentWindow.Average();
                    double previousAvg = previousWindow.Average();

                    if (Math.Abs(recentAvg - previousAvg) < PlateauThreshold)
                    {
                        if (!plateauNoted)
                        {
                            Console.WriteLine($"  [Возможное плато loss: {recentAvg:F4} держится ~{(PlateauWindowChunks * chunk):N0} партий. Решение останавливаться — твоё.]");
                            plateauNoted = true;
                        }
                    }
                    else
                    {
                        plateauNoted = false;
                    }
                }
                
                lock (SaveLock)
                {
                    string checkpointPath = GetSavePath(saveName);
                    ai.NetworkPath = checkpointPath;
                    ai.Save();
                }

                b++;
                lastCriticalBufferSize = ai.CriticalBuffer.BufferSize;
                lastCriticalSamplesLearnedCount = ai.CriticalBuffer.LearnedSamplesCount;
            }
            sw.Stop();

            if (!infinite)
            {
                double denominator = Math.Max(1L, sessionGames);

                Console.WriteLine();
                Console.WriteLine("=== FINAL (эта сессия) ===");
                Console.WriteLine($"Games:        {sessionGames:N0}");
                Console.WriteLine($"X wins:       {sessionXWins,6:N0} ({sessionXWins / denominator:P1})");
                Console.WriteLine($"O wins:       {sessionOWins,6:N0} ({sessionOWins / denominator:P1})");
                Console.WriteLine($"Draws:        {sessionDraws,6:N0} ({sessionDraws / denominator:P1})");
                Console.WriteLine($"Missed wins:  {sessionMissedWins,6:N0} (avg {sessionMissedWins / denominator:F2} за партию)");
                Console.WriteLine($"Movies:       {sessionTotalMovies,6:N0} ({sessionTotalMovies / denominator:F1})");
                Console.WriteLine($"Last loss:    {lastLoss:F4}");
                Console.WriteLine($"Time:         {sw.Elapsed.TotalSeconds:F1}s");

                Console.WriteLine();
                Console.WriteLine($"Всего партий с начала обучения этой сети: {ai.TotalGamesPlayed:N0}");
            }

            lock (SaveLock)
            {
                ai.Save();
            }
            Console.WriteLine($"Saved to:  {savePath}");
        }

        public static void RunLeagueTraining(TrainingArguments arguments)
        {
            RunGradientTraining(arguments, TrainingExecutors.TrainLeagueChunk);
        }

        public static void RunBotTraining(TrainingArguments arguments)
        {
            RunGradientTraining(arguments, TrainingExecutors.TrainBotChunk);
        }

        public static void RunGeneticTraining(TrainingArguments arguments)
        {
            GeneticTrainingArguments options = arguments.Genetic;

            int boardSize = arguments.Game.BoardSize;
            int winLength = arguments.Game.WinLength;
            int chunk = arguments.Chunk;
            
            if (chunk <= 0)
            {
                Console.WriteLine("Размер чанка должен быть больше нуля.");
                return;
            }

            string saveName = arguments.SaveName;
            LearningMode trainingMode = arguments.Mode;

            int population = options.Population;
            int eliteCount = options.EliteCount;
            double mutationSigma = options.MutationSigma;
            int gamesPerPair = options.GamesPerPair;
            int maxCycles = options.MaxCycles;
            bool infiniteCycles = maxCycles <= 0;

            Console.WriteLine("=== GomokuGame Trainer / TRAIN (genetic) ===");
            Console.WriteLine($"Board:         {boardSize}x{boardSize}");
            Console.WriteLine($"Win len:       {winLength}");
            Console.WriteLine($"Chunk games:   {chunk}");
            Console.WriteLine($"Population:    {population}");
            Console.WriteLine($"Elite count:   {eliteCount}");
            Console.WriteLine($"Mutation sigma:{mutationSigma}");
            Console.WriteLine($"Training mode: {trainingMode}");
            Console.WriteLine($"Games/pair:    {gamesPerPair}");
            Console.WriteLine($"Cycles:        {(infiniteCycles ? "∞ (стоп — Ctrl+C)" : maxCycles.ToString())}");
            Console.WriteLine($"Save as:       {saveName}.json");
            Console.WriteLine();

            string? existingPath = File.Exists(GetSavePath(saveName)) ? GetSavePath(saveName) : null;

            var trainer = new GeneticTrainer(population, eliteCount, mutationSigma,
                gamesPerPair, boardSize, winLength, chunk, trainingMode, saveName);

            trainer.InitializePopulation(existingPath);

            int cycle = 0;
            while (infiniteCycles || cycle < maxCycles)
            {
                var stop_watch = Stopwatch.StartNew();
                trainer.RunChunkCycle();
                stop_watch.Stop();

                var best = trainer.GetBest();
                
                Console.WriteLine(
                    $"Cycle {cycle,6:N0} | best score {best.Score,6:F1} | " +
                    $"W/D/L {best.Wins}/{best.Draws}/{best.Losses} | time {stop_watch.Elapsed.TotalSeconds:F1}s");

                cycle++;
            }

            if (!infiniteCycles)
                Console.WriteLine($"=== Готово: {maxCycles} циклов эволюции пройдено ===");
        }

        /// <summary>
        /// Сохраняет чекпоинт атомарно в отдельный файл {saveName}_ep{done}.json:
        /// модель пишется во временный файл, затем он атомарно перемещается на место снимка.
        /// Основной файл модели при этом не перезаписывается, ai.NetworkPath восстанавливается.
        /// Ошибка снимка не прерывает обучение.
        /// </summary>
        private static void SaveSnapshotAtomic(AiPlayer ai, string saveName, long done)
        {
            string finalPath = GetSavePath($"{saveName}_ep{done}");
            string tempPath = finalPath + ".tmp";

            lock (SaveLock)
            {
                string? originalPath = ai.NetworkPath;
                try
                {
                    ai.NetworkPath = tempPath;
                    ai.Save();

                    File.Move(tempPath, finalPath, overwrite: true);
                    Console.WriteLine($"  Checkpoint snapshot saved: {finalPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ОШИБКА снимка на {done:N0}: {ex.Message}");
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
                finally
                {
                    ai.NetworkPath = originalPath;
                }
            }
        }
    }
}
