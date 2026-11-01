using System;
using System.Collections.Generic;
using System.IO;
using GomokuGame.Trainer.MCP.LogAnalyzers;

namespace GomokuGame.Trainer.MCP.LogAnalyzers
{
    /// <summary>
    /// Центральное управление для всех тестовых сценариев CsvLogParser.
    /// </summary>
    public sealed class TestRunner
    {
        // Пути к тестовым файлам
        private static readonly string[] TestFilePaths = new[]
        {
            @"C:\Users\Darlo.DARLOKA\source\repos\Gomoku\GomokuGame.Trainer.MCP\tests\baseline_d_20000_permuted.txt",  // Перестановка колонок
            @"C:\Users\Darlo.DARLOKA\source\repos\Gomoku\GomokuGame.Trainer.MCP\tests\baseline_d_missing_missed.txt",    // Отсутствие колонки
            @"C:\Users\Darlo.DARLOKA\source\repos\Gomoku\GomokuGame.Trainer.MCP\tests\baseline_d_extra_field.txt",      // Лишнее поле
            @"C:\Users\Darlo.DARLOKA\source\repos\Gomoku\GomokuGame.Trainer.MCP\tests\baseline_d_invalid_value.txt"     // Неверный формат
        };

        private static readonly CsvLogParser _parser = new();

        /// <summary>
        /// Запускает все тесты.
        /// </summary>
        public static void RunAllTests()
        {
            Console.WriteLine("=== Тестирование CsvLogParser ===\n");
            
            RunPermutationTest();
            RunMissingColumnTest();
            RunExtraFieldTest();
            RunInvalidFormatTest();
            
            Console.WriteLine("\n✓ Все тесты завершены!");
        }

        /// <summary>
        /// Тест: перестановка колонок.
        /// </summary>
        public static void RunPermutationTest()
        {
            Console.WriteLine("--- Тест 1: Перестановка колонок ---");
            
            try
            {
                var records = _parser.Parse(TestFilePaths[0]);
                
                if (records.Count > 0)
                {
                    var firstRecord = records[0];
                    
                    Console.WriteLine($"  ✓ Парсинг прошёл успешно!");
                    Console.WriteLine($"    Done:   {firstRecord.Done:N0} партий");
                    Console.WriteLine($"    X:      {firstRecord.X:N0} побед за себя");
                    Console.WriteLine($"    O:      {firstRecord.O:N0} побед противника");
                    Console.WriteLine($"    Draw:   {firstRecord.Draw:N0} ничьих");
                    Console.WriteLine($"    Loss:   {firstRecord.Loss:F4}");
                    Console.WriteLine($"    Missed: {firstRecord.Missed:N0} пропущенных побед");
                    
                    // Проверка ожидаемых значений
                    if (firstRecord.Done == 500 && firstRecord.X == 303 && firstRecord.O == 197)
                    {
                        Console.WriteLine("  ✓ Перестановка колонок обработана корректно!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Ошибка при перестановке: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Тест: отсутствие обязательной колонки.
        /// </summary>
        public static void RunMissingColumnTest()
        {
            Console.WriteLine("--- Тест 2: Отсутствие обязательной колонки 'Missed' ---");
            
            try
            {
                var records = _parser.Parse(TestFilePaths[1]);
                Console.WriteLine($"  ✗ Ожидалось исключение, но парсинг прошёл!");
            }
            catch (MissingFieldException ex)
            {
                Console.WriteLine("  ✓ ✓ ✓ Механизм работы:");
                Console.WriteLine($"    Тип исключения: MissingFieldException");
                Console.WriteLine($"    Сообщение: {ex.Message}");
                Console.WriteLine("  ✓ Отсутствующая колонка 'Missed' корректно обнаружена!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Ошибка: {ex.GetType().Name}: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Тест: наличие лишнего поля.
        /// </summary>
        public static void RunExtraFieldTest()
        {
            Console.WriteLine("--- Тест 3: Наличие лишнего поля 'ExtraColumn' ---");
            
            try
            {
                var records = _parser.Parse(TestFilePaths[2]);
                Console.WriteLine($"  ✗ Ожидалось исключение, но парсинг прошёл!");
            }
            catch (FieldCountMismatchException ex)
            {
                Console.WriteLine("  ✓ ✓ ✓ Механизм работы:");
                Console.WriteLine($"    Тип исключения: FieldCountMismatchException");
                Console.WriteLine($"    Сообщение: {ex.Message}");
                if (ex.ExtraColumns != null && ex.ExtraColumns.Count > 0)
                {
                    Console.WriteLine($"    Лишние поля: {string.Join(", ", ex.ExtraColumns)}");
                }
                Console.WriteLine("  ✓ Проверка количества полей работает корректно!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Ошибка: {ex.GetType().Name}: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Тест: неверное числовое значение.
        /// </summary>
        public static void RunInvalidFormatTest()
        {
            Console.WriteLine("--- Тест 4: Неверное числовое значение в поле 'Missed' ---");
            
            try
            {
                var records = _parser.Parse(TestFilePaths[3]);
                Console.WriteLine($"  ✗ Ожидалось исключение, но парсинг прошёл!");
            }
            catch (InvalidFieldException ex)
            {
                Console.WriteLine("  ✓ ✓ ✓ Механизм работы:");
                Console.WriteLine($"    Тип исключения: InvalidFieldException");
                Console.WriteLine($"    Сообщение: {ex.Message}");
                Console.WriteLine($"    Номер строки с ошибкой: {ex.LineNumber}");
                Console.WriteLine("  ✓ Проверка формата числового значения работает корректно!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Ошибка: {ex.GetType().Name}: {ex.Message}");
            }

            Console.WriteLine();
        }
    }
}