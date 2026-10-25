using GomokuGame.Models;

namespace GomokuGame.AI.Training
{
    internal readonly struct TrainingContext
    {
        public CellState Winner { get; }
        public int LastWinnerIndex { get; }
        public int WinLength { get; }

        public double Discount { get; }
        public double AttackWeight { get; }
        public double DefenseWeight { get; }

        public TrainingContext(
            CellState winner,
            int lastWinnerIndex,
            int winLength,
            double discount,
            double attackWeight,
            double defenseWeight)
        {
            Winner = winner;
            LastWinnerIndex = lastWinnerIndex;
            WinLength = winLength;
            Discount = discount;
            AttackWeight = attackWeight;
            DefenseWeight = defenseWeight;
        }
    }
}