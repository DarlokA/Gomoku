using System.Threading.Tasks;

namespace GomokuGame.Trainer.MCP.Commands
{
    /// <summary>
    /// Интерфейс для команды запуска обучения.
    /// </summary>
    public interface ITrainCommand
    {
        /// <summary>
        /// Запустить обучение указанной сети.
        /// </summary>
        /// <param name="network">Имя обучаемой сети (A/B/C/D/E)</param>
        /// <param name="games">Количество партий для обучения (0 — бесконечно)</param>
        /// <param name="saveName">Имя сохранения для результата (если не указано, используется дефолтное)</param>
        /// <param name="seed">Seed случайных чисел</param>
        /// <param name="learningRate">Learning rate</param>
        /// <returns>Асинхронная задача запуска обучения</returns>
        Task TrainAsync(
            string network,
            int games,
            string? saveName = null,
            int seed = 42,
            double learningRate = 0.01);

        /// <summary>
        /// Получить название команды.
        /// </summary>
        string CommandName { get; }
    }
}