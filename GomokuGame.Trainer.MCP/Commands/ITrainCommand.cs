using System.Collections.Generic;
using System.Threading.Tasks;

namespace GomokuGame.Trainer.MCP.Commands
{
    /// <summary>
    /// Интерфейс для команды запуска обучения.
    /// </summary>
    public interface ITrainCommand
    {
        /// <summary>
        /// Запустить обучение указанной сети через args (краткий режим).
        /// </summary>
        /// <param name="network">Имя обучаемой сети (произвольное, например: "network_A", "topology_test")</param>
        /// <param name="args">Аргументы командной строки тренера в формате ключ:значение (опционально)</param>
        /// <returns>Асинхронная задача запуска обучения</returns>
        Task TrainAsync(
            string network,           // ИМЯ СЕТИ! (произвольное)
            Dictionary<string, string>? args = null);

        /// <summary>
        /// Запустить обучение указанной сети с указанными параметрами.
        /// </summary>
        /// <param name="network">Имя обучаемой сети (произвольное)</param>
        /// <param name="games">Количество партий для обучения</param>
        /// <param name="saveName">Имя сохранения для результата (опционально)</param>
        /// <param name="seed">Seed случайных чисел</param>
        /// <param name="learningRate">Learning rate</param>
        /// <returns>Асинхронная задача запуска обучения</returns>
        Task TrainAsync(
            string network,           // ИМЯ СЕТИ! (произвольное)
            int games,
            string? saveName = null,  // Имя сохранения обученной сети!
            int seed = 42,
            double learningRate = 0.01);

        /// <summary>
        /// Запустить обучение указанной сети с расширенными параметрами тренера.
        /// </summary>
        /// <param name="network">Имя обучаемой сети (произвольное)</param>
        /// <param name="additionalArgs">Дополнительные параметры тренера в формате key:value (опционально)</param>
        /// <returns>Асинхронная задача запуска обучения</returns>
        Task TrainAdvancedAsync(
            string network,           // ИМЯ СЕТИ!
            Dictionary<string, string>? additionalArgs = null);

        /// <summary>
        /// Получить название команды.
        /// </summary>
        string CommandName { get; }
    }
}