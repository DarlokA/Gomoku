using System;
using System.IO;
using GomokuGame.Trainer.MCP.Models;
using GomokuGame.Trainer.MCP.Services;
using GomokuGame.Trainer.MCP.LogAnalyzers;

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
            
            // 2. Анализ CSV-лога: <лог> напрямую (завершается после анализа)
            if (args.Length == 1 && !string.IsNullOrEmpty(args[0]) && args[0].StartsWith("--", StringComparison.Ordinal) == false)
            {
                var cliAnalyzer = new CliAnalyzer();
                cliAnalyzer.Analyze(args[0]);
                return;
            }
            
            // 3. Явная команда анализа: analyze <лог> (завершается после анализа)
            if (args.Length >= 2 && args[0].ToLowerInvariant() == "analyze" && !string.IsNullOrEmpty(args[1]) && args[0].StartsWith("--", StringComparison.Ordinal) == false)
            {
                var cliAnalyzer = new CliAnalyzer();
                cliAnalyzer.Analyze(args[1]);
                return;
            }
            
            // 4. Команды: start, stop, status, help — завершают после выполнения
            if (args.Length == 1 && !string.IsNullOrEmpty(args[0]) && args[0].StartsWith("--", StringComparison.Ordinal) == false && args[0].ToLowerInvariant() != "analyze")
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
                            await StartTraining(args[1]);
                        }
                        break;

                    case "stop":
                        await StopTraining();
                        Console.WriteLine("[INFO] Процесс остановлен.");
                        break;

                    case "status":
                        PrintEnvironmentInfo();
                        Console.WriteLine($"Текущий статус: {(GetTrainer().IsRunning ? "ЗАПУЩЕН" : "ОСТАНОВЛЕН")}");
                        break;

                    case "help":
                    case "-h":
                        PrintHelp();
                        break;

                    default:
                        Console.WriteLine("Неизвестная команда. Введите 'start', 'stop', 'status' или 'help'.");
                        break;
                }
                
                // После обработки команды завершаем
                return;
            }
            
            // 5. Интерактивный режим (пустой запуск — запускается явно пользователем)
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
                <лог>                     Автоматически анализировать CSV-лог (завершается после анализа)
                analyze <лог>             Анализировать CSV-лог с явной командой
                start <сеть> [партии]     Запустить обучение (A/B/C/D/E, по умолчанию 20000 партий)
                stop                       Остановить процесс обучения
                status                     Показать текущий статус и информацию об окружении
                help / -h                  Показать эту справку

              Тестирование:
                --test                     Запустить тесты CsvLogParser

              Примеры:
                dotnet run start D 20000
                dotnet run analyze logs/baseline_d_20000.txt
                dotnet run tests\baseline_d_permuted.txt
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

                    Console.Write("\nКоманда (start/stop/status/help): ");

                    var input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input)) continue;

                    var command = input.Trim().ToLowerInvariant();

                    switch (command)
                    {
                        case "start":
                            await StartTraining("D");
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
                            Console.WriteLine("Неизвестная команда. Введите 'start', 'stop', 'status' или 'help'.");
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

        static async Task StartTraining(string network = "D")
        {
            var trainer = new TrainerController();
            await trainer.StartTrainingAsync(network, 20000);
            Console.WriteLine("\nОбучение запущено! Статус проверяйте командой 'status'.");
        }

        static async Task StopTraining()
        {
            var trainer = new TrainerController();
            await trainer.StopTrainingAsync();
        }

        static TrainerController GetTrainer()
        {
            return new TrainerController();
        }
    }
}