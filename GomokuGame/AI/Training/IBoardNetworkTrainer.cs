using System.Collections.Generic;
using GomokuGame.Models;

namespace GomokuGame.AI.Training
{
    public interface IBoardNetworkTrainer
    {
        int InputSize { get; }

        double[] Encode(
            GameBoard board,
            int row,
            int col,
            CellState player,
            int orientation = 0);

        double LearnFromGame(
            AiPlayer player,
            List<MoveRecord> history,
            CellState winner,
            int winLength);
    }
}
