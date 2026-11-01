using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GomokuGame.Trainer.MCP.Models;
using GomokuGame.Trainer.MCP.Services;
using GomokuGame.Trainer.MCP.LogAnalyzers;
using GomokuGame.Trainer.MCP.Commands;

namespace GomokuGame.Trainer.MCP
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Gomoku Trainer MCP ===");
            
            // 1. Тестирование (высокий приоритет)
            if (args.Contains("--test"))
            {
                TestRunner.RunAllTests();
                return;
            }
            
            // 2. Команда train (запуск обучения) — завершается после выполнения
            if (args.Length == 1 && !string.IsNullOrEmpty(args[0]) && args[0].StartsWith("--", StringComparison.Ordinal) == false)
            {
                var command = args[0].ToLowerInvariant();
                
                switch (command)
                {
                    case "train":
                        // train <сеть> [партии] — запуск обучения
                        if (args.Length == 1 || string.IsNullOrEmpty(args[1]))
                        {
                            Console.WriteLine("Ошибка: Укажите сеть и количество партий для обучения");
                            Console.WriteLine("Пример: dotnet run train network_A 20000");
                        }
                        else
                        {
                            try
                            {
                                // Простая форма: train <сеть> [партии] [saveName] [seed] [learningRate]
                                var network = args[1];
                                
                                int games = 20000; // Дефолтное значение
                                string? saveName = null;
                                int seed = 42;
                                double learningRate = 0.01;

                                if (args.Length >= 3 && !string.IsNullOrEmpty(args[2]))
                                {
                                    // args[2] может быть числом (games) или строкой (saveName)
                                    var gValue = int.TryParse(args[2], CultureInfo.InvariantCulture, out var g) 
                                        ? Math.Max(1, g) 
                                        : default(int?);
                                    
                                    if (gValue.HasValue && gValue.Value > 0)
                                    {
                                        games = gValue.Value;
                                        if (args.Length >= 4 && !string.IsNullOrEmpty(args[3]))
                                        {
                                            // Если есть третий аргумент — это saveName или seed/lr
                                            var s2Value = int.TryParse(args[3], CultureInfo.InvariantCulture, out var s) && s > 0 
                                                ? s 
                                                : default(int);
                                            if (s2Value > 0)
                                                seed = s2Value;
                                            else
                                                saveName = args[3];
                                        }
                                    }
                                    else
                                    {
                                        // Это saveName
                                        saveName = args[2];
                                        if (args.Length >= 4 && !string.IsNullOrEmpty(args[3]))
                                        {
                                            // Если есть четвёртый аргумент — это seed или lr
                                            var s3Value = int.TryParse(args[3], CultureInfo.InvariantCulture, out var seedValue) && seedValue > 0 
                                                ? seedValue 
                                                : default(int);
                                            if (s3Value > 0)
                                                seed = s3Value;
                                        }
                                    }
                                }

                                var trainCommand = new TrainCommand();
                                await trainCommand.TrainAsync(
                                    network: network,
                                    games: games,
                                    saveName: saveName,
                                    seed: seed,
                                    learningRate: learningRate);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Ошибка запуска команды train: {ex.Message}");
                            }
                        }
                        return;
                    
                    case "start":
                        Console.WriteLine("Ошибка: Используйте команду 'train' для запуска обучения.");
                        break;
                    case "stop":
                        var trainer = new TrainerController();
                        await trainer.StopTrainingAsync();
                        Console.WriteLine("[INFO] Процесс остановлен.");
                        break;
                    case "status":
                        var tr = new TrainerController();
                        PrintEnvironmentInfo();
                        Console.WriteLine($"Текущий статус: {(tr.IsRunning ? "ЗАПУЩЕН" : "ОСТАНОВЛЕН")}");
                        break;
                    case "help":
                    case "-h":
                        PrintHelp();
                        break;
                    default:
                        Console.WriteLine("Неизвестная команда. Введите 'train', 'start', 'stop', 'status' или 'help'.");
                        break;
                }
                return;
            }
            
            // 3. Анализ CSV-лога: <лог> напрямую (завершается после анализа)
            if (args.Length == 1 && !string.IsNullOrEmpty(args[0]) && args[0].StartsWith("--", StringComparison.Ordinal) == false && 
                args[0].ToLowerInvariant() != "train" && args[0].ToLowerInvariant() != "start" && 
                args[0].ToLowerInvariant() != "stop" && args[0].ToLowerInvariant() != "status" && args[0].ToLowerInvariant() != "help")
            {
                var cliAnalyzer = new CliAnalyzer();
                cliAnalyzer.Analyze(args[0]);
                return;
            }
            
            // 4. Явная команда анализа: analyze <лог> (завершается после анализа)
            if (args.Length >= 2 && args[0].ToLowerInvariant() == "analyze" && !string.IsNullOrEmpty(args[1]) && 
                args[0].StartsWith("--", StringComparison.Ordinal) == false)
            {
                var cliAnalyzer = new CliAnalyzer();
                cliAnalyzer.Analyze(args[1]);
                return;
            }
            
            // 5. Команды: start, stop, status, help — завершают после выполнения
            if (args.Length == 1 && !string.IsNullOrEmpty(args[0]) && args[0].StartsWith("--", StringComparison.Ordinal) == false && 
                args[0].ToLowerInvariant() != "analyze" && args[0].ToLowerInvariant() != "train")
            {
                var command = args[0].ToLowerInvariant();
                
                switch (command)
                {
                    case "start":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Ошибка: Укажите сеть");
                        }
                        else
                        {
                            Console.WriteLine("Внимание: Используйте команду 'train' для запуска обучения.");
                        }
                        break;

                    case "stop":
                        var trainer2 = new TrainerController();
                        await trainer2.StopTrainingAsync();
                        Console.WriteLine("[INFO] Процесс остановлен.");
                        break;

                    case "status":
                        var tr2 = new TrainerController();
                        PrintEnvironmentInfo();
                        Console.WriteLine($"Текущий статус: {(tr2.IsRunning ? "ЗАПУЩЕН" : "ОСТАНОВЛЕН")}");
                        break;

                    case "help":
                    case "-h":
                        PrintHelp();
                        break;

                    default:
                        Console.WriteLine("Неизвестная команда. Введите 'train', 'start', 'stop', 'status' или 'help'.");
                        break;
                }
                
                // После обработки команды завершаем
                return;
            }
            
            // 6. Интерактивный режим (пустой запуск — запускается явно пользователем)
            if (args.Length == 0)
            {
                await InteractiveMode();
            }
        }

        static void PrintHelp()
        {
            string helpText = @"
              Gomoku Trainer MCP - CLI для управления обучением и анализа логов

              Команды:
                <лог>                        Автоматически анализировать CSV-лог (завершается после анализа)
                analyze <лог>               Анализировать CSV-лог с явной командой
                train <сеть> [партии]       Запустить обучение (ИМЯ СЕТИ, по умолчанию 20000 партий)
                stop                        Остановить процесс обучения
                status                      Показать текущий статус и информацию об окружении
                help / -h                   Показать эту справку

              Тестирование:
                --test                      Запустить тесты CsvLogParser

              Параметры команды train:
                train <сеть> [партии]           — Обучение с указанным числом партий
                train <сеть> --games:N          — Количество партий (N)
                train <сеть> --lr:N             — Learning rate
                train <сеть> --epsilon:N        — Epsilon для exploration
                train <сеть> --decay:N          — Decay coefficient
                train <сеть> --seed:N           — Seed случайных чисел
                train <сеть> --save:имя         — Имя сохранения сети
                train <сеть> --mode:A|B|C|D|E   — Режим обучения (LearningMode)
                train <сеть> --correction:X     — Коррекция ошибок (FULL/ONCE/OFF/ALL)
                train <сеть> --algorithm:alg    — Алгоритм обучения (standard/league/genetic/bot)
                train <сеть> --chunk:N          — Размер чанка для обучения
                train <сеть> --warmup:N         — Warmup партий
                train <сеть> --snapshotEvery:N  — Снимки каждые N партий
                train <сеть> --demo:0|1         — Показывать демо игры (по умолчанию 1)
                train <сеть> --board:N          — Размер доски (по умолчанию 9)
                train <сеть> --win:N            — Длина победы (по умолчанию 5)

              Примеры:
                # Обучить сеть network_A на 20000 партий в режиме C (по умолчанию)
                dotnet run train network_A 20000
                
                # Обучить сеть topology_test на 50000 партий с именем сохранения
                dotnet run train topology_test \
                  --games:50000 \
                  --lr:0.01 \
                  --epsilon:0.3 \
                  --decay:0.995 \
                  --seed:42 \
                  --save:topology_50k \
                  --mode:C \
                  --correction:FULL
                
                # Обучение в режиме B с бесконечным обучением
                dotnet run train network_B \
                  --mode:B \
                  --games:0 \
                  --lr:0.01
                
                # Обучение в режиме E (LTT) с коррекцией OFF
                dotnet run train network_A \
                  --mode:E \
                  --correction:OFF
                
                # Анализировать CSV-лог
                dotnet run analyze tests\baseline_d_20000_permuted.txt
                
                # Запустить тесты CsvLogParser
                dotnet run --test
            ";
            Console.WriteLine(helpText.Trim());
        }

        static void PrintEnvironmentInfo()
        {
            Console.WriteLine("=== Информация об окружении ===");
            Console.WriteLine($"Путь к тренеру: {ConfigurationLoader.GetTrainerPath()}");
            Console.WriteLine($"Папка паттерн-логов: {ConfigurationLoader.GetPatternLogsPath()}");
            Console.WriteLine($"Папка логов живых партий: {ConfigurationLoader.GetLiveLogsPath()}");
        }

        static async Task InteractiveMode()
        {
            Console.WriteLine("=== Интерактивный режим ===\n");

            while (true)
            {
                try
                {
                    var trainer = new TrainerController();
                    Console.WriteLine($"Текущий статус: {(trainer.IsRunning ? "ЗАПУЩЕН" : "ОСТАНОВЛЕН")}");

                    Console.Write("\nКоманда (train/start/stop/status/help): ");

                    var input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input)) continue;

                    var command = input.Trim().ToLowerInvariant();

                    switch (command)
                    {
                        case "train":
                            Console.Write("\nВведите ИМЯ СЕТИ (например: network_A, topology_test): ");
                            var network = Console.ReadLine()?.Trim() ?? "network_D";

                            Console.Write("Количество партий (по умолчанию 20000 или 0 для бесконечно): ");
                            string gamesInput;
                            int games = int.TryParse(gamesInput = Console.ReadLine()?.Trim() ?? "", out var g) && g > 0 ? g : 20000;

                            Console.Write("Имя сохранения (оставьте пустым для дефолтного): ");
                            var saveName = Console.ReadLine()?.Trim();

                            Console.Write("Seed случайных чисел (по умолчанию 42): ");
                            var seedInput = Console.ReadLine()?.Trim() ?? "42";
                            int seed = int.TryParse(seedInput, out var s) ? s : 42;

                            Console.Write("Learning rate (по умолчанию 0.01): ");
                            var lrInput = Console.ReadLine()?.Trim() ?? "0.01";
                            double learningRate = double.TryParse(lrInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var lr) ? lr : 0.01;

                            // Режим обучения (опционально)
                            Console.Write("Режим обучения A/B/C/D/E (по умолчанию C): ");
                            var modeInput = Console.ReadLine()?.Trim().ToUpperInvariant();
                            string learningMode = modeInput != null && Array.Exists(new[] { "A", "B", "C", "D", "E" }, x => x.Equals(modeInput, StringComparison.OrdinalIgnoreCase)) 
                                ? modeInput : "C";

                            // Коррекция (опционально)
                            Console.Write("Коррекция FULL/ONCE/OFF/ALL (по умолчанию FULL): ");
                            var correctionInput = Console.ReadLine()?.Trim().ToUpperInvariant();
                            string correctionMode = correctionInput != null && Array.Exists(new[] { "FULL", "ONCE", "OFF", "ALL" }, x => x.Equals(correctionInput, StringComparison.OrdinalIgnoreCase))
                                ? correctionInput : "FULL";

                            var trainCommand = new TrainCommand
                            {
                                LearningMode = learningMode,
                                CorrectionMode = correctionMode
                            };

                            await trainer.TrainAsync(
                                command: trainCommand,
                                network: network,
                                games: games,
                                saveName: string.IsNullOrWhiteSpace(saveName) ? null : saveName,
                                seed: seed,
                                learningRate: learningRate);
                            break;

                        case "start":
                            Console.WriteLine("Внимание: Используйте команду 'train' для запуска обучения.");
                            break;

                        case "stop":
                            await trainer.StopTrainingAsync();
                            Console.WriteLine("[INFO] Процесс остановлен.");
                            break;

                        case "status":
                            var tr = new TrainerController();
                            Console.WriteLine($"Текущий статус: {(tr.IsRunning ? "ЗАПУЩЕН" : "ОСТАНОВЛЕН")}");
                            break;

                        case "help":
                        case "-h":
                            PrintHelp();
                            break;

                        default:
                            Console.WriteLine("Неизвестная команда. Введите 'train', 'start', 'stop', 'status' или 'help'.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nОшибка: {ex.Message}");
                    break;
                }
            }
        }
    }
}