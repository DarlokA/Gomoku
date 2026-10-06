using GomokuGame.AI;
using GomokuGame.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using static GomokuGame.AI.AiPlayer;

namespace GomokuGame.Trainer
{
    public static class Program
    {
        private static readonly object SaveLock = new object();

        public static void Main(string[] args)
        {
            // Первый аргумент — команда:
            //   train — обучение
            //   match — матч между двумя сетями
            if (args.Length > 0 && args[0].Equals("match", StringComparison.OrdinalIgnoreCase))
            {
                RunMatch(args);
                return;
            }else if (args.Length > 0 && args[0].Equals("matchlog", StringComparison.OrdinalIgnoreCase))
            {
                RunMatchLog(args);
                return;
            }

            RunTraining(args);
        }

        static void SaveSnapshotAtomic(AiPlayer ai, string saveName, long done)
        {
            string finalPath = GetSavePath($"{saveName}_ep{done}");
            string tempPath = finalPath + ".tmp";

            lock (SaveLock)
            {
                string? originalPath = ai?.NetworkPath ?? null;
                try
                {
                    ai.NetworkPath = tempPath;
                    ai.Save();
                    File.Move(tempPath, finalPath, overwrite: true); // атомарная подмена
                    Console.WriteLine($"  Checkpoint snapshot saved: {finalPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ОШИБКА снимка на {done:N0}: {ex.Message}");
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { /* не критично */ }
                    }
                }
                finally
                {
                    ai.NetworkPath = originalPath;
                }
            }
        }

        // ============ TRAIN ============

        private static void RunTraining(string[] args)
        {
            // Параметры: [boardSize] [winLength] [games] [lr] [epsilon] [decay] [seed] [mode] [saveName]
            int boardSize = GetArg(args, 0, 9);
            int winLength = GetArg(args, 1, 5);
            int games = GetArg(args, 2, 10_000);
            bool infinite = games <= 0;   // games=0 значит "без ограничения"
            double lr = GetArgDouble(args, 3, 0.001);
            double epsilon = GetArgDouble(args, 4, 0.3);
            double decay = GetArgDouble(args, 5, 0.9995);
            int seed = GetArg(args, 6, 42);
            string modeStr = args.Length > 7 ? args[7] : "C";
            string saveName = args.Length > 8 ? args[8] : "network";
            bool showDemo = GetArg(args, 9, 1) == 1;
            string algorithm = args.Length > 10 ? args[10] : "standard";
            int chunk = GetArg(args, 11, 500);
            int warmupGames = GetArg(args, 12, 500);
            int snapshotEvery = GetArg(args, 13, 100);
            string correctionModeStr = args.Length > 14 ? args[14] : "Full";

            bool leagueMode = algorithm.Equals("league", StringComparison.OrdinalIgnoreCase);

            GameSession.SetSeed(seed);   // установить seed для аугментации

            string savePath = GetSavePath(saveName);
            bool continuing = File.Exists(savePath);

            var ai = continuing
                ? AiPlayer.LoadOrCreate(savePath, seed)
                : AiPlayer.CreateFresh(seed);

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
                _ => CorrectionMode.Full
            };

            Console.WriteLine("=== GomokuGame Trainer / TRAIN ===");
            Console.WriteLine($"Board:   {boardSize}x{boardSize}");
            Console.WriteLine($"Win len: {winLength}");
            Console.WriteLine($"Games:   {(infinite ? "∞ (стоп — закрыть окно / Ctrl+C)" : games.ToString())}");
            Console.WriteLine($"LR:      {lr}");
            Console.WriteLine($"Epsilon: {ai.Epsilon} (min 0.1, decay {decay})");
            Console.WriteLine($"Seed:    {seed}");
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

        private static void RunMatch(string[] args)
        {
            // Параметры: match [boardSize] [winLength] [games] [fileA] [fileB]
            // fileA играет за X, fileB — за O.
            // Если fileA == "random" — случайная сеть (без обучения).
            int boardSize = GetArg(args, 1, 9);
            int winLength = GetArg(args, 2, 5);
            int games = GetArg(args, 3, 1000);
            string fileA = args.Length > 4 ? args[4] : "network";
            string fileB = args.Length > 5 ? args[5] : "network";

            var aiX = LoadOrRandom(fileA, seed: 1);
            var aiO = LoadOrRandom(fileB, seed: 2);


            Console.WriteLine("=== GomokuGame Trainer / MATCH ===");
            Console.WriteLine($"Board:   {boardSize}x{boardSize}");
            Console.WriteLine($"Win len: {winLength}");
            Console.WriteLine($"Games:   {games}");
            Console.WriteLine($"X plays: {fileA}");
            Console.WriteLine($"O plays: {fileB}");
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

        private static void RunMatchLog(string[] args)
        {
            // Параметры: matchlog [boardSize] [winLength] [fileA] [fileB] [logFile] [epsilon]
            int boardSize = GetArg(args, 1, 15);
            int winLength = GetArg(args, 2, 5);
            string fileA = args.Length > 3 ? args[3] : "network";
            string fileB = args.Length > 4 ? args[4] : "network";
            string? logFile = args.Length > 5 ? args[5] : null;
            double epsilon = GetArgDouble(args, 6, 0.0);   // по умолчанию 0 — детерминированная игра

            var aiX = LoadOrRandom(fileA, seed: 1);
            var aiO = LoadOrRandom(fileB, seed: 2);
            aiX.Epsilon = epsilon;
            aiO.Epsilon = epsilon;

            Console.WriteLine("=== GomokuGame Trainer / MATCHLOG ===");
            Console.WriteLine($"X plays: {fileA}, O plays: {fileB}, epsilon: {epsilon}");
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

        private static string GetSavePath(string name)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "GomokuGame", name + ".json");
        }

        private static int GetArg(string[] args, int i, int def)
            => args.Length > i && int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v : def;

        private static double GetArgDouble(string[] args, int i, double def)
            => args.Length > i && double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : def;
    }
}