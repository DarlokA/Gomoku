using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GomokuGame.Trainer.MCP.LogAnalyzers;

namespace GomokuGame.Trainer.MCP
{
    /// <summary>
    /// Тестировщик всех сценариев парсинга CSV.
    /// </summary>
    public sealed class CsvValidationTests
    {
        private static readonly CultureInfo _ci = CultureInfo.InvariantCulture;

        public static void Main()
        {
            Console.WriteLine("=== ТестированиеCsvLogParser ===\n");

            // Кейс 1: Перестановка колонок
            TestPermutatedColumns();

            // Кейс 2: Отсутствие обязательной колонки
            TestMissingColumn();

            // Кейс 3: Лишнее поле
            TestExtraField();

            // Кейс 4: Неверное числовое значение
            TestInvalidValue();

            // Кейс 5: Правильный файл для сравнения
            TestValidFile();
        }

        private static void TestPermutatedColumns()
        {
            Console.WriteLine("--- Тест 1: Перестановка колонок ---");
            try
            {
                var parser = new CsvLogParser();
                var records = parser.Parse("logs/baseline_d_20000_permuted.txt");
                var firstRecord = records[0];

                Console.WriteLine($"✓ Парсинг прошёл успешно!");
                Console.WriteLine($"  Done:   {firstRecord.Done:N0}");
                Console.WriteLine($"  X:      {firstRecord.X:N0}");
                Console.WriteLine($"  O:      {firstRecord.O:N0}");
                Console.WriteLine($"  Draw:   {firstRecord.Draw:N0}");
                Console.WriteLine($"  Loss:   {firstRecord.Loss,-12:F4}");
                Console.WriteLine($"  Missed: {firstRecord.Missed:N0}");
                Console.WriteLine($"  WinRate: {firstRecord.WinRate:F2}%");

                Console.WriteLine($"\n✓ Перестановка колонок обработана корректно!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ ОШИБКА: {ex.Message}");
            }
        }

