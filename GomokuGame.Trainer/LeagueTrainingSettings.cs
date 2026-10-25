namespace GomokuGame.Trainer
{
    /// <summary>
    /// Параметры обучения в режиме лиги.
    /// </summary>
    internal readonly struct LeagueTrainingSettings
    {
        public int WarmupGames { get; }
        public int SnapshotEvery { get; }

        public LeagueTrainingSettings(
            int warmupGames,
            int snapshotEvery)
        {
            WarmupGames = warmupGames;
            SnapshotEvery = snapshotEvery;
        }
    }
}