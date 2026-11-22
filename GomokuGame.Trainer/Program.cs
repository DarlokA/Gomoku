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
using GomokuGame.AI.Configuration;
using static GomokuGame.Trainer.ArgumentParser;
using static GomokuGame.Trainer.TrainingArgumentsParser;
using static GomokuGame.Trainer.MatchArgumentsParser;
using static GomokuGame.Trainer.MatchLogArgumentsParser;
using static GomokuGame.Trainer.PathHelper;

namespace GomokuGame.Trainer
{
    public static class Program
    {
        private static readonly object SaveLock = new object();
        private static ConsoleLogTee? s_logTee;   // активный лог консоли (если задан consolelog:)

        public static void LogShutdown()
        {
            s_logTee?.Shutdown();
        }

        public static void Main(string[] args)
        {
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

















        private static void RunGeneticTraining(
            TrainingArguments arguments)
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


        // ============ MATCH ============

        private static void RunMatch(MatchArguments arguments)
        {
            int boardSize = arguments.Game.BoardSize;
            int winLength = arguments.Game.WinLength;
            int games = arguments.Games;
            string fileX = arguments.FileX;
            string fileO = arguments.FileO;

            var aiX = AiPlayer.LoadOrCreate(
    PathHelper.GetSavePath(fileX),
    1);
aiX.Epsilon = 0.4;
aiX.UseRotatingEvaluation = false;
            var aiO = AiPlayer.LoadOrCreate(
    PathHelper.GetSavePath(fileO),
    2);
aiO.Epsilon = 0.4;
aiO.UseRotatingEvaluation = false;


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

            var aiX = AiPlayer.LoadOrCreate(
    PathHelper.GetSavePath(fileX),
    1);
aiX.Epsilon = 0.4;
aiX.UseRotatingEvaluation = false;
            var aiO = AiPlayer.LoadOrCreate(
    PathHelper.GetSavePath(fileO),
    2);
aiO.Epsilon = 0.4;
aiO.UseRotatingEvaluation = false;
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

        // ============ HELPERS (оставлено для матчей) ============

        public static string GetSavePath(string name)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "GomokuGame", name + ".json");
        }
    }

}
