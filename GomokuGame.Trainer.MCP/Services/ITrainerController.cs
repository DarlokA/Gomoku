using System;
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
        /// Запустить обучение указанной сети через команду train
        /// </summary>
        /// <param name="command">Команда для запуска обучения (ITrainCommand)</param>
        /// <param name="network">Имя обучаемой сети (A/B/C/D/E)</param>
        /// <param name="games">Количество партий для обучения (0 — бесконечно)</param>
        /// <param name="saveName">Имя сохранения для результата (если не указано, используется дефолтное)</param>
        /// <param name="seed">Seed случайных чисел</param>
        /// <param name="learningRate">Learning rate</param>
        /// <returns>Асинхронная задача запуска обучения</returns>
        Task TrainAsync(
            ITrainCommand command,
            string network,
            int games,
            string? saveName = null,
            int seed = 42,
            double learningRate = 0.01);

        /// <summary>
        /// Запустить обучение указанной сети с расширенными параметрами тренера.
        /// </summary>
        /// <param name="command">Команда для запуска обучения</param>
        /// <param name="network">Имя обучаемой сети (A/B/C/D/E)</param>
        /// <param name="additionalArgs">Дополнительные параметры тренера в формате key:value (опционально)</param>
        /// <returns>Асинхронная задача запуска обучения</returns>
        Task TrainAdvancedAsync(
            ITrainCommand command,
            string network,
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