using System;
using System.Collections.Generic;
using System.Globalization;
using GomokuGame.Trainer.MCP.LogAnalyzers;

namespace GomokuGame.Trainer.MCP.LogAnalyzers
{
    /// <summary>
    /// Утилита для анализа CSV-логов без интерактивности.
    /// </summary>
    public sealed class CliAnalyzer
    {
        private readonly CultureInfo _ci = CultureInfo.InvariantCulture;
        private readonly CsvLogParser _parser = new();

        /// <summary>
        /// Результаты проверок первой записи.
        /// </summary>
        public record ValidationResult(
            bool Success,
            string? ErrorMessage = null,
            long Done = -1, int X = -1, int O = -1, 
            int Draw = -1, double Loss = -1, int Missed = -1,
            double WinRate = -1)
        {
            public override string ToString()
            {
                if (!Success && ErrorMessage != null)
                    return $"❌ ОШИБКА: {ErrorMessage}";

                return $"✅ УСПЕХ: Done={Done:N0}, X={X:N0}, O={O:N0}, Draw={Draw:N0}, Loss={Loss:F4}, Missed={Missed:N0}, WinRate={WinRate:F2}%";
            }
        }

        /// <summary>
        /// Анализирует лог и выводит результаты в формате, удобном для консольного вывода.
        /// </summary>
        public void Analyze(string logPath)
        {
            Console.WriteLine($"=== Анализ лога: {logPath} ===\n");

            var records = _parser.Parse(logPath);
            
            if (records.Count == 0)
            {
                Console.WriteLine("[INFO] Лог пуст, ничего не найдено.");
                return;
            }

            // Показываем первую запись (warmup batch)
            var firstRecord = records[0];
            PrintFirstRecord(firstRecord);

            // Валидация и проверка перестановки колонок
            Console.WriteLine("\n=== Валидация ===");
            var validationResult = ValidateFirstRecord(firstRecord, records.Count);
            
            if (!validationResult.Success)
            {
                Console.WriteLine(validationResult.ErrorMessage);
                return;
            }

            // Показываем сводку по всему логу
            PrintSummary(records);
        }

        /// <summary>
        /// Выводит информацию о первой записи (первый чанк обучения).
        /// </summary>
        private void PrintFirstRecord(TrainingRecord record)
        {
            Console.WriteLine("--- Первая запись (warmup batch) ---");
            Console.WriteLine($"Done:   {record.Done:N0} партий");
            Console.WriteLine($"X:      {record.X:N0} побед за себя");
            Console.WriteLine($"O:      {record.O:N0} побед противника");
            Console.WriteLine($"Draw:   {record.Draw:N0} ничьих");
            Console.WriteLine($"Loss:   {record.Loss:F4}");
            Console.WriteLine($"Missed: {record.Missed:N0} пропущенных побед");
            Console.WriteLine($"WinRate: {record.WinRate:F2}% ((X*100)/Done)");
        }

