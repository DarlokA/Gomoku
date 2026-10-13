using GomokuGame.AI;
using GomokuGame.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static GomokuGame.AI.AiPlayer;

namespace GomokuGame.Trainer
{
    public static class Program
    {
        private static readonly object SaveLock = new object();

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Использование: GomokuGame.Trainer.exe <train|match|matchlog> ключ:значение ...");
                return;
            }

            string command = args[0];
            var tokens = Tokenize(args.Skip(1).ToArray());
            var a = ParseKeyValueArgs(tokens);

            switch (command.ToLowerInvariant())
            {
                case "train":
                    RunTraining(a);
                    break;
                case "match":
                    RunMatch(a);
                    break;
                case "matchlog":
                    RunMatchLog(a);
                    break;
                default:
                    Console.WriteLine($"!! Неизвестная команда: \"{command}\". Ожидалась train, match или matchlog.");
                    break;
            }
        }

        // ============ ARG PARSING (команда + ключ:значение, с поддержкой кавычек) ============

        private static List<string> Tokenize(string[] args)
        {
            string joined = string.Join(" ", args);
            var tokens = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            bool tokenStarted = false;

            for (int i = 0; i < joined.Length; i++)
            {
                char c = joined[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    tokenStarted = true;
                    continue;
                }

                if (c == ' ' && !inQuotes)
                {
                    if (tokenStarted)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        tokenStarted = false;
                    }
                    continue;
                }

                current.Append(c);
                tokenStarted = true;
            }

            if (tokenStarted)
                tokens.Add(current.ToString());

            if (inQuotes)
                Console.WriteLine("!! Предупреждение: незакрытая кавычка в аргументах командной строки.");

            return tokens;
        }

        private static Dictionary<string, string> ParseKeyValueArgs(List<string> tokens)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in tokens)
            {
                int sep = token.IndexOf(':');
                if (sep <= 0)
                {
                    Console.WriteLine($"!! Аргумент без ключа проигнорирован: \"{token}\" (ожидался формат ключ:значение)");
                    continue;
                }
                string key = token.Substring(0, sep);
                string value = token.Substring(sep + 1);
                result[key] = value;
            }
            return result;
        }

        private static int GetArg(Dictionary<string, string> a, string key, int def)
            => a.TryGetValue(key, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v : def;

        private static double GetArgDouble(Dictionary<string, string> a, string key, double def)
            => a.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : def;

        private static string GetArgString(Dictionary<string, string> a, string key, string def)
            => a.TryGetValue(key, out var s) ? s : def;

        static void SaveSnapshotAtomic(AiPlayer ai, string saveName, long done)
        {
            string finalPath = GetSavePath($"{saveName}_ep{done}");
            string tempPath = finalPath + ".tmp";

            lock (SaveLock)
            {
                string? originalPath = ai?.NetworkPath ?? null;
                try
                {
                    if (ai != null)
                    {
                        ai.NetworkPath = tempPath;
                        ai.Save();
                    }
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
                    if (ai != null)
                        ai.NetworkPath = originalPath;
                }
            }
        }

        // ============ TRAIN ============

        private static void RunTraining(Dictionary<string, string> a)
        {
            int boardSize = GetArg(a, "board", 9);
            int winLength = GetArg(a, "win", 5);
            int games = GetArg(a, "games", 10_000);
            bool infinite = games <= 0;
            double lr = GetArgDouble(a, "lr", 0.001);
            double epsilon = GetArgDouble(a, "epsilon", 0.3);
            double decay = GetArgDouble(a, "decay", 0.9995);
            string modeStr = GetArgString(a, "mode", "C");
            string saveName = GetArgString(a, "save", "network");
            bool showDemo = GetArg(a, "demo", 1) == 1;
            string algorithm = GetArgString(a, "algorithm", "standard");
            int chunk = GetArg(a, "chunk", 500);
            int warmupGames = GetArg(a, "warmup", 500);
            int snapshotEvery = GetArg(a, "snapshotEvery", 100);
            string correctionModeStr = GetArgString(a, "correction", "Full");

            bool leagueMode = algorithm.Equals("league", StringComparison.OrdinalIgnoreCase);
            bool geneticMode = algorithm.Equals("genetic", StringComparison.OrdinalIgnoreCase);

            if (geneticMode)
            {
                int population = GetArg(a, "population", Environment.ProcessorCount);
                int eliteCount = GetArg(a, "eliteCount", Math.Max(1, population / 2));
                double mutationSigma = GetArgDouble(a, "mutationSigma", 0.05);
                int gamesPerPair = GetArg(a, "gamesPerPair", 100);

                Console.WriteLine("=== GomokuGame Trainer / TRAIN (genetic) ===");
                Console.WriteLine($"Board:         {boardSize}x{boardSize}");
                Console.WriteLine($"Win len:       {winLength}");
                Console.WriteLine($"Chunk games:   {chunk}");
                Console.WriteLine($"Population:    {population}");
                Console.WriteLine($"Elite count:   {eliteCount}");
                Console.WriteLine($"Mutation sigma:{mutationSigma}");
                Console.WriteLine($"Games/pair:    {gamesPerPair}");
                Console.WriteLine($"Save as:       {saveName}.json");
                Console.WriteLine();

                string? existingPath = File.Exists(GetSavePath(saveName)) ? GetSavePath(saveName) : null;

                var trainer = new GeneticTrainer(population, eliteCount, mutationSigma,
                    gamesPerPair, boardSize, winLength, chunk, saveName);

                trainer.InitializePopulation(existingPath);

                int cycle = 0;
                while (true)
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
                // выход — только Ctrl+C, как и в league/standard при infinite
            }

            string savePath = GetSavePath(saveName);
            bool continuing = File.Exists(savePath);

            var ai = continuing
                ? AiPlayer.LoadOrCreate(savePath)
                : AiPlayer.CreateFresh();

            const int SnapshotInterval = 25_000; // можешь сделать 10_000, если диска не жалко
            const int PlateauWindowChunks = 20;  // 20 чанков * 500 партий = 10 000 партий на окно
            const double PlateauThreshold = 0.001;

            bool plateauNoted = false;

            // ----- Восстановление накопленной статистики при продолжении обучения -----
            // У свежей сети (CreateFresh) эти поля по умолчанию равны 0 — ветвление не нужно,
            // можно читать напрямую из ai в обоих случаях.
            long gamesDoneBeforeThisSession = ai.TotalGamesPlayed;
            long totalXWins = ai.TotalXWins;
            long totalOWins = ai.TotalOWins;
            long totalDraws = ai.TotalDraws;
            long totalMovies = ai.TotalMoves;
            long totalMissedWins = ai.TotalMissedWins;
            long lastSnapshotAt = gamesDoneBeforeThisSession;

            // Статистика только ЭТОЙ сессии (для финальной сводки конечного прогона —
            // там отношения считаются к параметру games, а не к суммарному числу партий сети)
            int sessionXWins = 0, sessionOWins = 0, sessionDraws = 0, sessionTotalMovies = 0, sessionMissedWins = 0;

            Console.Title = $"Gomoku Trainer — {saveName} ({algorithm}) — {boardSize}x{boardSize} win{winLength}";

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // не даём процессу убиться мгновенно
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

            ai.Mode = modeStr.ToUpperInvariant() switch
            {
                "A" => LearningMode.LineLengthOnly,
                "B" => LearningMode.DiscountedOutcome,
                "C" => LearningMode.DiscountedWithShaping,
                "D" => LearningMode.AttackDefenseShaping,
                _ => LearningMode.DiscountedWithShaping
            };

            ai.Correction = correctionModeStr.ToUpperInvariant() switch
            {
                "FULL" => CorrectionMode.Full,
                "ONCE" => CorrectionMode.Once,
                "OFF" => CorrectionMode.Off,
                "ALL" => CorrectionMode.All,
                _ => CorrectionMode.Full
            };

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
            if (leagueMode)
            {
                Console.WriteLine($"WarmUp:   {warmupGames}");
                Console.WriteLine($"Snapshot: {snapshotEvery}");
            }
            Console.WriteLine($"Save as: {saveName}.json");
            Console.WriteLine();

            double lastLoss = ai.LastAverageLoss, avg_movies = 0;

            var sw = Stopwatch.StartNew();
            int numChunks = infinite ? 0 : games / chunk;

            Console.WriteLine($"{"Done",12} | {"X",10} {"O",10} {"Draw",8} | {"Loss",7} | {"Eps",5} | {"Missed",10} | {"Time",8} | {"PerParty time",7} | {"PerParty movies",7}");
            Console.WriteLine(new string('-', 130));

            int b = 0;
            while (infinite || b < numChunks)
            {
                TimeSpan before = sw.Elapsed;
                TrainingStats stats;
                if (algorithm.Equals("bot", StringComparison.OrdinalIgnoreCase))
                    stats = GameSession.TrainManyAgainstBot(ai, chunk, boardSize, winLength, chunkSize: chunk);
                else if (leagueMode)
                    stats = GameSession.TrainManyLeague(ai, chunk, boardSize, winLength, warmupGames, snapshotEvery, progressEvery: chunk);
                else
                    stats = GameSession.TrainMany(ai, chunk, boardSize, winLength, progressEvery: chunk);
                TimeSpan after = sw.Elapsed;

                int CriticalBufferSize = ai.CriticalBuffer.BufferSize;
                int CriticalSamplesLearnedCount = ai.CriticalBuffer.LearnedSamplesCount;
                double AverageAttemptsToLearn = ai.CriticalBuffer.AverageAttemptsToLearn;

                TimeSpan delta = (after - before) / chunk;

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


                long done = gamesDoneBeforeThisSession + (long)(b + 1) * chunk;

                // Синхронизируем накопленную статистику в самой сети — она уйдёт в файл
                // при следующем ai.Save() (периодический чекпоинт чуть ниже или снимок).
                ai.TotalGamesPlayed = done;
                ai.TotalXWins = totalXWins;
                ai.TotalOWins = totalOWins;
                ai.TotalDraws = totalDraws;
                ai.TotalMissedWins = totalMissedWins;
                ai.TotalMoves = totalMovies;

                Console.WriteLine(
                    $"{done,12:N0} | {totalXWins,10:N0} {totalOWins,10:N0} {totalDraws,8:N0} | " +
                    $"{lastLoss,7:F4} | {ai.Epsilon,5:F3} | {totalMissedWins,10:N0} | " +
                    $"{sw.Elapsed.TotalSeconds,7:F1}s | {delta.TotalMilliseconds,6:F1}ms | {avg_movies,6:F1}");
                Console.WriteLine(
                    $"CriticalBufferSize {CriticalBufferSize,12:N0} | CriticalSamplesLearnedCount {CriticalSamplesLearnedCount,10:N0} | AverageAttemptsToLearn {AverageAttemptsToLearn,10:N0}");

                // ===== Pattern probe — после каждого чанка =====
                var patternValues = PatternProbe.EvaluateAll(ai, boardSize, winLength);

                // Консоль — столбиком
                PatternProbe.LogToConsole(b + 1, done, patternValues);

                // CSV — с именем сети и стартовой партией
                PatternProbe.AppendCsv(
                    saveName,
                    gamesDoneBeforeThisSession,
                    done,
                    stats,
                    lastLoss,
                    ai.Epsilon,
                    sw.Elapsed.TotalSeconds,
                    delta.TotalMilliseconds,
                    avg_movies,
                    CriticalBufferSize,
                    CriticalSamplesLearnedCount,
                    AverageAttemptsToLearn,
                    patternValues);

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

                // Снимок раз в SnapshotInterval партий — по разнице от последнего снимка,
                // а не по остатку от деления: так интервал соблюдается при любом размере chunk
                // и при любом количестве партий, перенесённых из прошлой сессии.
                if (done - lastSnapshotAt >= SnapshotInterval)
                {
                    SaveSnapshotAtomic(ai, saveName, done);
                    lastSnapshotAt = done;
                }

                // Отслеживание плато по скользящему среднему loss (только информационная пометка, без автостопа).
                // История хранится внутри самой сети (ai.RecentLossHistory) и переживает перезапуск.
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
                        plateauNoted = false; // loss снова двигается — снимаем пометку
                    }
                }

                lock (SaveLock)
                {
                    string checkpointPath = GetSavePath(saveName);
                    ai.NetworkPath = checkpointPath;
                    ai.Save();
                }

                b++;
            }

            sw.Stop();

            if (!infinite)   // финальная сводка только для конечного прогона, по статистике ЭТОЙ сессии
            {
                Console.WriteLine();
                Console.WriteLine("=== FINAL (эта сессия) ===");
                Console.WriteLine($"Games:        {games:N0}");
                Console.WriteLine($"X wins:       {sessionXWins,6:N0} ({(double)sessionXWins / games:P1})");
                Console.WriteLine($"O wins:       {sessionOWins,6:N0} ({(double)sessionOWins / games:P1})");
                Console.WriteLine($"Draws:        {sessionDraws,6:N0} ({(double)sessionDraws / games:P1})");
                Console.WriteLine($"Missed wins:  {sessionMissedWins,6:N0} (avg {(double)sessionMissedWins / games:F2} за партию)");
                Console.WriteLine($"Movies:       {sessionTotalMovies,6:N0} ({(double)sessionTotalMovies / games:F1})");
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

        // ============ MATCH ============

        private static void RunMatch(Dictionary<string, string> a)
        {
            int boardSize = GetArg(a, "board", 9);
            int winLength = GetArg(a, "win", 5);
            int games = GetArg(a, "games", 1000);
            string fileX = GetArgString(a, "x", "network");
            string fileO = GetArgString(a, "o", "network");

            var aiX = LoadOrRandom(fileX, seed: 1);
            var aiO = LoadOrRandom(fileO, seed: 2);


            Console.WriteLine("=== GomokuGame Trainer / MATCH ===");
            Console.WriteLine($"Board:   {boardSize}x{boardSize}");
            Console.WriteLine($"Win len: {winLength}");
            Console.WriteLine($"Games:   {games}");
            Console.WriteLine($"X plays: {fileX}");
            Console.WriteLine($"O plays: {fileO}");
            Console.WriteLine();

            int threadCount = Environment.ProcessorCount;
            Console.WriteLine($"Threads: {threadCount}");

            var sw = Stopwatch.StartNew();
            var stats = GameSession.PlayMatchSeriesParallel(
                aiX, aiO, games, boardSize, winLength, threadCount);
            sw.Stop();

            Console.WriteLine("=== RESULT ===");
            Console.WriteLine($"X wins: {stats.XWins,5} ({stats.XWinRate:P1})");
            Console.WriteLine($"O wins: {stats.OWins,5} ({stats.OWinRate:P1})");
            Console.WriteLine($"Draws:  {stats.Draws,5} ({stats.DrawRate:P1})");
            Console.WriteLine($"Movies:  {stats.totalMoves,5} (Avg:{stats.AverageMovies:F2})");
            Console.WriteLine($"Time:   {sw.Elapsed.TotalSeconds:F1}s " +
                              $"({sw.Elapsed.TotalMilliseconds / games:F1}ms/game)");
        }

        private static void RunMatchLog(Dictionary<string, string> a)
        {
            int boardSize = GetArg(a, "board", 15);
            int winLength = GetArg(a, "win", 5);
            string fileX = GetArgString(a, "x", "network");
            string fileO = GetArgString(a, "o", "network");
            string? logFile = a.TryGetValue("log", out var lf) ? lf : null;
            double epsilon = GetArgDouble(a, "epsilon", 0.0);

            var aiX = LoadOrRandom(fileX, seed: 1);
            var aiO = LoadOrRandom(fileO, seed: 2);
            aiX.Epsilon = epsilon;
            aiO.Epsilon = epsilon;

            Console.WriteLine("=== GomokuGame Trainer / MATCHLOG ===");
            Console.WriteLine($"X plays: {fileX}, O plays: {fileO}, epsilon: {epsilon}");
            Console.WriteLine();

            System.IO.TextWriter writer = logFile != null
                ? new System.IO.StreamWriter(logFile, append: false)
                : Console.Out;

            var result = GameSession.PlayMatchWithLog(aiX, aiO, boardSize, winLength, writer);

            if (logFile != null)
            {
                writer.Flush();
                writer.Dispose();
                Console.WriteLine($"Log saved to: {logFile}");
            }
            Console.WriteLine($"Winner: {result.Winner}, moves: {result.Moves}");
        }

        private static AiPlayer LoadOrRandom(string name, int seed)
        {
            if (name.Equals("random", StringComparison.OrdinalIgnoreCase))
            {
                var ai = AiPlayer.CreateFresh(seed);
                ai.Epsilon = 1.0;
                ai.UseRotatingEvaluation = false;   // random — без TTA
                return ai;
            }

            string path = GetSavePath(name);
            if (!File.Exists(path))
            {
                Console.WriteLine($"!! File not found: {path}, using random");
                var ai = AiPlayer.CreateFresh(seed);
                ai.Epsilon = 1.0;
                ai.UseRotatingEvaluation = false;
                return ai;
            }

            var loaded = AiPlayer.LoadOrCreate(path, seed);
            loaded.Epsilon = 0.4;
            loaded.UseRotatingEvaluation = false;   // TTA при матчах
            return loaded;
        }

        // ============ HELPERS ============

        public static string GetSavePath(string name)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "GomokuGame", name + ".json");
        }
    }
}