using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using GomokuGame.Trainer.MCP.Models;

namespace GomokuGame.Trainer.MCP.Commands
{
    /// <summary>
    /// Команда запуска обучения Gomoku сети через ShellExecute.
    /// Поддерживает все параметры командной строки приложения GomokuGame.Trainer.exe
    /// </summary>
    public sealed class TrainCommand : ITrainCommand
    {
        private static readonly CultureInfo _ci = CultureInfo.InvariantCulture;

        /// <summary>
        /// Режим обучения сети (LearningMode):
        /// A - reward = длина линии после хода, без исхода партии.
        /// B - reward = дисконт от победы. Ходы победителя — 0.5 + 0.5·γ^dist, остальные — 0.5.
        /// C - reward = дисконт + shaping за длину линии.
        /// D - discount + shaping за потенциальную длину линии.
        /// E - LocalTactical (LTT), локальная тактическая оценка хода.
        /// </summary>
        public string LearningMode { get; set; } = "C"; // DiscountedWithShaping (по умолчанию)

        /// <summary>
        /// Коррекция ошибок: FULL/ONCE/OFF/ALL
        /// </summary>
        public string CorrectionMode { get; set; } = "FULL";

        /// <inheritdoc/>
        public string CommandName => "train";

        /// <inheritdoc/>
        public Task TrainAsync(
            string network,           // ИМЯ СЕТИ! (произвольное, например: "network_A", "topology_test")
            Dictionary<string, string>? args = null)
        {
            return TrainAdvancedAsync(network, args);
        }

        /// <inheritdoc/>
        public async Task TrainAsync(
            string network,           // ИМЯ СЕТИ! (произвольное!)
            int games,
            string? saveName = null,  // Имя сохранения обученной сети!
            int seed = 42,
            double learningRate = 0.01)
        {
            Console.WriteLine($"=== Запуск команды '{CommandName}' ===");
            Console.WriteLine($"ИМЯ СЕТИ:     {network} (например: network_A, topology_test)");
            Console.WriteLine($"Партии:       {games:N0} {(games == 0 ? "(бесконечно)" : "")}");
            Console.WriteLine($"Seed:         {seed}");
            Console.WriteLine($"LearningRate: {learningRate:F4}");
            Console.WriteLine($"LearningMode: {LearningMode} (A/B/C/D/E)");
            Console.WriteLine($"Correction:   {CorrectionMode}");
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

            // Режим обучения и коррекция (из полей класса, но можно переопределить через args)
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

        /// <summary>
        /// Расширенная версия TrainAsync со всеми параметрами тренера.
        /// </summary>
        public async Task TrainAdvancedAsync(
            string network,           // ИМЯ СЕТИ! (произвольное!)
            Dictionary<string, string>? additionalArgs = null)
        {
            Console.WriteLine($"=== Запуск команды '{CommandName}' (advanced) ===");
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

            // Режим обучения и коррекция (из полей класса, но можно переопределить через args)
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

        /// <summary>
        /// Парсит Dictionary<string, string> в список аргументов командной строки.
        /// Формат: train ИМЯ_СЕТИ games:N lr:N epsilon:N ...
        /// </summary>
        private static List<string> ParseArgs(string network, Dictionary<string, string> argsDict)
        {
            var result = new List<string>();
            foreach (var kvp in argsDict)
            {
                result.Add(kvp.Key);
                result.Add(kvp.Value);
            }
            // Добавляем ИМЯ СЕТИ в начало (после команды train, которая подразумевается)
            result.Insert(1, network);
            return result;
        }

        private static string GetArg(Dictionary<string, string>? args, string key, string def = "")
            => args?.ContainsKey(key) == true && !string.IsNullOrEmpty(args[key]) ? args[key] : def;

        private static double GetArgDouble(Dictionary<string, string>? args, string key, double def)
            => args?.ContainsKey(key) == true && double.TryParse(
                args[key], _ci, out var v) ? v : def;

        private static int GetArgInt(Dictionary<string, string>? args, string key, int def)
            => args?.ContainsKey(key) == true && int.TryParse(
                args[key], _ci, out var v) ? v : def;

        /// <summary>
        /// Запустить процесс тренера и ожидать завершения.
        /// </summary>
        private async Task RunTrainerAsync(string[] args)
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
                    while (true)
                    {
                        await Task.Delay(100);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[INFO] Запуск обучения отменён.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Процесс завершён с ошибкой: {ex.Message}");
                }
            });

            await process.WaitForExitAsync();

            Console.WriteLine("\n=== Команда 'train' завершена ===\n");
        }
    }
}