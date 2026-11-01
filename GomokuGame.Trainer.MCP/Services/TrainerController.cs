using GomokuGame.Trainer.MCP.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GomokuGame.Trainer.MCP.Commands;

namespace GomokuGame.Trainer.MCP.Services
{
    /// <summary>
    /// Контроллер для управления процессом обучения Gomoku сети.
    /// </summary>
    public sealed class TrainerController : ITrainerController
    {
        private static readonly CultureInfo _ci = CultureInfo.InvariantCulture;
        private Process? _trainerProcess;
        private readonly object _lock = new();

        /// <inheritdoc/>
        public bool IsRunning => _trainerProcess != null && !_trainerProcess.HasExited;

        /// <summary>
        /// Последний вывод тренера (для отладки).
        /// </summary>
        public string? LastOutput { get; private set; }

        /// <inheritdoc/>
        public async Task TrainAsync(
            ITrainCommand command,
            string network,
            int games,
            string? saveName = null,
            int seed = 42,
            double learningRate = 0.01)
        {
            Console.WriteLine($"=== Запуск команды '{command.CommandName}' ===");
            Console.WriteLine($"Сеть:     {network}");
            Console.WriteLine($"Партии:   {games:N0} {(games == 0 ? "(бесконечно)" : "")}");
            Console.WriteLine($"Seed:     {seed}");
            Console.WriteLine($"LearningRate: {learningRate:F4}");
            if (!string.IsNullOrWhiteSpace(saveName))
                Console.WriteLine($"SaveName: {saveName}");

            var ci = CultureInfo.InvariantCulture;
            var args = new List<string>();

            args.Add("train");
            args.Add(network);
            args.Add(games.ToString(ci));
            args.Add(learningRate.ToString(ci));
            args.Add(0.3.ToString(ci));       // epsilon
            args.Add(0.995.ToString(ci));     // decay
            args.Add(seed.ToString(ci));

            if (!string.IsNullOrWhiteSpace(saveName))
                args.Add(saveName);

            args.Add("1");                     // showDemo = true
            args.Add("standard");              // algorithm
            args.Add("FULL");                 // correctionMode

            args.Add("0");                    // chunk (в бесконечном режиме)
            args.Add(500.ToString(ci));      // warmupGames
            args.Add("100");                  // snapshotEvery

            Console.WriteLine($"[INFO] Запуск GomokuGame.Trainer.exe с аргументами: {string.Join(" ", args)}\n");

            await RunTrainerAsync(args.ToArray());
        }

        /// <inheritdoc/>
        public async Task TrainAdvancedAsync(
            ITrainCommand command,
            string network,
            Dictionary<string, string>? additionalArgs = null)
        {
            Console.WriteLine($"=== Запуск команды '{command.CommandName}' (advanced) ===");
            Console.WriteLine($"Сеть:     {network}");

            var args = new List<string>();

            args.Add("train");
            args.Add(network);
            
            var games = GetArgInt(additionalArgs, "games", 20000);
            args.Add($"games:{games}");
            args.Add($"lr:{GetArgDouble(additionalArgs, "lr", 0.01)}");
            args.Add($"epsilon:{GetArgDouble(additionalArgs, "epsilon", 0.3)}");
            args.Add($"decay:{GetArgDouble(additionalArgs, "decay", 0.995)}");
            args.Add($"seed:{GetArgInt(additionalArgs, "seed", 42).ToString(_ci)}");

            if (!string.IsNullOrWhiteSpace(GetArg(additionalArgs, "save")))
                args.Add($"save:{GetArg(additionalArgs, "save")}");

            args.Add("algorithm:standard");
            args.Add("mode:C");                 // DiscountedWithShaping
            args.Add("correction:FULL");        // CorrectionMode.Full
            args.Add("chunk:500");              // размер чанка
            args.Add("warmup:500");             // warmup партий
            args.Add("snapshotEvery:100");      // снимки каждые N партий
            args.Add("demo:1");                 // показывать демо игры
            args.Add("board:9");                // размер доски
            args.Add($"win:5");                 // длина победы

            Console.WriteLine($"\nЗапуск GomokuGame.Trainer.exe с аргументами: {string.Join(" ", args)}\n");

            await RunTrainerAsync(args.ToArray());
        }

