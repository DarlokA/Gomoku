using System.Collections.Generic;
using System.Threading.Tasks;
using GomokuGame.Trainer.MCP.Commands;

namespace GomokuGame.Trainer.MCP.Services
{
    /// <summary>
    /// Интерфейс для управления процессом обучения Gomoku сети.
    /// </summary>
    public interface ITrainerController
    {
        /// <summary>
        /// Флаг активности процесса обучения.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Режим обучения (LearningMode) — A/B/C/D/E:
        /// A - reward = длина линии после хода, без исхода партии.
        /// B - reward = дисконт от победы. Ходы победителя — 0.5 + 0.5·γ^dist, остальные — 0.5.
        /// C - reward = дисконт + shaping за длину линии.
        /// D - discount + shaping за потенциальную длину линии.
        /// E - LocalTactical (LTT), локальная тактическая оценка хода.
        /// </summary>
        string LearningMode { get; set; }

        /// <summary>
        /// Коррекция ошибок: FULL/ONCE/OFF/ALL
        /// </summary>
        string CorrectionMode { get; set; }

        /// <summary>
        /// Запустить обучение указанной сети через команду train.
        /// </summary>
        /// <param name="command">Команда для запуска обучения (ITrainCommand)</param>
        /// <param name="network">Имя обучаемой сети (произвольное, например: "network_A", "topology_test")</param>
        /// <param name="args">Аргументы командной строки тренера в формате ключ:значение (опционально)</param>
        /// <returns>Асинхронная задача запуска обучения</returns>
        Task TrainAsync(
            ITrainCommand command,
            string network,           // ИМЯ СЕТИ! (произвольное, не A/B/C/D/E!)
            Dictionary<string, string>? args = null);

        /// <summary>
        /// Запустить обучение указанной сети с указанными параметрами.
        /// </summary>
        /// <param name="command">Команда для запуска обучения</param>
        /// <param name="network">Имя обучаемой сети (произвольное)</param>
        /// <param name="games">Количество партий для обучения</param>
        /// <param name="saveName">Имя сохранения обученной сети (опционально)</param>
        /// <param name="seed">Seed случайных чисел</param>
        /// <param name="learningRate">Learning rate</param>
        /// <returns>Асинхронная задача запуска обучения</returns>
        Task TrainAsync(
            ITrainCommand command,
            string network,           // ИМЯ СЕТИ! (произвольное)
            int games,
            string? saveName = null,  // Имя сохранения обученной сети!
            int seed = 42,
            double learningRate = 0.01);

        /// <summary>
        /// Запустить обучение указанной сети с расширенными параметрами тренера.
        /// </summary>
        /// <param name="command">Команда для запуска обучения</param>
        /// <param name="network">Имя обучаемой сети (произвольное)</param>
        /// <param name="additionalArgs">Дополнительные параметры тренера в формате key:value (опционально)</param>
        /// <returns>Асинхронная задача запуска обучения</returns>
        Task TrainAdvancedAsync(
            ITrainCommand command,
            string network,           // ИМЯ СЕТИ!
            Dictionary<string, string>? additionalArgs = null);

        /// <summary>
        /// Остановить процесс обучения.
        /// </summary>
        /// <returns>Асинхронная задача остановки</returns>
        Task StopTrainingAsync();

        /// <summary>
        /// Запустить тренер с указанными аргументами (для гибкости).
        /// </summary>
        /// <param name="args">Аргументы командной строки тренера</param>
        /// <returns>Асинхронная задача запуска</returns>
        Task RunTrainerAsync(string[] args);

        /// <summary>
        /// Получить текущее состояние процесса обучения.
        /// </summary>
        /// <returns>Состояние обучения (TrainingState)</returns>
        Task<Models.TrainingState> GetStatusAsync();
    }
}