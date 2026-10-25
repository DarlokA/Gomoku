namespace GomokuGame.Trainer
{
    /// <summary>
    /// Размер поля и условие победы.
    /// </summary>
    internal readonly struct GameSettings
    {
        public int BoardSize { get; }
        public int WinLength { get; }

        public GameSettings(int boardSize, int winLength)
        {
            BoardSize = boardSize;
            WinLength = winLength;
        }
    }
}