using System;
using GomokuGame.AI;
using GomokuGame.AI.Training;
using GomokuGame.AI.Training.Targets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GomokuGame.Tests
{
    [TestClass]
    public class TrainingTargetFactoryTests
    {
        [DataTestMethod]
        [DataRow(
            AiPlayer.LearningMode.LineLengthOnly,
            typeof(LineLengthTarget))]
        [DataRow(
            AiPlayer.LearningMode.DiscountedOutcome,
            typeof(DiscountedOutcomeTarget))]
        [DataRow(
            AiPlayer.LearningMode.DiscountedWithShaping,
            typeof(DiscountedWithShapingTarget))]
        [DataRow(
            AiPlayer.LearningMode.AttackDefenseShaping,
            typeof(AttackDefenseTarget))]
        [DataRow(
            AiPlayer.LearningMode.LocalTactical,
            typeof(LocalTacticalTarget))]
        public void Get_ReturnsCalculatorForRequestedMode(
            AiPlayer.LearningMode mode,
            Type expectedType)
        {
            var target = TrainingTargetFactory.Get(mode);

            Assert.IsNotNull(target);
            Assert.AreEqual(expectedType, target.GetType());
        }

        [DataTestMethod]
        [DataRow(AiPlayer.LearningMode.LineLengthOnly, 0)]
        [DataRow(AiPlayer.LearningMode.DiscountedOutcome, 1)]
        [DataRow(AiPlayer.LearningMode.DiscountedWithShaping, 2)]
        [DataRow(AiPlayer.LearningMode.AttackDefenseShaping, 3)]
        [DataRow(AiPlayer.LearningMode.LocalTactical, 4)]
        public void LearningMode_NumericValuesRemainStable(
            AiPlayer.LearningMode mode,
            int expectedValue)
        {
            Assert.AreEqual(expectedValue, (int)mode);
        }

        [TestMethod]
        public void Get_UnknownMode_ReturnsNull()
        {
            var target = TrainingTargetFactory.Get(
                (AiPlayer.LearningMode)999);

            Assert.IsNull(target);
        }
    }
}
