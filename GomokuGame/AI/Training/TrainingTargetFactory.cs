using GomokuGame.AI.Training.Targets;

namespace GomokuGame.AI.Training
{
    internal static class TrainingTargetFactory
    {
        private static readonly ITrainingTarget _lineLength =
            new LineLengthTarget();

        private static readonly ITrainingTarget _discountedOutcome =
            new DiscountedOutcomeTarget();

        private static readonly ITrainingTarget _discountedWithShaping =
            new DiscountedWithShapingTarget();

        private static readonly ITrainingTarget _attackDefense =
            new AttackDefenseTarget();

        private static readonly ITrainingTarget _localTactical =
            new LocalTacticalTarget();

        public static ITrainingTarget? Get(AiPlayer.LearningMode mode)
        {
            return mode switch
            {
                AiPlayer.LearningMode.LineLengthOnly =>
                    _lineLength,

                AiPlayer.LearningMode.DiscountedOutcome =>
                    _discountedOutcome,

                AiPlayer.LearningMode.DiscountedWithShaping =>
                    _discountedWithShaping,

                AiPlayer.LearningMode.AttackDefenseShaping =>
                    _attackDefense,

                AiPlayer.LearningMode.LocalTactical =>
                    _localTactical,

                _ => null
            };
        }
    }
}