        /// <summary>
        /// Проверяет и выводит результаты валидации первой записи.
        /// </summary>
        private ValidationResult ValidateFirstRecord(TrainingRecord firstRecord, int totalChunks)
        {
            // Проверка значений
            var expectedDone = 500L;
            var expectedX = 303;
            var expectedO = 197;
            var expectedDraw = 0;
            var expectedLoss = 0.0792;
            var expectedMissed = 1378;

            // Проверка Done
            if (firstRecord.Done != expectedDone)
            {
                return new ValidationResult(
                    false, 
                    $"Done: {firstRecord.Done:N0} != {expectedDone:N0}",
                    firstRecord.Done, firstRecord.X, firstRecord.O, firstRecord.Draw, firstRecord.Loss, firstRecord.Missed, firstRecord.WinRate);
            }

            // Проверка X
            if (firstRecord.X != expectedX)
            {
                return new ValidationResult(
                    false, 
                    $"X: {firstRecord.X:N0} != {expectedX:N0}",
                    firstRecord.Done, firstRecord.X, firstRecord.O, firstRecord.Draw, firstRecord.Loss, firstRecord.Missed, firstRecord.WinRate);
            }

            // Проверка O
            if (firstRecord.O != expectedO)
            {
                return new ValidationResult(
                    false, 
                    $"O: {firstRecord.O:N0} != {expectedO:N0}",
                    firstRecord.Done, firstRecord.X, firstRecord.O, firstRecord.Draw, firstRecord.Loss, firstRecord.Missed, firstRecord.WinRate);
            }

            // Проверка Draw
            if (firstRecord.Draw != expectedDraw)
            {
                return new ValidationResult(
                    false, 
                    $"Draw: {firstRecord.Draw:N0} != {expectedDraw:N0}",
                    firstRecord.Done, firstRecord.X, firstRecord.O, firstRecord.Draw, firstRecord.Loss, firstRecord.Missed, firstRecord.WinRate);
            }

            // Проверка Loss
            if (Math.Abs(firstRecord.Loss - expectedLoss) > 0.0001) // допускаем небольшую погрешность при округлении
            {
                return new ValidationResult(
                    false, 
                    $"Loss: {firstRecord.Loss:F4} != {expectedLoss:F4}",
                    firstRecord.Done, firstRecord.X, firstRecord.O, firstRecord.Draw, firstRecord.Loss, firstRecord.Missed, firstRecord.WinRate);
            }

            // Проверка Missed
            if (firstRecord.Missed != expectedMissed)
            {
                return new ValidationResult(
                    false, 
                    $"Missed: {firstRecord.Missed:N0} != {expectedMissed:N0}",
                    firstRecord.Done, firstRecord.X, firstRecord.O, firstRecord.Draw, firstRecord.Loss, firstRecord.Missed, firstRecord.WinRate);
            }

            // Проверка WinRate
            var expectedWinRate = (double)expectedX / expectedDone * 100;
            if (Math.Abs(firstRecord.WinRate - expectedWinRate) > 0.01)
            {
                return new ValidationResult(
                    false, 
                    $"WinRate: {firstRecord.WinRate:F2}% != {expectedWinRate:F2}%",
                    firstRecord.Done, firstRecord.X, firstRecord.O, firstRecord.Draw, firstRecord.Loss, firstRecord.Missed, firstRecord.WinRate);
            }

            // Проверка суммы X+O+Draw = Done
            if (firstRecord.X + firstRecord.O + firstRecord.Draw != firstRecord.Done)
            {
                return new ValidationResult(
                    false, 
                    $"Сумма: X({firstRecord.X})+O({firstRecord.O})+Draw({firstRecord.Draw})={firstRecord.X+firstRecord.O+firstRecord.Draw} != Done({firstRecord.Done})",
                    firstRecord.Done, firstRecord.X, firstRecord.O, firstRecord.Draw, firstRecord.Loss, firstRecord.Missed, firstRecord.WinRate);
            }

            // Вывод всех проверок
            Console.WriteLine($"Done:   {firstRecord.Done:N0} == {expectedDone:N0} -> ✅");
            Console.WriteLine($"X:      {firstRecord.X:N0} == {expectedX:N0} -> ✅");
            Console.WriteLine($"O:      {firstRecord.O:N0} == {expectedO:N0} -> ✅");
            Console.WriteLine($"Draw:   {firstRecord.Draw:N0} == {expectedDraw:N0} -> ✅");
            Console.WriteLine($"Loss:   {firstRecord.Loss:F4} == {expectedLoss:F4} -> ✅");
            Console.WriteLine($"Missed: {firstRecord.Missed:N0} == {expectedMissed:N0} -> ✅");
            Console.WriteLine($"WinRate: {firstRecord.WinRate:F2}% (ожидаемо: {expectedWinRate:F2}%) -> ✅");
            Console.WriteLine($"Сумма: X+O+Draw = {firstRecord.X}+{firstRecord.O}+{firstRecord.Draw} = {firstRecord.X+firstRecord.O+firstRecord.Draw} == Done({firstRecord.Done}) -> ✅");

            return new ValidationResult(
                true,
                null,
                firstRecord.Done, firstRecord.X, firstRecord.O, 
                firstRecord.Draw, firstRecord.Loss, firstRecord.Missed, firstRecord.WinRate);
        }

        /// <summary>
        /// Выводит сводную статистику по всему логу.
        /// </summary>
        private void PrintSummary(List<TrainingRecord> records)
        {
            Console.WriteLine("\n--- Сводная статистика ---");
            
            long totalDone = 0;
            int totalX = 0;
            int totalO = 0;
            int totalDraw = 0;
            double totalLossSum = 0;
            int totalMissed = 0;

            foreach (var record in records)
            {
                totalDone += record.Done;
                totalX += record.X;
                totalO += record.O;
                totalDraw += record.Draw;
                totalLossSum += record.Loss;
                totalMissed += record.Missed;
            }

            Console.WriteLine($"Всего партий: {totalDone:N0}");
            Console.WriteLine($"Победы X:     {totalX:N0}");
            Console.WriteLine($"Победы O:     {totalO:N0}");
            Console.WriteLine($"Ничьих:       {totalDraw:N0}");
            Console.WriteLine($"Средний Loss: {(totalDone > 0 ? totalLossSum / records.Count : 0.0):F4}");
            Console.WriteLine($"Средние Missed Wins: {(totalMissed > 0 ? totalMissed / records.Count : 0.0):N0}");
            
            // Показываем последнюю запись для сравнения
            var lastRecord = records[records.Count - 1];
            Console.WriteLine($"\n--- Последняя запись ---");
            Console.WriteLine($"Done:   {lastRecord.Done:N0} партий");
            Console.WriteLine($"Loss:   {lastRecord.Loss:F4}");
            Console.WriteLine($"WinRate: {lastRecord.WinRate:F2}%");
        }
    }
}