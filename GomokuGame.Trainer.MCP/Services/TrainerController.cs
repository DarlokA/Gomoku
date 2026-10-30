using GomokuGame.Trainer.MCP.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GomokuGame.Trainer.MCP.Services
{
    /// <summary>
    /// Контроллер для управления процессом обучения Gomoku сети.
    /// </summary>
    public sealed class TrainerController : ITrainerController
    {
        private Process? _trainerProcess;
        private readonly object _lock = new();

        /// <inheritdoc/>
        public bool IsRunning => _trainerProcess != null && !_trainerProcess.HasExited;

        /// <summary>
        /// Последний вывод тренера (для отладки).
        /// </summary>
        public string? LastOutput { get; private set; }

        /// <inheritdoc/>
        public async Task StartTrainingAsync(
            string network,
            int games,
            string? saveName = null,
            int seed = 42,
            double learningRate = 0.01)
        {
            // Если уже запущено — останавливаем предыдущий процесс
            if (IsRunning)
            {
                Console.WriteLine("[WARN] Обучение уже запущено. Остановка предыдущего процесса...");
                await StopTrainingAsync();
            }

            var ci = CultureInfo.InvariantCulture;
            var args = new List<string>();

            // Аргументы для запуска тренера
            args.Add("train");
            args.Add(network);
            args.Add(games.ToString(ci));
            args.Add(learningRate.ToString(ci));
            args.Add(0.3.ToString(ci));       // epsilon
            args.Add(0.995.ToString(ci));     // decay
            args.Add(seed.ToString(ci));

            if (!string.IsNullOrWhiteSpace(saveName))
                args.Add(saveName);

            args.Add("1");                         // showDemo = true
            args.Add("standard");                  // algorithm
            args.Add("FULL");                      // correctionMode

            args.Add("0");                        // chunk (в бесконечном режиме)
            args.Add(500.ToString(ci));           // warmupGames
            args.Add("100");                      // snapshotEvery

            Console.WriteLine($"[INFO] Запуск GomokuGame.Trainer.exe с аргументами: {string.Join(" ", args)}");

            await RunTrainerAsync(args.ToArray());
        }

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
            // Путь к исполняемому файлу тренера
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var trainerPath = Path.Combine(baseDir, "GomokuGame.Trainer.exe");

            if (!File.Exists(trainerPath))
            {
                throw new FileNotFoundException(
                    $"Не найден файл GomokuGame.Trainer.exe в пути: {trainerPath}");
            }

            Console.WriteLine($"[INFO] Путь к тренеру: {trainerPath}");

            // Запускаем процесс
            var psi = new ProcessStartInfo(trainerPath, string.Join(" ", args))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            Console.WriteLine($"[INFO] Запуск процесса...");

            _trainerProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _trainerProcess.OutputDataReceived += OnOutputReceived;
            _trainerProcess.ErrorDataReceived += OnErrorReceived;

            // Небольшая задержка для запуска перед началом чтения вывода
            await Task.Run(async () =>
            {
                try
                {
                    _trainerProcess.Start();
                    Thread.Sleep(100); // Небольшая пауза

                    _trainerProcess.BeginOutputReadLine();
                    _trainerProcess.BeginErrorReadLine();

                    // Ждём завершения процесса или пока не будет вызван Stop
                    while (IsRunning)
                    {
                        await Task.Delay(100);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Ошибка при запуске: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Получение статуса обучения.
        /// </summary>
        public async Task<Models.TrainingState> GetStatusAsync()
        {
            if (!IsRunning)
            {
                return new Models.TrainingState { Status = Models.TrainingStatus.Idle };
            }

            try
            {
                // Читаем последние строки вывода для получения статуса
                string? output = null;
                if (_trainerProcess != null)
                {
                    output = await _trainerProcess.StandardOutput.ReadToEndAsync();
                }

                // Парсим winrate и loss из последнего батча
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

                        // Winrate в логах без %, парсим напрямую
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

        /// <summary>
        /// Обработка вывода тренера.
        /// </summary>
        private void OnOutputReceived(object? sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                LastOutput = e.Data;
                Console.WriteLine(e.Data);
            }
        }

        /// <summary>
        /// Обработка ошибок тренера.
        /// </summary>
        private void OnErrorReceived(object? sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine($"[ERROR] {e.Data}");
            }
        }
    }
}