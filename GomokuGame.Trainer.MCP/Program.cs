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
                            Console.WriteLine("Пример: dotnet run train D 20000");
                        }
                        else
                        {
                            try
                            {
                                var trainCommand = new TrainCommand();
                                await trainCommand.TrainAsync(
                                    network: args[1],
                                    games: int.Parse(args[2] ?? "20000", CultureInfo.InvariantCulture),
                                    saveName: string.IsNullOrWhiteSpace(args[3]) ? null : args[3],
                                    seed: int.Parse(args[4] ?? "42", CultureInfo.InvariantCulture),
                                    learningRate: double.Parse(args[5] ?? "0.01", CultureInfo.InvariantCulture));
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
            if (args.Length == 1 && !string.IsNullOrEmpty(args[0]) && args[0].StartsWith("--", StringComparison.Ordinal) == false && args[0].ToLowerInvariant() != "train" && args[0].ToLowerInvariant() != "start" && args[0].ToLowerInvariant() != "stop" && args[0].ToLowerInvariant() != "status" && args[0].ToLowerInvariant() != "help")
            {
                var cliAnalyzer = new CliAnalyzer();
                cliAnalyzer.Analyze(args[0]);
                return;
            }
            
            // 4. Явная команда анализа: analyze <лог> (завершается после анализа)
            if (args.Length >= 2 && args[0].ToLowerInvariant() == "analyze" && !string.IsNullOrEmpty(args[1]) && args[0].StartsWith("--", StringComparison.Ordinal) == false)
            {
                var cliAnalyzer = new CliAnalyzer();
                cliAnalyzer.Analyze(args[1]);
                return;
            }
            
            // 5. Команды: start, stop, status, help — завершают после выполнения
            if (args.Length == 1 && !string.IsNullOrEmpty(args[0]) && args[0].StartsWith("--", StringComparison.Ordinal) == false && args[0].ToLowerInvariant() != "analyze" && args[0].ToLowerInvariant() != "train")
            {
                var command = args[0].ToLowerInvariant();
                
                switch (command)
                {
                    case "start":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Ошибка: Укажите сеть (A/B/C/D/E)");
                        }
                        else
                        {
                            Console.WriteLine("Внимание: Используйте команду 'train' для запуска обучения.");
                        }
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
                
                // После обработки команды завершаем
                return;
            }
            
            // 6. Интерактивный режим (пустой запуск — запускается явно пользователем)
            if (args.Length == 0 && args.All(a => string.IsNullOrWhiteSpace(a)))
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
                train <сеть> <партии> [saveName] [seed] [learningRate]  Запустить обучение (A/B/C/D/E, по умолчанию 20000 партий)
                start <сеть> [партии]       Запустить обучение (устарело, используйте 'train')
                stop                        Остановить процесс обучения
                status                      Показать текущий статус и информацию об окружении
                help / -h                   Показать эту справку

              Тестирование:
                --test                      Запустить тесты CsvLogParser

              Примеры:
                dotnet run train D 20000                     — Обучить сеть D на 20,000 партий
                dotnet run train A 50000 save_A_50k          — Обучить сеть A с сохранением
                dotnet run analyze logs/baseline_d_20000.txt  — Проанализировать лог
                dotnet run --test                           — Запустить тесты CsvLogParser
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
                            Console.Write("\nВведите сеть (A/B/C/D/E): ");
                            var network = Console.ReadLine()?.Trim() ?? "D";

                            Console.Write("Количество партий (по умолчанию 20000): ");
                            var gamesInput = Console.ReadLine()?.Trim() ?? "20000";
                            
                            int games = int.TryParse(gamesInput, out var g) && g > 0 ? g : 20000;

                            Console.Write("Имя сохранения (оставьте пустым для дефолтного): ");
                            var saveName = Console.ReadLine()?.Trim();

                            Console.Write("Seed случайных чисел (по умолчанию 42): ");
                            var seedInput = Console.ReadLine()?.Trim() ?? "42";
                            int seed = int.TryParse(seedInput, out var s) ? s : 42;

                            Console.Write("Learning rate (по умолчанию 0.01): ");
                            var lrInput = Console.ReadLine()?.Trim() ?? "0.01";
                            double learningRate = double.TryParse(lrInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var lr) ? lr : 0.01;

                            var trainCommand = new TrainCommand();
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