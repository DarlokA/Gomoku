using System;
using System.Collections.Generic;
using System.Globalization;
using GomokuGame.Trainer.MCP.LogAnalyzers;

namespace GomokuGame.Trainer.MCP.LogAnalyzers
{
    /// <summary>
    /// Утилита для анализа CSV-логов без интерактивности.
    /// Тестовые сценарии перемещены в TestRunner.cs
    /// </summary>
    public sealed class CliAnalyzer
    {
        private readonly CultureInfo _ci = CultureInfo.InvariantCulture;
        private readonly CsvLogParser _parser = new();

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