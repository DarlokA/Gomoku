using System;

namespace GomokuGame.AI.Training.Targets
{
    internal sealed class DiscountedWithShapingTarget : ITrainingTarget
    {
        private readonly DiscountedOutcomeTarget _outcome = new();

        public double Compute(
            MoveRecord move,
            int index,
            TrainingContext context)
        {
            double baseTarget = _outcome.Compute(move, index, context);
            double shaping = ComputeShaping(move);

            return Math.Clamp(baseTarget + shaping, 0.0, 1.0);
        }

        /// <summary>
        /// Shaping: небольшой бонус за длину линии. Максимум +0.1.
        /// </summary>
        private static double ComputeShaping(MoveRecord move)
        {
            // lineLen = 1 → 0; 2 → 0.025; 3 → 0.05; 4 → 0.075; 5 → 0.1
            double t = (move.LineLengthAfter - 1) / 4.0;
            return 0.1 * Math.Clamp(t, 0.0, 1.0);
        }
    }
}