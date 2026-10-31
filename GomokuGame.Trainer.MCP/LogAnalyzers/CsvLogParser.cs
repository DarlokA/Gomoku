using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GomokuGame.Trainer.MCP.LogAnalyzers
{
    /// <summary>
    /// Парсит CSV-логи обучения сети. Читает колонки по заголовкам (независимо от порядка).
    /// </summary>
    public sealed class CsvLogParser
    {
        private readonly CultureInfo _ci = CultureInfo.InvariantCulture;
        
        // Обязательные колонки для первой записи (warmup)
        private static readonly string[] RequiredColumns = new[] { "Done", "X", "O", "Draw", "Loss", "Missed" };

        /// <summary>
        /// Парсит CSV-лог и возвращает список записей.
        /// </summary>
        public List<TrainingRecord> Parse(string logPath)
        {
            if (!File.Exists(logPath)) throw new FileNotFoundException($"Лог не найден: {logPath}");
            
            var records = new List<TrainingRecord>();
            using var reader = new StreamReader(logPath);
            
            // Читаем заголовок первой строки
            var headerLine = reader.ReadLine();
            if (headerLine == null) throw new InvalidOperationException("Файл лога пуст");
            
            // Парсим заголовок и создаём словарь «название → индекс»
            var headers = ParseHeader(headerLine);
            if (headers.Count == 0) throw new InvalidOperationException("Не удалось прочитать заголовок CSV");
            
            // Проверяем наличие всех обязательных колонок
            foreach (var col in RequiredColumns)
            {
                if (!headers.ContainsKey(col))
                    throw new MissingFieldException($"Отсутствует обязательная колонка: {col}");
            }
            
            var lines = reader.ReadToEnd();
            var recordLines = lines.Split('\n', '\r')
                                  .Select(l => l.Trim())
                                  .Where(l => !string.IsNullOrWhiteSpace(l));

            int lineNum = 1; // Первая строка уже прочитана (заголовок)
            
            foreach (var line in recordLines)
            {
                lineNum++;
                var fields = ParseLine(line, headers);
                
                // Проверка: количество полей должно совпадать с количеством колонок в заголовке
                if (fields.Count != headers.Count)
                {
                    throw new FieldCountMismatchException(
                        $"Строка {lineNum}: ожидается {headers.Count} полей, но найдено {fields.Count}")
                    { LineNumber = lineNum, MissingColumn = GetMissingColumn(fields, headers), ExtraColumns = GetExtraColumns(fields, headers) };
                }
                
                string? failedFieldName = null;
                try
                {
                    var record = new TrainingRecord
                    {
                        Done = long.Parse(fields["Done"], _ci),
                        X = int.Parse(fields["X"], _ci),
                        O = int.Parse(fields["O"], _ci),
                        Draw = int.Parse(fields["Draw"], _ci),
                        Loss = double.Parse(fields["Loss"], _ci),
                        Missed = int.Parse(fields["Missed"], _ci)
                    };

                    records.Add(record);

                    // Проверка на некорректные данные
                    if (record.Done != record.X + record.O + record.Draw)
                    {
                        throw new InvalidFieldException($"Строка {lineNum}: сумма X+O+Draw ({record.X}+{record.O}+{record.Draw} = {record.X + record.O + record.Draw}) не совпадает с Done ({record.Done})")
                        { LineNumber = lineNum, FieldName = "Done" };
                    }
                }
                catch (FormatException ex)
                {
                    // Определяем имя колонки где произошла ошибка
                    var fieldNames = new[] { "Done", "X", "O", "Draw", "Loss", "Missed" };
                    foreach (var fieldName in fieldNames)
                    {
                        try
                        {
                            if (fieldName == "Done") long.Parse(fields[fieldName], _ci);
                            else if (fieldName == "X") int.Parse(fields[fieldName]);
                            else if (fieldName == "O") int.Parse(fields[fieldName]);
                            else if (fieldName == "Draw") int.Parse(fields[fieldName]);
                            else if (fieldName == "Loss") double.Parse(fields[fieldName], _ci);
                            else if (fieldName == "Missed") int.Parse(fields[fieldName]);
                        }
                        catch (FormatException)
                        {
                            failedFieldName = fieldName;
                            break;
                        }
                    }
                    
                    throw new InvalidFieldException($"Строка {lineNum}, поле '{failedFieldName}' имеет неверный формат: {ex.Message}")
                    { LineNumber = lineNum };
                }
            }

            return records;
        }

        /// <summary>
        /// Парсит строку заголовка CSV и возвращает словарь «название → индекс».
        /// </summary>
        private Dictionary<string, int> ParseHeader(string headerLine)
        {
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var fields = headerLine.Split(';');
            
            foreach (var field in fields)
            {
                var trimmed = field.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    headers[trimmed] = headers.Count;
            }

            return headers;
        }

        /// <summary>
        /// Парсит строку данных CSV по названиям колонок из заголовка.
        /// </summary>
        private Dictionary<string, string> ParseLine(string line, Dictionary<string, int> headers)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var values = line.Split(';');
            
            // Проверка: количество значений должно совпадать с количеством колонок в заголовке
            if (values.Length != headers.Count)
            {
                throw new FieldCountMismatchException(
                    $"Строка данных содержит {values.Length} значений, но ожидается {headers.Count}"
                    )
                {
                    LineNumber = 2,
                    ExtraColumns = values.Skip(headers.Count).Select((v, i) => v.Trim()).ToList()
                };
            }

            // Сохраняем значения по индексам из заголовка
            foreach (var kvp in headers)
            {
                if (kvp.Value < values.Length)
                    fields[kvp.Key] = values[kvp.Value].Trim();
            }

            return fields;
        }

        /// <summary>
        /// Определяет, какая колонка отсутствует из обязательных.
        /// </summary>
        private string GetMissingColumn(Dictionary<string, string> fields, Dictionary<string, int> headers)
        {
            var missingColumns = RequiredColumns.Where(c => !fields.ContainsKey(c)).ToList();
            return missingColumns.Count == 1 ? missingColumns[0] : "несколько";
        }

        /// <summary>
        /// Определяет лишние колонки, которых нет в списке обязательных.
        /// </summary>
        private List<string> GetExtraColumns(Dictionary<string, string> fields, Dictionary<string, int> headers)
        {
            var allRequired = new HashSet<string>(RequiredColumns, StringComparer.OrdinalIgnoreCase);
            var extra = fields.Keys.Where(k => !allRequired.Contains(k)).ToList();
            return extra;
        }

        private double GetMissedWinsValue(TrainingRecord record) => record.Missed > 0 ? record.Missed : 0.0;
    }

    /// <summary>
    /// Запись чанка обучения (Warmup: первые 500 партий).
    /// </summary>
    public sealed class TrainingRecord
    {
        /// <summary>Накопительное количество сыгранных партий.</summary>
        public long Done { get; set; }

        /// <summary>Количество побед X (для себя).</summary>
        public int X { get; set; }

        /// <summary>Количество побед O (для противника).</summary>
        public int O { get; set; }

        /// <summary>Количество ничьих.</summary>
        public int Draw { get; set; }

        /// <summary>Ошибка модели.</summary>
        public double Loss { get; set; }

        /// <summary>Количество пропущенных побед (missed wins).</summary>
        public int Missed { get; set; }

        /// <summary>Вычисляет winrate в процентах.</summary>
        public float WinRate => Done > 0 ? (float)(X * 100.0 / Done) : 0f;

        /// <summary>Всего сыгранных партий (alias).</summary>
        public long TotalGamesPlayed => Done;
    }
}