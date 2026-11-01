using GomokuGame.Trainer.MCP.Models;
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
        /// Режим обучения (LearningMode) — A/B/C/D/E:
        /// A - reward = длина линии после хода, без исхода партии.
        /// B - reward = дисконт от победы. Ходы победителя — 0.5 + 0.5·γ^dist, остальные — 0.5.
        /// C - reward = дисконт + shaping за длину линии.
        /// D - discount + shaping за потенциальную длину линии.
        /// E - LocalTactical (LTT), локальная тактическая оценка хода.
        /// </summary>
        public string LearningMode { get; set; } = "C";

        /// <summary>
        /// Коррекция ошибок: FULL/ONCE/OFF/ALL
        /// </summary>
        public string CorrectionMode { get; set; } = "FULL";

        /// <summary>
        /// Последний вывод тренера (для отладки).
        /// </summary>
        public string? LastOutput { get; private set; }

        /// <inheritdoc/>
        public async Task TrainAsync(
            ITrainCommand command,
            string network,           // ИМЯ СЕТИ! (произвольное)
            Dictionary<string, string>? args = null)
        {
            Console.WriteLine($"=== Запуск команды '{command.CommandName}' ===");
            Console.WriteLine($"ИМЯ СЕТИ:     {network} (например: network_A, topology_test)");
            
            if (args != null && args.Count > 0)
            {
                Console.WriteLine("Аргументы (ключ:значение):");
                foreach (var kvp in args)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            await RunTrainerAsync(ParseArgs(network, args).ToArray());
        }

        /// <inheritdoc/>
        public async Task TrainAsync(
            ITrainCommand command,
            string network,           // ИМЯ СЕТИ! (произвольное)
            int games,
            string? saveName = null,  // Имя сохранения обученной сети!
            int seed = 42,
            double learningRate = 0.01)
        {
            Console.WriteLine($"=== Запуск команды '{command.CommandName}' ===");
            Console.WriteLine($"ИМЯ СЕТИ:     {network} (например: network_A, topology_test)");
            Console.WriteLine($"Партии:       {games:N0} {(games == 0 ? "(бесконечно)" : "")}");
            Console.WriteLine($"Seed:         {seed}");
            Console.WriteLine($"LearningRate: {learningRate:F4}");
            Console.WriteLine($"LearningMode: C (A/B/C/D/E)");
            Console.WriteLine($"Correction:   FULL (FULL/ONCE/OFF/ALL)");
            if (!string.IsNullOrWhiteSpace(saveName))
                Console.WriteLine($"SaveName:     {saveName}");

            var argsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Основные параметры из метода с сигнатурой (games, lr, seed)
            argsDict["games"] = games.ToString(_ci);
            argsDict["lr"] = learningRate.ToString(_ci);
            argsDict["epsilon"] = "0.3";   // epsilon для exploration (по умолчанию)
            argsDict["decay"] = "0.995";   // decay coefficient (по умолчанию)
            argsDict["seed"] = seed.ToString(_ci);

            if (!string.IsNullOrWhiteSpace(saveName))
                argsDict["save"] = saveName;

            // Режим обучения и коррекция (по умолчанию из класса TrainerController, но можно переопределить через args)
            argsDict["mode"] = LearningMode;       // A/B/C/D/E
            argsDict["correction"] = CorrectionMode; // FULL/ONCE/OFF/ALL

            // Дополнительные параметры тренера с разумными значениями по умолчанию
            argsDict["algorithm"] = "standard";
            argsDict["chunk"] = "500";     // размер чанка для обучения
            argsDict["warmup"] = "500";    // warmup партий
            argsDict["snapshotEvery"] = "100"; // снимки каждые N партий
            argsDict["demo"] = "1";         // показывать демонические игры (true=1)
            argsDict["board"] = "9";        // размер доски 9x9 (по умолчанию)
            argsDict["win"] = "5";          // длина победы 5 (по умолчанию)

            Console.WriteLine($"\nЗапуск GomokuGame.Trainer.exe с аргументами: {string.Join(" ", ParseArgs(network, argsDict))}\n");

            await RunTrainerAsync(ParseArgs(network, argsDict).ToArray());
        }

        /// <inheritdoc/>
        public async Task TrainAdvancedAsync(
            ITrainCommand command,
            string network,           // ИМЯ СЕТИ!
            Dictionary<string, string>? additionalArgs = null)
        {
            Console.WriteLine($"=== Запуск команды '{command.CommandName}' (advanced) ===");
            Console.WriteLine($"ИМЯ СЕТИ:     {network}");

            // Создаем словарь аргументов с дефолтными значениями
            var argsDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Параметры из additionalArgs или дефолтные значения
            if (additionalArgs?.ContainsKey("games") == true)
                argsDict["games"] = GetArgInt(additionalArgs, "games", 20000).ToString(_ci);
            else
                argsDict["games"] = "20000";

            argsDict["lr"] = GetArgDouble(additionalArgs, "lr", 0.01).ToString(_ci);
            argsDict["epsilon"] = GetArgDouble(additionalArgs, "epsilon", 0.3).ToString(_ci);
            argsDict["decay"] = GetArgDouble(additionalArgs, "decay", 0.995).ToString(_ci);
            argsDict["seed"] = GetArgInt(additionalArgs, "seed", 42).ToString(_ci);

            if (additionalArgs?.ContainsKey("save") == true)
                argsDict["save"] = additionalArgs["save"];

            // Режим обучения и коррекция (из полей класса TrainerController, но можно переопределить через args)
            if (additionalArgs?.ContainsKey("mode") == true)
                argsDict["mode"] = additionalArgs["mode"].ToUpperInvariant();
            else
                argsDict["mode"] = LearningMode;

            if (additionalArgs?.ContainsKey("correction") == true)
                argsDict["correction"] = additionalArgs["correction"].ToUpperInvariant();
            else
                argsDict["correction"] = CorrectionMode;

            // Дополнительные параметры тренера с разумными значениями по умолчанию
            argsDict["algorithm"] = GetArg(additionalArgs, "algorithm", "standard");
            argsDict["chunk"] = GetArgInt(additionalArgs, "chunk", 500).ToString(_ci);
            argsDict["warmup"] = GetArgInt(additionalArgs, "warmup", 500).ToString(_ci);
            argsDict["snapshotEvery"] = GetArgInt(additionalArgs, "snapshotEvery", 100).ToString(_ci);
            argsDict["demo"] = GetArg(additionalArgs, "demo", "1");
            argsDict["board"] = GetArgInt(additionalArgs, "board", 9).ToString(_ci);
            argsDict["win"] = GetArgInt(additionalArgs, "win", 5).ToString(_ci);

            Console.WriteLine($"\nЗапуск GomokuGame.Trainer.exe с аргументами: {string.Join(" ", ParseArgs(network, argsDict))}\n");

            await RunTrainerAsync(ParseArgs(network, argsDict).ToArray());
        }

        private static string[] ParseArgs(string network, Dictionary<string, string> argsDict)
        {
            var result = new List<string>();
            foreach (var kvp in argsDict)
            {
                result.Add(kvp.Key);
                result.Add(kvp.Value);
            }
            // Добавляем ИМЯ СЕТИ в начало (после команды train, которая подразумевается)
            result.Insert(1, network);
            return result.ToArray();
        }

        private static string GetArg(Dictionary<string, string>? args, string key, string def = "")
            => args?.ContainsKey(key) == true && !string.IsNullOrEmpty(args[key]) ? args[key] : def;

        private static double GetArgDouble(Dictionary<string, string>? args, string key, double def)
            => args?.ContainsKey(key) == true && double.TryParse(
                args[key], _ci, out var v) ? v : def;

        private static int GetArgInt(Dictionary<string, string>? args, string key, int def)
            => args?.ContainsKey(key) == true && int.TryParse(
                args[key], _ci, out var v) ? v : def;

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

            Console.WriteLine("[INFO] Запуск процесса через ShellExecute...");

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
                                lastLoss = double.Parse(parts[1].Trim(), _ci);
                        }

                        if (line.Contains("Winrate="))
                        {
                            var parts = line.Split('=', 1, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                                lastWinRate = double.Parse(parts[1].Trim(), _ci);
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