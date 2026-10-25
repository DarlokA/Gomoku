namespace GomokuGame.Trainer
{
    /// <summary>
    /// Настройки выполнения одного обучающего чанка.
    /// </summary>
    internal sealed class TrainingChunkOptions
    {
        public string Algorithm { get; }
        public GameSettings Game { get; }
        public int Games { get; set; }
        public LeagueTrainingSettings League { get; }

        public TrainingChunkOptions(
            string algorithm,
            GameSettings game,
            int games,
            LeagueTrainingSettings league)
        {
            Algorithm = algorithm;
            Game = game;
            Games = games;
            League = league;
        }
    }
}