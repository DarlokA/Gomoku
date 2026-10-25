using System;

namespace GomokuGame.AI.Training.Targets
{
    internal sealed class AttackDefenseTarget : ITrainingTarget
    {
        private readonly DiscountedOutcomeTarget _outcome = new();

        public double Compute(
            MoveRecord move,
            int index,
            TrainingContext context)
        {
            // База: дисконтированный исход
            double baseTarget = _outcome.Compute(move, index, context);

            // Shaping: атака — свой потенциал, защита — потенциал противника
            // winLength — надо знать! Возьмём извне.
            double attack = Math.Min(
                1.0,
                move.AttackPotential / context.WinLength);

            double defense = Math.Min(
                1.0,
                move.OpponentPotential / context.WinLength);

            // Плохо, если потенциал противника ВЫСОКИЙ
            double shaping =
                context.AttackWeight * attack
                - context.DefenseWeight * defense;

            return Math.Clamp(baseTarget + shaping, 0.0, 1.0);
        }
    }
}