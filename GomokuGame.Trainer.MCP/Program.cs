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
            
            if (args.Length == 0)
            {
                await InteractiveMode();
            }
            else
            {
                // Если один аргумент - это путь к CSV-логу, анализируем его
                if (args.Length == 1 && !string.IsNullOrEmpty(args[0]))
                {
                    var cliAnalyzer = new CliAnalyzer();
                    cliAnalyzer.Analyze(args[0]);
                }
                else
                {
                    // Командная строка с явными командами
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
                                string network = args[1];
                                int gamesCount = 20000;

                                if (args.Length >= 3)
                                {
                                    var thirdArg = args[2];

                                    if (int.TryParse(thirdArg, out int parseGames))
                                    {
                                        gamesCount = parseGames;
                                    }
                                    else if (thirdArg.Contains("games="))
                                    {
                                        var parts = thirdArg.Split('=');
                                        if (parts.Length == 2 && int.TryParse(parts[1], out int games))
                                        {
                                            gamesCount = games;
                                        }
                                    }
                                }

                                var trainerPath = ConfigurationLoader.GetTrainerPath();
                                Console.WriteLine($"[INFO] Путь к тренеру: {trainerPath}");

                                if (!File.Exists(trainerPath))
                                {
                                    Console.WriteLine($"[ERROR] Файл тренера не найден: {trainerPath}");
                                    return;
                                }

                                var trainer = new TrainerController();
                                await trainer.StartTrainingAsync(network, gamesCount, saveName: null, seed: 42);
                            }
                            break;

                        case "stop":
                            {
                                var trainer = new TrainerController();
                                await trainer.StopTrainingAsync();
                            }
                            break;

                        case "status":
                        default:
                            {
                                var trainer = new TrainerController();
                                PrintEnvironmentInfo();
                                Console.WriteLine($"Текущий статус: {(trainer.IsRunning ? "ЗАПУЩЕН" : "ОСТАНОВЛЕН")}");
                                Console.Write("\nКоманда (start/stop/status/help): ");
                            }
                            break;
                    }
                }
            }
        }

        static void PrintHelp()
        {
            string helpText = @"
              Gomoku Trainer MCP - CLI для управления обучением и анализа логов

              Команды:
                <лог>                     Автоматически анализировать CSV-лог
                start <сеть> [партии]    Запустить обучение (A/B/C/D/E)
                stop                      Остановить процесс обучения
                status                    Показать текущий статус
                help                      Показать эту справку

              Примеры:
                dotnet run start D 20000
                dotnet run analyze logs/baseline_d_20000.txt
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
                        case "analyze":
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
    }
}
