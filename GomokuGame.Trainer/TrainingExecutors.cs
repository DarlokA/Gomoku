using GomokuGame.AI;

namespace GomokuGame.Trainer
{
    /// <summary>
    /// Выполнители методов обучения — выносят логику выполнения чанков из Program.cs.
    /// </summary>
    internal static class TrainingExecutors
    {
        /// <summary>
        /// Обучает нейросеть стандартным методом (градиентный спуск).
        /// Вызывает GameSession.TrainMany для одной партии.
        /// </summary>
        /// <param name="ai">Игровой ИИ с обучаемой сетью.</param>
        /// <param name="options">Параметры обучения (количество партий, настройки игры).</param>
        /// <returns>Статистика обучения за чанк.</returns>
        public static TrainingStats TrainStandardChunk(
            AiPlayer ai,
            TrainingChunkOptions options)
        {
            return GameSession.TrainMany(
                ai,
                options.Games,
                options.Game.BoardSize,
                options.Game.WinLength,
                progressEvery: options.Games);
        }

        /// <summary>
        /// Обучает нейросеть в режиме лиги (league).
        /// Вызывает GameSession.TrainManyLeague с warmup и snapshot.
        /// </summary>
        /// <param name="ai">Игровой ИИ с обучаемой сетью.</param>
        /// <param name="options">Параметры обучения (количество партий, настройки игры, лиги).</param>
        /// <returns>Статистика обучения за чанк.</returns>
        public static TrainingStats TrainLeagueChunk(
            AiPlayer ai,
            TrainingChunkOptions options)
        {
            return GameSession.TrainManyLeague(
                ai,
                options.Games,
                options.Game.BoardSize,
                options.Game.WinLength,
                options.League.WarmupGames,
                options.League.SnapshotEvery,
                progressEvery: options.Games);
        }

        /// <summary>
        /// Обучает нейросеть против простого бота.
        /// Вызывает GameSession.TrainManyAgainstBot для тренировки.
        /// </summary>
        /// <param name="ai">Игровой ИИ с обучаемой сетью.</param>
        /// <param name="options">Параметры обучения (количество партий, настройки игры).</param>
        /// <returns>Статистика обучения за чанк.</returns>
        public static TrainingStats TrainBotChunk(
            AiPlayer ai,
            TrainingChunkOptions options)
        {
            return GameSession.TrainManyAgainstBot(
                ai,
                options.Games,
                options.Game.BoardSize,
                options.Game.WinLength,
                chunkSize: options.Games);
        }
    }
}
