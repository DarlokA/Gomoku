using GomokuGame.Trainer.MCP.Models;
using GomokuGame.Trainer.MCP.Services;

namespace GomokuGame.Trainer.MCP
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Gomoku Trainer MCP ===");
            Console.WriteLine();

            // Если нет аргументов — интерактивный режим
            if (args.Length == 0)
            {
                await InteractiveMode();
            }
            else
            {
                // Командная строка
                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "start":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Ошибка: Укажите сеть (A/B/C/D/E)");
                            Console.WriteLine("Пример: start D 20000 --seed=42");
                        }
                        else
                        {
                            var trainer = new TrainerController();
                            await trainer.StartTrainingAsync(args[1], 20000);
                        }
                        break;

                    case "stop":
                        {
                            var trainer = new TrainerController();
                            await trainer.StopTrainingAsync();
                        }
                        break;

                    case "status":
                        {
                            var trainer = new TrainerController();
                            await PrintStatus(trainer);
                            Console.Write("\nКоманда (start/stop/status/help): ");
                        }
                        break;

                    case "help":
                    case "--help":
                    case "-h":
                        await PrintHelp();
                        Console.Write("Команда (start/stop/status/help): ");
                        break;

                    default:
                        Console.WriteLine($"Неизвестная команда: {command}");
                        Console.WriteLine("Введите 'help' для списка команд.");
                        Console.Write("\nКоманда (start/stop/status/help): ");
                        break;
                }
            }
        }

        static async Task PrintHelp()
        {
            Console.WriteLine($"""
              Gomoku Trainer MCP — CLI для управления обучением

              Команды:
                start <сеть> [партии]    Запустить обучение (A/B/C/D/E)
                stop                      Остановить процесс обучения
                status                    Показать текущий статус
                help                      Показать эту справку

              Примеры:
                dotnet run start D 20000
                dotnet run start E --games=0 --seed=42
                dotnet run status

            """);
        }

        static async Task PrintStatus(TrainerController trainer)
        {
            // Показываем статус без блокирующего чтения всего вывода
            Console.WriteLine($"Текущий статус: {(trainer.IsRunning ? "ЗАПУЩЕН" : "ОСТАНОВЛЕН")}");

            if (trainer.IsRunning)
            {
                var state = await trainer.GetStatusAsync();
                
                Console.WriteLine($"Прогресс: {state.ProgressPercent:F2}%");
                Console.WriteLine($"Batch: {state.CurrentBatch}/{state.TotalBatches}");
                if (state.LastLoss.HasValue)
                    Console.WriteLine($"Loss: {state.LastLoss:F4}");
                if (state.LastWinRate.HasValue)
                    Console.WriteLine($"Winrate: {state.LastWinRate:F2}%");
            }
        }

        static async Task InteractiveMode()
        {
            Console.WriteLine("=== Интерактивный режим ===\n");

            while (true)
            {
                try
                {
                    var trainer = new TrainerController();
                    
                    // Показываем статус без блокировки вывода
                    await PrintStatus(trainer);

                    Console.Write("\nКоманда (start/stop/status/help): ");
                    
                    var input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input)) continue;

                    var command = input.Trim().ToLowerInvariant();
                    
                    switch (command)
                    {
                        case "start":
                            await StartTraining("D"); // Дефолтная сеть D
                            break;
                        
                        case "stop":
                            await trainer.StopTrainingAsync();
                            Console.WriteLine("[INFO] Процесс остановлен.");
                            break;

                        case "status":
                            await PrintStatus(trainer);
                            break;

                        case "help":
                        case "-h":
                            await PrintHelp();
                            break;

                        default:
                            Console.WriteLine($"Неизвестная команда. Введите 'start', 'stop', 'status' или 'help'.");
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
            // Не возвращаем в цикл, ждём пользователя
        }
    }
}