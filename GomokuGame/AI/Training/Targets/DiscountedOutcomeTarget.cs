using System;
using GomokuGame.Models;

namespace GomokuGame.AI.Training.Targets
{
    /// <summary>
    /// Дисконтированный финальный исход. Ходы победителя: 0.5 + 0.5·γ^dist. Остальные: 0.5.
    /// </summary>
    internal sealed class DiscountedOutcomeTarget : ITrainingTarget
    {
        public double Compute(
            MoveRecord move,
            int index,
            TrainingContext context)
        {
            if (context.Winner == CellState.Empty)
                return 0.5;

            if (move.Player == context.Winner
                && context.LastWinnerIndex >= 0)
            {
                int dist = context.LastWinnerIndex - index;

                return 0.5
                    + 0.5 * Math.Pow(context.Discount, dist);
            }

            return 0.5;
        }
    }
}