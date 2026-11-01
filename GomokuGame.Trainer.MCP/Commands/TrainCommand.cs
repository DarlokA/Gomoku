using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using GomokuGame.Trainer.MCP.Models;

namespace GomokuGame.Trainer.MCP.Commands
{
    public sealed class TrainCommand : ITrainCommand
    {
        private static readonly CultureInfo _ci = CultureInfo.InvariantCulture;
        public string LearningMode { get; set; } = "C";
        public string CorrectionMode { get; set; } = "FULL";
        public string CommandName => "train";

        public async Task TrainAsync(string network, int games = 20000, Dictionary<string, string>? args = null)
        {
            Console.WriteLine($"=== Запуск команды '{CommandName}' ===");
            Console.WriteLine($"ИМЯ СЕТИ:     {network}");

            if (args == null || args.Count == 0)
            {
                args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {"games", games.ToString(_ci)},
                    {"lr", "0.01"},
                    {"epsilon", "0.3"},
                    {"decay", "0.995"},
                    {"seed", "42"},
                    {"save", network},
                    {"mode", LearningMode},
                    {"correction", CorrectionMode},
                    {"algorithm", "standard"},
                    {"chunk", "500"},
                    {"warmup", "500"},
                    {"snapshotEvery", "100"},
                    {"demo", "1"},
                    {"board", "9"},
                    {"win", "5"}
                };
            }

            Console.WriteLine($"\nЗапуск GomokuGame.Trainer.exe с аргументами: {string.Join(" ", ParseArgs(network, args))}\n");
            await RunTrainerAsync(ParseArgs(network, args).ToArray());
        }

        public async Task TrainAsync(string network, int games, string? saveName = null, int seed = 42, double learningRate = 0.01)
        {
            var argsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            argsDict["games"] = games.ToString(_ci);
            argsDict["lr"] = learningRate.ToString(_ci);
            argsDict["epsilon"] = "0.3";
            argsDict["decay"] = "0.995";
            argsDict["seed"] = seed.ToString(_ci);

            if (!string.IsNullOrWhiteSpace(saveName))
                argsDict["save"] = saveName;

            argsDict["mode"] = LearningMode;
            argsDict["correction"] = CorrectionMode;
            argsDict["algorithm"] = "standard";
            argsDict["chunk"] = "500";
            argsDict["warmup"] = "500";
            argsDict["snapshotEvery"] = "100";
            argsDict["demo"] = "1";
            argsDict["board"] = "9";
            argsDict["win"] = "5";

            Console.WriteLine($"=== Запуск команды '{CommandName}' ===");
            Console.WriteLine($"ИМЯ СЕТИ:     {network}");
            Console.WriteLine($"Партии:       {games:N0} {(games == 0 ? "(бесконечно)" : "")}");
            Console.WriteLine($"SaveName:     {saveName ?? "default"}");

            await TrainAsync(network, games, argsDict);
        }

        public async Task TrainAdvancedAsync(string network, Dictionary<string, string>? additionalArgs = null) =>
            await TrainAsync(network, 0, additionalArgs);

        private static string[] ParseArgs(string network, Dictionary<string, string> argsDict)
        {
            var result = new List<string>();
            foreach (var kvp in argsDict)
            {
                result.Add(kvp.Key);
                result.Add(kvp.Value);
            }
            result.Insert(1, network);
            return result.ToArray();
        }

        private async Task RunTrainerAsync(string[] trainerArgs)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var trainerPath = Path.Combine(baseDir, "GomokuGame.Trainer.exe");

            if (!File.Exists(trainerPath))
                throw new FileNotFoundException($"Не найден файл GomokuGame.Trainer.exe в пути: {trainerPath}");

            Console.WriteLine($"[INFO] Путь к тренеру: {trainerPath}");
            Console.WriteLine($"Аргументы: {string.Join(" ", trainerArgs)}\n");

            var psi = new ProcessStartInfo(trainerPath, string.Join(" ", trainerArgs))
            {
                UseShellExecute = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false,
                WorkingDirectory = baseDir,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            Console.WriteLine("[INFO] Запуск GomokuGame.Trainer.exe через ShellExecute...");

            using var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine($"[TRAINER OUTPUT] {e.Data}");
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine($"[TRAINER ERROR] {e.Data}");
            };

            bool started = false;
            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Не удалось запустить процесс GomokuGame.Trainer.exe");
                }
                started = true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || !started)
            {
                Console.WriteLine($"[WARN] Не удалось запустить процесс: {ex.Message}");
                Console.WriteLine("[INFO] Используя прямой вызов без перенаправления вывода...");

                try
                {
                    var simpleArgs = trainerArgs;
                    var simplePsi = new ProcessStartInfo(trainerPath, string.Join(" ", simpleArgs))
                    {
                        UseShellExecute = false,
                        WorkingDirectory = baseDir,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    };

                    using var simpleProcess = new Process { StartInfo = simplePsi };
                    simpleProcess.Start();
                    simpleProcess.WaitForExit();
                    Console.WriteLine($"[INFO] Процесс завершён с кодом: {simpleProcess.ExitCode}");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"[ERROR] Прямой вызов не удался: {ex2.Message}");
                }
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(async () =>
            {
                try
                {
                    while (process.HasExited == false)
                    {
                        await Task.Delay(100);
                    }
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        Console.WriteLine("[INFO] Процесс отменён.");
                    }
                }
            });

            await process.WaitForExitAsync();

            int exitCode = process.ExitCode;
            Console.WriteLine($"\n=== Команда 'train' завершена (код: {exitCode}) ===\n");
        }
    }
}