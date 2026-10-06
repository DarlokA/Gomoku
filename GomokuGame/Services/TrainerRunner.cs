using System;
using System.Diagnostics;
using System.Text;

namespace GomokuGame.Services
{
    /// <summary>
    /// Запускает GomokuGame.Trainer.exe как внешний процесс и отдаёт его
    /// консольный вывод построчно через событие OutputReceived.
    /// </summary>
    public class TrainerRunner
    {
        private Process? _process;

        public bool IsRunning => _process != null && !_process.HasExited;

        /// <summary>Очередная строка стандартного вывода или ошибки процесса.</summary>
        public event Action<string>? OutputReceived;
        /// <summary>Процесс завершился штатно (с кодом возврата).</summary>
        public event Action<int>? Exited;
        /// <summary>Не удалось запустить процесс (например, не найден файл).</summary>
        public event Action<string>? ErrorOccurred;

        public void Start(string exePath, string[] args)
        {
            if (IsRunning)
                throw new InvalidOperationException("Процесс уже запущен.");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke(e.Data);
            };
            _process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke("[ERR] " + e.Data);
            };
            _process.Exited += (s, e) =>
            {
                Exited?.Invoke(_process?.ExitCode ?? -1);
            };

            try
            {
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex.Message);
                _process = null;
            }
        }

        public void Stop()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Процесс мог уже завершиться сам — игнорируем.
            }
        }
    }
}