        private string GetArg(Dictionary<string, string>? args, string key, string def = "")
            => args?.ContainsKey(key) == true && !string.IsNullOrEmpty(args[key]) ? args[key] : def;

        private double GetArgDouble(Dictionary<string, string>? args, string key, double def)
            => args?.ContainsKey(key) == true && double.TryParse(
                args[key], CultureInfo.InvariantCulture, out var v)
                ? v : def;

        private int GetArgInt(Dictionary<string, string>? args, string key, int def)
            => args?.ContainsKey(key) == true && int.TryParse(
                args[key], CultureInfo.InvariantCulture, out var v)
                ? v : def;

        /// <inheritdoc/>
        public Task StopTrainingAsync()
        {
            if (!IsRunning)
            {
                Console.WriteLine("[INFO] Процесс обучения не запущен.");
                return Task.CompletedTask;
            }

            try
            {
                Console.WriteLine("[WARN] Остановка процесса обучения...");

                lock (_lock)
                {
                    if (_trainerProcess != null && !_trainerProcess.HasExited)
                    {
                        _trainerProcess.Kill(entireProcessTree: true);
                    }

                    _trainerProcess = null;
                }

                Console.WriteLine("[INFO] Процесс остановлен.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Не удалось остановить процесс: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task RunTrainerAsync(string[] args)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var trainerPath = Path.Combine(baseDir, "GomokuGame.Trainer.exe");

            if (!File.Exists(trainerPath))
            {
                throw new FileNotFoundException(
                    $"Не найден файл GomokuGame.Trainer.exe в пути: {trainerPath}");
            }

            Console.WriteLine($"[INFO] Путь к тренеру: {trainerPath}");

            var psi = new ProcessStartInfo(trainerPath, string.Join(" ", args))
            {
                UseShellExecute = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            Console.WriteLine($"[INFO] Запуск процесса через ShellExecute...");

            using var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"[TRAINER] {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"[ERROR TRAINER] {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(async () =>
            {
                try
                {
                    while (IsRunning)
                    {
                        await Task.Delay(100);
                    }
                    
                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine($"\n[INFO] Процесс тренера завершён успешно (код: {process.ExitCode})");
                    }
                    else
                    {
                        Console.WriteLine($"\n[WARN] Процесс тренера завершился с кодом: {process.ExitCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Процесс завершён с ошибкой: {ex.Message}");
                }
            });

            await process.WaitForExitAsync();
        }

        /// <inheritdoc/>
        public async Task<Models.TrainingState> GetStatusAsync()
        {
            if (!IsRunning)
            {
                return new Models.TrainingState { Status = Models.TrainingStatus.Idle };
            }

            try
            {
                string? output = null;
                if (_trainerProcess != null)
                {
                    output = await _trainerProcess.StandardOutput.ReadToEndAsync();
                }

                double? lastLoss = null;
                double? lastWinRate = null;
                int? currentBatch = null;
                int? totalBatches = null;

                if (output != null)
                {
                    var lines = output.Split('\n');

                    foreach (var line in lines)
                    {
                        if (line.Contains("Loss="))
                        {
                            var parts = line.Split('=', 1, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                                lastLoss = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                        }

                        if (line.Contains("Winrate="))
                        {
                            var parts = line.Split('=', 1, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                                lastWinRate = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                        }

                        if (line.Contains("Batch "))
                        {
                            var parts = line.Split(new[] { "Batch " }, StringSplitOptions.None);
                            if (parts.Length >= 2)
                            {
                                var batchInfo = parts[1].Split('/');
                                if (batchInfo.Length >= 2)
                                {
                                    currentBatch = int.Parse(batchInfo[0]);
                                    totalBatches = int.Parse(batchInfo[1]);
                                }
                            }
                        }
                    }
                }

                return new Models.TrainingState
                {
                    Status = Models.TrainingStatus.Running,
                    CurrentBatch = currentBatch,
                    TotalBatches = totalBatches,
                    LastLoss = lastLoss,
                    LastWinRate = lastWinRate
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Ошибка получения статуса: {ex.Message}");
                return new Models.TrainingState { Status = Models.TrainingStatus.Error };
            }
        }
    }
}