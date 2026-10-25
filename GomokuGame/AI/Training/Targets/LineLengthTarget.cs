using System;

namespace GomokuGame.AI.Training.Targets
{
    /// <summary>
    /// Reward только за длину линии. Победа / поражение — не учитываются.
    /// </summary>
    internal sealed class LineLengthTarget : ITrainingTarget
    {
        public double Compute(
            MoveRecord move,
            int index,
            TrainingContext context)
        {
            // lineLen = 1 → 0.0; 2 → 0.25; 3 → 0.5; 4 → 0.75; 5 → 1.0
            double t = (move.LineLengthAfter - 1) / 4.0;
            return Math.Clamp(t, 0.0, 1.0);
        }
    }
}