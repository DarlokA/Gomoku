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
        /// <inheritdoc/>
        public string CommandName => "train";

        private readonly CultureInfo _ci = CultureInfo.InvariantCulture;

        /// <inheritdoc/>
        public async Task TrainAsync(
            string network,
            int games,
            string? saveName = null,
            int seed = 42,
            double learningRate = 0.01)
        {
            Console.WriteLine($"=== Запуск команды '{CommandName}' ===");
            Console.WriteLine($"Сеть:     {network}");
            Console.WriteLine($"Партии:   {games:N0} {(games == 0 ? "(бесконечно)" : "")}");
            Console.WriteLine($"Seed:     {seed}");
            Console.WriteLine($"LearningRate: {learningRate:F4}");
            if (!string.IsNullOrWhiteSpace(saveName))
                Console.WriteLine($"SaveName: {saveName}");

            var args = new List<string>();

            args.Add("train");
            args.Add(network);
            args.Add($"games:{games}");
            args.Add($"lr:{learningRate}");
            args.Add($"epsilon:0.3");
            args.Add($"decay:0.995");
            args.Add($"seed:{seed}");

            if (!string.IsNullOrWhiteSpace(saveName))
                args.Add($"save:{saveName}");

            args.Add("algorithm:standard");
            args.Add("mode:C");
            args.Add("correction:FULL");
            args.Add("chunk:500");
            args.Add("warmup:500");
            args.Add("snapshotEvery:100");
            args.Add("demo:1");
            args.Add("board:9");
            args.Add($"win:5");

            Console.WriteLine($"\nЗапуск GomokuGame.Trainer.exe с аргументами: {string.Join(" ", args)}\n");

            await RunTrainerAsync(args.ToArray());
        }

        /// <summary>
        /// Расширенная версия TrainAsync со всеми параметрами тренера.
        /// </summary>
        public async Task TrainAdvancedAsync(
            string network,
            Dictionary<string, string>? additionalArgs = null)
        {
            Console.WriteLine($"=== Запуск команды '{CommandName}' (advanced) ===");
            Console.WriteLine($"Сеть:     {network}");

            var args = new List<string>();

            args.Add("train");
            args.Add(network);
            
            args.Add($"games:{GetArgInt(additionalArgs, "games", 20000)}");
            args.Add($"lr:{GetArgDouble(additionalArgs, "lr", 0.01)}");
            args.Add($"epsilon:{GetArgDouble(additionalArgs, "epsilon", 0.3)}");
            args.Add($"decay:{GetArgDouble(additionalArgs, "decay", 0.995)}");
            args.Add($"seed:{GetArgInt(additionalArgs, "seed", 42)}");

            if (!string.IsNullOrWhiteSpace(GetArg(additionalArgs, "save")))
                args.Add($"save:{GetArg(additionalArgs, "save")}");

            args.Add("algorithm:standard");
            args.Add("mode:C");
            args.Add("correction:FULL");
            args.Add("chunk:500");
            args.Add("warmup:500");
            args.Add("snapshotEvery:100");
            args.Add("demo:1");
            args.Add("board:9");
            args.Add($"win:5");

            Console.WriteLine($"\nЗапуск GomokuGame.Trainer.exe с аргументами: {string.Join(" ", args)}\n");

            await RunTrainerAsync(args.ToArray());
        }

        private string GetArg(Dictionary<string, string>? args, string key, string def = "")
            => args?.ContainsKey(key) == true && !string.IsNullOrEmpty(args[key]) ? args[key] : def;

        private double GetArgDouble(Dictionary<string, string>? args, string key, double def)
            => args?.ContainsKey(key) == true && double.TryParse(
                args[key], _ci, out var v) ? v : def;

        private int GetArgInt(Dictionary<string, string>? args, string key, int def)
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