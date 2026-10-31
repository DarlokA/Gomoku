using System;
using System.Collections.Generic;

namespace GomokuGame.Trainer.MCP.LogAnalyzers
{
    /// <summary>
    /// Исключение для отсутствующей обязательной колонки в CSV.
    /// </summary>
    public sealed class MissingFieldException : Exception
    {
        public MissingFieldException(string message) : base(message) { }
    }

    /// <summary>
    /// Исключение для несовпадения количества полей в строке данных и заголовке.
    /// </summary>
    public sealed class FieldCountMismatchException : Exception
    {
        public int LineNumber { get; set; }
        public string MissingColumn { get; set; }
        public List<string> ExtraColumns { get; set; }

        public FieldCountMismatchException(string message) 
            : base(message)
        {
            // Поля будут установлены после парсинга заголовка
        }

        internal FieldCountMismatchException(string message, int lineNum, string missingColumn, List<string> extraColumns)
            : this($"{message} (строка {lineNum})")
        {
            LineNumber = lineNum;
            MissingColumn = missingColumn ?? "неизвестно";
            ExtraColumns = extraColumns ?? new List<string>();
        }
    }

    /// <summary>
    /// Исключение для неверного формата числового поля.
    /// </summary>
    public sealed class InvalidFieldException : Exception
    {
        public int LineNumber { get; set; }
        public string FieldName { get; set; }

        public InvalidFieldException(string message) : base(message) { }

        internal InvalidFieldException(string message, int lineNum)
            : this($"{message} (строка {lineNum})")
        {
            LineNumber = lineNum;
        }

        internal InvalidFieldException(string message, int lineNum, string fieldName)
            : this($"{message} (строка {lineNum}, поле '{fieldName}')")
        {
            LineNumber = lineNum;
            FieldName = fieldName;
        }
    }
}