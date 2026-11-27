using GomokuGame.AI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static GomokuGame.Trainer.ArgumentParser;
using static GomokuGame.Trainer.TrainingArgumentsParser;
using static GomokuGame.Trainer.MatchArgumentsParser;
using static GomokuGame.Trainer.MatchLogArgumentsParser;
using static GomokuGame.Trainer.PathHelper;

namespace GomokuGame.Trainer
{
    public static class Program
    {
        private static ConsoleLogTee? s_logTee;   // активный лог консоли (если задан consolelog:)

        public static void LogShutdown()
        {
            s_logTee?.Shutdown();
        }

        public static void Main(string[] args)
        {
            // При запуске с перенаправленным выводом (из UI) консоль по умолчанию
            // пишет в OEM-кодировке (CP866), а UI читает UTF-8 — русские символы
            // превращаются в "кракозябры". Для перенаправленного вывода переходим
            // на UTF-8; интерактивная консоль сохраняет нативную кодировку.
            try
            {
                if (Console.IsOutputRedirected || Console.IsErrorRedirected)
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
            }
            catch { /* не удалось сменить кодировку — работаем как есть */ }

            if (args.Length == 0)
            {
                Console.WriteLine(
                    "Использование: GomokuGame.Trainer.exe " +
                    "<train|match|matchlog> ключ:значение ...");
                return;
            }

            string command = args[0];

            var tokens = args.Skip(1).ToList();
            var arguments = ParseKeyValueArgs(tokens);

            // Опциональный аргумент consolelog:<путь> — зеркало консоли в файл один к одному
            if (arguments.TryGetValue("consolelog", out string? logPath) && !string.IsNullOrWhiteSpace(logPath))
            {
                s_logTee = ConsoleLogTee.Create(logPath, out string? logError);
                if (logError != null)
                    Console.Error.WriteLine(logError); // ошибка не попадёт в лог: файл всё равно не открыт
            }

            try
            {
            switch (command.ToLowerInvariant())
            {
                case "train":
                    {
                        try
                        {
                            var trainingArguments = ParseTrainingArguments(arguments);
                            TrainingOrchestrator.RunTraining(trainingArguments);
                        }
                        catch (FormatException ex)
                        {
                            Console.Error.WriteLine(
                                $"Ошибка конфигурации обучения: {ex.Message}");

                            Environment.ExitCode = 1;
                        }

                        break;
                    }
                case "match":
                    RunMatch(ParseMatchArguments(arguments));
                    break;

                case "matchlog":
                    RunMatchLog(ParseMatchLogArguments(arguments));
                    break;

                default:
                    Console.WriteLine(
                        $"!! Неизвестная команда: \"{command}\". " +
                        "Ожидалась train, match или matchlog.");
                    break;
            }
            } // конец try
            finally
            {
                s_logTee?.Dispose(); // нормальный выход: закрыть файл, вернуть исходную консоль
                s_logTee = null;
            }
        }

        // ============ MATCH ============

        private static void RunMatch(MatchArguments arguments)
        {
            int boardSize = arguments.Game.BoardSize;
            int winLength = arguments.Game.WinLength;
            int games = arguments.Games;
            string fileX = arguments.FileX;
            string fileO = arguments.FileO;

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

        private static void RunMatchLog(MatchLogArguments arguments)
        {
            int boardSize = arguments.Game.BoardSize;
            int winLength = arguments.Game.WinLength;
            string fileX = arguments.FileX;
            string fileO = arguments.FileO;
            string? logFile = arguments.LogFile;
            double epsilon = arguments.Epsilon;

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

        /// <summary>
        /// Загружает сеть по имени; "random" или отсутствующий файл дают
        /// случайного игрока (Epsilon = 1.0), как было до рефакторинга.
        /// </summary>
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
    }

}
