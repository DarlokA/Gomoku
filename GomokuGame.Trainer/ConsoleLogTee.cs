using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace GomokuGame.Trainer
{
    // ==================== КОНСОЛЬНЫЙ ЛОГ (consolelog:<путь>) ====================

    /// <summary>
    /// Утилиты для переключения потоков консоли с выводом в файл лога.
    /// </summary>
    internal sealed class ConsoleLogTee : IDisposable
    {
        private sealed class TeeTextWriter : TextWriter
        {
            /// <summary>Оригинальный поток консоли (основной).</summary>
            private readonly TextWriter _primary;

            /// <summary>Поток для вывода в файл лога.</summary>
            private readonly StreamWriter _mirror;

            /// <summary>Конструктор: принимает основной поток и writer для файла.</summary>
            public TeeTextWriter(TextWriter primary, StreamWriter mirror)
                : base(primary.Encoding as IFormatProvider ?? CultureInfo.CurrentCulture)
            {
                _primary = primary;
                _mirror = mirror;
            }

            /// <summary>Кодировка — берётся из основного потока.</summary>
            public override Encoding Encoding => _primary.Encoding;

            /// <summary>Запись символа: копируется в основной поток и в файл лога.</summary>
            public override void Write(char value)
            {
                _primary.Write(value);
                _mirror.Write(value);
            }

            /// <summary>Запись строки: копируется в основной поток и в файл лога (пропускает null).</summary>
            public override void Write(string? value)
            {
                if (value is null) return;
                _primary.Write(value);
                _mirror.Write(value);
            }

            /// <summary>Запись буфера: копируется в основной поток и в файл лога.</summary>
            public override void Write(char[] buffer, int index, int count)
            {
                _primary.Write(buffer, index, count);
                _mirror.Write(buffer, index, count);
            }

            /// <summary>Флеш обоих потоков (основной и файл лога).</summary>
            public override void Flush()
            {
                _primary.Flush();
                try { _mirror.Flush(); } catch { /* файл уже закрыт — не страшно */ }
            }

            // Close() намеренно ничего не делает: исходный поток консоли закрывать нельзя.
        }

        /// <summary>Оригинальный вывод консоли.</summary>
        private readonly TextWriter _originalOut;

        /// <summary>Оригинальный вывод ошибок (может быть null).</summary>
        private readonly TextWriter? _originalErr;

        /// <summary>Writer для файла лога.</summary>
        private readonly StreamWriter _fileWriter;

        /// <summary>Флаг, что объект уже освобождён.</summary>
        private bool _disposed;

        /// <summary>Статический конструктор: создаёт ConsoleLogTee и возвращает null при ошибке.</summary>
        public static ConsoleLogTee? Create(string path, out string? error)
        {
            try
            {
                error = null;
                return new ConsoleLogTee(path)!;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = $"Не удалось открыть файл лога: {path} ({ex.Message})";
                return null;
            }
        }

        /// <summary>Конструктор: открывает файл лога и переключает потоки консоли.</summary>
        private ConsoleLogTee(string path)
        {
            _fileWriter = new StreamWriter(
                new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
                Console.OutputEncoding); // та же кодировка, что и консоль → один к одному

            _originalOut = Console.Out;
            _originalErr = Console.Error;

            Console.SetOut(new TeeTextWriter(_originalOut, _fileWriter));
            Console.SetError(new TeeTextWriter(_originalErr, _fileWriter));
        }

        /// <summary>Флеш + закрытие файла (без возврата исходной консоли) — для Ctrl+C.</summary>
        public void Shutdown()
        {
            if (_disposed) return;
            try { _fileWriter.Flush(); _fileWriter.Dispose(); }
            catch { /* процесс всё равно завершается */ }
            _disposed = true;
        }

        /// <summary>Освобождение ресурсов: возвращает исходную консоль и закрывает файл.</summary>
        public void Dispose()
        {
            Shutdown();
            Console.SetOut(_originalOut);
            if (_originalErr != null) Console.SetError(_originalErr);
        }
    }
}