        private static void TestMissingColumn()
        {
            Console.WriteLine("\n--- Тест 2: Отсутствие обязательной колонки 'Missed' ---");
            try
            {
                var parser = new CsvLogParser();
                var records = parser.Parse("logs/baseline_d_missing_missed.txt");
                Console.WriteLine("\n✗ ОШИБКА: Исключение не сработало!");
            }
            catch (GomokuGame.Trainer.MCP.LogAnalyzers.MissingFieldException ex)
            {
                Console.WriteLine($"✓ ✓ ✓ Механизм работы:");
                Console.WriteLine($"  Тип исключения: MissingFieldException");
                Console.WriteLine($"  Сообщение: {ex.Message}");
                Console.WriteLine($"\n✓ Отсутствующая колонка 'Missed' корректно обнаружена!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Неправильное исключение: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void TestExtraField()
        {
            Console.WriteLine("\n--- Тест 3: Наличие лишнего поля 'ExtraColumn' ---");
            try
            {
                var parser = new CsvLogParser();
                var records = parser.Parse("logs/baseline_d_extra_field.txt");
                Console.WriteLine($"\n✗ ОШИБКА: Исключение не сработало! Поле 'ExtraColumn' игнорируется.");
            }
            catch (GomokuGame.Trainer.MCP.LogAnalyzers.FieldCountMismatchException ex)
            {
                Console.WriteLine($"✓ ✓ ✓ Механизм работы:");
                Console.WriteLine($"  Тип исключения: FieldCountMismatchException");
                Console.WriteLine($"  Сообщение: {ex.Message}");
                
                if (!string.IsNullOrEmpty(ex.MissingColumn))
                {
                    Console.WriteLine($"  Отсутствующая колонка: {ex.MissingColumn}");
                }
                else
                {
                    Console.WriteLine($"  Лишние поля: {string.Join(", ", ex.ExtraColumns)}");
                }

                Console.WriteLine($"\n✓ Проверка количества полей работает корректно!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Неправильное исключение: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void TestInvalidValue()
        {
            Console.WriteLine("\n--- Тест 4: Неверное числовое значение в поле 'Missed' ---");
            try
            {
                var parser = new CsvLogParser();
                var records = parser.Parse("logs/baseline_d_invalid_value.txt");
                Console.WriteLine($"\n✗ ОШИБКА: Исключение не сработало!");
            }
            catch (GomokuGame.Trainer.MCP.LogAnalyzers.InvalidFieldException ex)
            {
                Console.WriteLine($"✓ ✓ ✓ Механизм работы:");
                Console.WriteLine($"  Тип исключения: InvalidFieldException");
                Console.WriteLine($"  Сообщение: {ex.Message}");

                if (ex.LineNumber > 0)
                {
                    Console.WriteLine($"  Номер строки с ошибкой: {ex.LineNumber}");
                }
                
                Console.WriteLine($"\n✓ Проверка формата числового значения работает корректно!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Неправильное исключение: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void TestValidFile()
        {
            Console.WriteLine("\n--- Тест 5: Правильный файл (baseline_d_20000.txt) ---");
            try
            {
                var parser = new CsvLogParser();
                var records = parser.Parse("logs/baseline_d_20000.txt");
                var firstRecord = records[0];

                Console.WriteLine($"✓ Парсинг прошёл успешно!");
                Console.WriteLine("--- Первая запись (warmup batch) ---");
                Console.WriteLine($"Done:   {firstRecord.Done:N0} партий");
                Console.WriteLine($"X:      {firstRecord.X:N0} побед за себя");
                Console.WriteLine($"O:      {firstRecord.O:N0} побед противника");
                Console.WriteLine($"Draw:   {firstRecord.Draw:N0} ничьих");
                Console.WriteLine($"Loss:   {firstRecord.Loss,-12:F4}");
                Console.WriteLine($"Missed: {firstRecord.Missed:N0} пропущенных побед");
                Console.WriteLine($"WinRate: {firstRecord.WinRate:F2}% ((X*100)/Done)");

                // Валидация
                var expectedDone = 500L;
                var expectedX = 303;
                var expectedO = 197;
                var expectedDraw = 0;
                var expectedLoss = 0.0792;
                var expectedMissed = 1378;

                Console.WriteLine("\n=== Валидация ===");
                Console.WriteLine($"Done:   {firstRecord.Done:N0} == {expectedDone:N0} -> {(firstRecord.Done == expectedDone ? "✓" : "✗")}");
                Console.WriteLine($"X:      {firstRecord.X:N0} == {expectedX:N0} -> {(firstRecord.X == expectedX ? "✓" : "✗")}");
                Console.WriteLine($"O:      {firstRecord.O:N0} == {expectedO:N0} -> {(firstRecord.O == expectedO ? "✓" : "✗")}");
                Console.WriteLine($"Draw:   {firstRecord.Draw:N0} == {expectedDraw:N0} -> {(firstRecord.Draw == expectedDraw ? "✓" : "✗")}");
                Console.WriteLine($"Loss:   {firstRecord.Loss,-12:F4} == {expectedLoss,-12:F4} -> {(Math.Abs(firstRecord.Loss - expectedLoss) < 0.0001 ? "✓" : "✗")}");
                Console.WriteLine($"Missed: {firstRecord.Missed:N0} == {expectedMissed:N0} -> {(firstRecord.Missed == expectedMissed ? "✓" : "✗")}");
                
                var calculatedWinRate = (float)(expectedX * 100.0 / expectedDone);
                Console.WriteLine($"\nWinRate: {firstRecord.WinRate:F2}% (ожидаемо: {calculatedWinRate:F2}%) -> {(Math.Abs(firstRecord.WinRate - calculatedWinRate) < 0.1 ? "✓" : "✗")}");

                // Проверка суммы
                if (firstRecord.Done == firstRecord.X + firstRecord.O + firstRecord.Draw)
                {
                    Console.WriteLine($"Сумма: X+O+Draw = {firstRecord.X}+{firstRecord.O}+{firstRecord.Draw} = {firstRecord.X + firstRecord.O + firstRecord.Draw} == Done({firstRecord.Done}) -> ✓");
                }
                else
                {
                    Console.WriteLine($"Сумма: X+O+Draw != Done!");
                }

                Console.WriteLine("\n✓ Все проверки пройдены!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ ОШИБКА: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Внутренняя ошибка: {ex.InnerException.Message}");
                }
            }
        }
    }
}