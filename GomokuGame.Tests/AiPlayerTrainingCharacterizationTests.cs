using System;
using GomokuGame.AI;
using GomokuGame.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GomokuGame.Tests
{
    [TestClass]
    public class AiPlayerTrainingCharacterizationTests
    {
        private const double Tolerance = 1e-12;

        // Фиксируем именно СУЩЕСТВУЮЩЕЕ поведение.
        // Новые правила оценки атак и угроз сюда пока не добавляем.

        [DataTestMethod]
        [DataRow(0, 5, 0.0)]
        [DataRow(1, 5, 0.0)]
        [DataRow(2, 5, 0.25)]
        [DataRow(3, 5, 0.50)]
        [DataRow(4, 5, 0.75)]
        [DataRow(5, 5, 1.0)]
        [DataRow(8, 5, 1.0)]
        [DataRow(3, 3, 0.50)]
        [DataRow(3, 7, 0.50)]
        public void A_CurrentTarget_UsesFixedDivisorFour(
            int lineLength,
            int winLength,
            double expectedTarget)
        {
            var ai = CreatePlayer(AiPlayer.LearningMode.LineLengthOnly);
            var move = Move(CellState.X, lineLength);

            AssertTraining(
                ai, CellState.O, winLength,
                new[] { move },
                new[] { expectedTarget });
        }

        [DataTestMethod]
        [DataRow(CellState.Empty)]
        [DataRow(CellState.O)]
        public void B_DrawOrLosingPlayer_TargetIsHalf(CellState winner)
        {
            var ai = CreatePlayer(AiPlayer.LearningMode.DiscountedOutcome);

            AssertTraining(
                ai, winner, 5,
                new[] { Move(CellState.X, 5) },
                new[] { 0.5 });
        }

        [DataTestMethod]
        [DataRow(0.9, 0.905)]
        [DataRow(0.5, 0.625)]
        public void B_DiscountDistance_CountsAllHistoryRecords(
            double discount,
            double firstTarget)
        {
            var ai = CreatePlayer(AiPlayer.LearningMode.DiscountedOutcome);
            ai.Discount = discount;

            // Между двумя записями X находится запись O.
            // Текущая дистанция для первой записи X равна 2, а не 1.
            var moves = new[]
            {
                Move(CellState.X, 2),
                Move(CellState.O, 3),
                Move(CellState.X, 5)
            };

            AssertTraining(
                ai, CellState.X, 5, moves,
                new[] { firstTarget, 0.5, 1.0 });
        }

        [TestMethod]
        public void C_Draw_StillIncludesLineShaping()
        {
            var ai = CreatePlayer(
                AiPlayer.LearningMode.DiscountedWithShaping);

            AssertTraining(
                ai, CellState.Empty, 5,
                new[] { Move(CellState.X, 3) },
                new[] { 0.55 });
        }

        [TestMethod]
        public void C_AddsShapingToDiscountedTarget_AndClamps()
        {
            var ai = CreatePlayer(
                AiPlayer.LearningMode.DiscountedWithShaping);

            var moves = new[]
            {
                Move(CellState.X, 3),
                Move(CellState.O, 1),
                Move(CellState.X, 5)
            };

            AssertTraining(
                ai, CellState.X, 5, moves,
                new[] { 0.955, 0.5, 1.0 });
        }

        [DataTestMethod]
        [DataRow(5, 3.0, 1.5, CellState.Empty, 0.56)]
        [DataRow(10, 5.0, 5.0, CellState.Empty, 0.40)]
        [DataRow(5, 50.0, 0.0, CellState.Empty, 0.90)]
        [DataRow(5, 0.0, 50.0, CellState.Empty, 0.0)]
        [DataRow(5, 5.0, 5.0, CellState.X, 0.80)]
        [DataRow(5, 5.0, 0.0, CellState.X, 1.0)]
        public void D_CurrentTarget_CombinesOutcomeAttackAndDefense(
            int winLength,
            double attack,
            double opponent,
            CellState winner,
            double expectedTarget)
        {
            var ai = CreatePlayer(
                AiPlayer.LearningMode.AttackDefenseShaping);

            var move = Move(CellState.X, 5);
            move.AttackPotential = attack;
            move.OpponentPotential = opponent;

            AssertTraining(
                ai, winner, winLength,
                new[] { move },
                new[] { expectedTarget });
        }

        [TestMethod]
        public void D_UsesConfiguredAttackAndDefenseWeights()
        {
            var ai = CreatePlayer(
                AiPlayer.LearningMode.AttackDefenseShaping);

            ai.AttackWeight = 0.2;
            ai.DefenseWeight = 0.3;

            var move = Move(CellState.X, 3);
            move.AttackPotential = 5.0;
            move.OpponentPotential = 5.0;

            AssertTraining(
                ai, CellState.Empty, 10,
                new[] { move },
                new[] { 0.45 });
        }

        [DataTestMethod]
        [DataRow(-0.2, 0.0)]
        [DataRow(0.9, 0.9)]
        [DataRow(1.2, 1.0)]
        public void ForcedCorrection_OverridesEveryLearningMode(
            double criticality,
            double expectedTarget)
        {
            foreach (var mode in
                     Enum.GetValues<AiPlayer.LearningMode>())
            {
                var ai = CreatePlayer(mode);
                var move = Move(CellState.X, 1);

                move.IsForcedCorrection = true;
                move.Criticality = criticality;
                move.AttackPotential = 0;
                move.OpponentPotential = 100;

                AssertTraining(
                    ai, CellState.O, 5,
                    new[] { move },
                    new[] { expectedTarget });
            }
        }

        [DataTestMethod]
        [DataRow(1.0)]
        [DataRow(3.0)]
        [DataRow(5.0)]
        [DataRow(8.0)]
        public void SampleWeight_MultipliesLearningRate(double weight)
        {
            var ai = CreatePlayer(AiPlayer.LearningMode.LineLengthOnly);
            var move = Move(CellState.X, 4);
            move.CriticalityWeight = weight;

            AssertTraining(
                ai, CellState.Empty, 5,
                new[] { move },
                new[] { 0.75 });
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void Orientations_AreTrainedInEpisodeDependentOrder(
            int episodesTrained)
        {
            var ai = CreatePlayer(AiPlayer.LearningMode.LineLengthOnly);
            ai.EpisodesTrained = episodesTrained;

            // Намеренно разные диагностические входы:
            // перестановка обновлений должна быть заметна по весам.
            var move = new MoveRecord(
                new[] { 9.0 }, 0, 0, CellState.X)
            {
                LineLengthAfter = 4,
                AllOrientationStates = new[]
                {
                    new[] { 1.0 },
                    new[] { 0.0 },
                    new[] { 2.0 },
                    new[] { -1.0 }
                }
            };

            AssertTraining(
                ai, CellState.Empty, 5,
                new[] { move },
                new[] { 0.75 });
        }

        [DataTestMethod]
        [DataRow(AiPlayer.CorrectionMode.Off, 0, 0)]
        [DataRow(AiPlayer.CorrectionMode.Once, 0, 0)]
        [DataRow(AiPlayer.CorrectionMode.Full, 1, 1)]
        [DataRow(AiPlayer.CorrectionMode.All, 1, 1)]
        public void CriticalSample_IsStoredAndReplayedAccordingToMode(
            AiPlayer.CorrectionMode correction,
            int expectedBufferSize,
            int expectedAttempts)
        {
            var ai = CreatePlayer(AiPlayer.LearningMode.LineLengthOnly);
            ai.Correction = correction;

            // Не позволяем буферу признать пример выученным и удалить его.
            ai.CriticalBuffer.GoodErrorThreshold = -1.0;

            var states = new[]
            {
                new[] { 1.0 },
                new[] { 0.0 },
                new[] { 2.0 },
                new[] { -1.0 }
            };

            ai.RecordMoveWithWeight(
                stateBefore: states[0],
                row: 0,
                col: 0,
                player: CellState.X,
                lineLengthAfter: 1,
                attackPotential: 0,
                opponentPotential: 0,
                criticalityWeight: 5.0,
                criticality: 0.9,
                isForcedCorrection: true,
                allOrientationStates: states,
                isCritical: true);

            ai.LearnFromGame(CellState.O, 5);

            Assert.AreEqual(expectedBufferSize, ai.CriticalBuffer.BufferSize);
            Assert.AreEqual(0, ai.HistoryCount);
            Assert.AreEqual(1, ai.EpisodesTrained);

            if (expectedBufferSize > 0)
            {
                var sample = ai.CriticalBuffer.Snapshot()[0];

                Assert.AreEqual(0.9, sample.Target, Tolerance);
                Assert.AreEqual(5.0, sample.Weight, Tolerance);
                Assert.AreEqual(expectedAttempts, sample.AttemptsSinceAdded);
            }
        }

        [DataTestMethod]
        [DataRow(AiPlayer.CorrectionMode.Off, 0)]
        [DataRow(AiPlayer.CorrectionMode.Once, 0)]
        [DataRow(AiPlayer.CorrectionMode.Full, 0)]
        [DataRow(AiPlayer.CorrectionMode.All, 1)]
        public void ExistingBuffer_WithoutNewCriticalSamples_IsReplayedOnlyInAll(
            AiPlayer.CorrectionMode correction,
            int expectedAttempts)
        {
            var ai = CreatePlayer(AiPlayer.LearningMode.LineLengthOnly);
            ai.Correction = correction;
            ai.CriticalBuffer.GoodErrorThreshold = -1.0;

            var states = new[]
            {
                new[] { 1.0 },
                new[] { 0.0 },
                new[] { 2.0 },
                new[] { -1.0 }
            };

            long key = CriticalSampleBuffer.ComputeKey(states[0]);
            ai.CriticalBuffer.AddOrUpdate(states, 0.9, 5.0, key);

            // Обычный, некритичный пример.
            ai.RecordMove(
                stateBefore: new[] { 1.0 },
                row: 0,
                col: 0,
                player: CellState.X,
                lineLengthAfter: 3,
                attackPotential: 0,
                opponentPotential: 0);

            ai.LearnFromGame(CellState.Empty, 5);

            Assert.AreEqual(1, ai.CriticalBuffer.BufferSize);
            Assert.AreEqual(
                expectedAttempts,
                ai.CriticalBuffer.Snapshot()[0].AttemptsSinceAdded);
        }



        [TestMethod]
        public void EmptyHistory_DoesNotTrainOrAdvanceEpisode()
        {
            var ai = CreatePlayer(AiPlayer.LearningMode.LineLengthOnly);
            ai.EpisodesTrained = 7;
            ai.LastAverageLoss = 0.123;

            var layer = ai.Network.GetLayer(0);
            double oldWeight = layer.W[0][0];
            double oldBias = layer.B[0];
            double oldEpsilon = ai.Epsilon;

            double loss = ai.LearnFromGame(CellState.X, 5);

            Assert.AreEqual(0.0, loss, Tolerance);
            Assert.AreEqual(7, ai.EpisodesTrained);
            Assert.AreEqual(0.123, ai.LastAverageLoss, Tolerance);
            Assert.AreEqual(oldEpsilon, ai.Epsilon, Tolerance);
            Assert.AreEqual(oldWeight, layer.W[0][0], Tolerance);
            Assert.AreEqual(oldBias, layer.B[0], Tolerance);
        }

        [TestMethod]
        public void EmptyHistory_DoesNotReplayEvenInAllMode()
        {
            var ai = CreatePlayer(AiPlayer.LearningMode.LineLengthOnly);
            ai.Correction = AiPlayer.CorrectionMode.All;

            var states = new[] { new[] { 1.0 } };
            long key = CriticalSampleBuffer.ComputeKey(states[0]);

            ai.CriticalBuffer.AddOrUpdate(states, 0.9, 5.0, key);

            double loss = ai.LearnFromGame(CellState.X, 5);

            Assert.AreEqual(0.0, loss, Tolerance);
            Assert.AreEqual(0, ai.EpisodesTrained);
            Assert.AreEqual(1, ai.CriticalBuffer.BufferSize);
            Assert.AreEqual(
                0,
                ai.CriticalBuffer.Snapshot()[0].AttemptsSinceAdded);
        }

        private static AiPlayer CreatePlayer(AiPlayer.LearningMode mode)
        {
            var layer = new Layer(
                1, 1, ActivationType.Linear, new Random(42));

            // Явно задаём начальные параметры:
            // тест не зависит от случайной инициализации.
            layer.W[0][0] = 0.17;
            layer.B[0] = -0.08;

            return new AiPlayer(
                new NeuralNetwork(layer), new Random(42))
            {
                Mode = mode,
                Correction = AiPlayer.CorrectionMode.Once,
                LearningRate = 0.05,
                Discount = 0.9,
                AttackWeight = 0.4,
                DefenseWeight = 0.6,
                Epsilon = 0.3,
                EpsilonMin = 0.02,
                EpsilonDecay = 0.995
            };
        }

        private static MoveRecord Move(
            CellState player, int lineLength)
        {
            return new MoveRecord(new[] { 1.0 }, 0, 0, player)
            {
                LineLengthAfter = lineLength
            };
        }

        private static void AssertTraining(
            AiPlayer ai,
            CellState winner,
            int winLength,
            MoveRecord[] moves,
            double[] expectedTargets)
        {
            Assert.AreEqual(moves.Length, expectedTargets.Length);
            Assert.IsTrue(moves.Length > 0);

            var layer = ai.Network.GetLayer(0);
            double expectedWeight = layer.W[0][0];
            double expectedBias = layer.B[0];
            double expectedTotalLoss = 0;

            int oldEpisodes = ai.EpisodesTrained;
            double oldEpsilon = ai.Epsilon;
            bool reverse = oldEpisodes % 2 == 1;

            for (int i = 0; i < moves.Length; i++)
            {
                var move = moves[i];
                double target = expectedTargets[i];

                ai.RecordMoveWithWeight(
                    stateBefore: move.State,
                    row: move.Row,
                    col: move.Col,
                    player: move.Player,
                    lineLengthAfter: move.LineLengthAfter,
                    attackPotential: move.AttackPotential,
                    opponentPotential: move.OpponentPotential,
                    criticalityWeight: move.CriticalityWeight,
                    criticality: move.Criticality,
                    isForcedCorrection: move.IsForcedCorrection,
                    allOrientationStates: move.AllOrientationStates,
                    isCritical: false);

                var states = move.AllOrientationStates
                    ?? new[] { move.State };

                double sampleLoss = 0;
                double lr = ai.LearningRate * move.CriticalityWeight;

                for (int k = 0; k < states.Length; k++)
                {
                    int orientation = reverse
                        ? states.Length - 1 - k
                        : k;

                    double input = states[orientation][0];

                    // Независимый расчёт одного SGD-шага
                    // для единственного линейного нейрона.
                    double output =
                        expectedWeight * input + expectedBias;

                    double error = output - target;
                    sampleLoss += error * error;

                    double gradient = 2.0 * error;
                    expectedWeight -= lr * gradient * input;
                    expectedBias -= lr * gradient;
                }

                expectedTotalLoss += sampleLoss / states.Length;
            }

            Assert.AreEqual(moves.Length, ai.HistoryCount);

            double actualLoss = ai.LearnFromGame(winner, winLength);
            double expectedLoss = expectedTotalLoss / moves.Length;
            string context = $"Mode: {ai.Mode}";

            Assert.AreEqual(
                expectedLoss, actualLoss, Tolerance, context);

            Assert.AreEqual(
                expectedLoss, ai.LastAverageLoss, Tolerance, context);

            Assert.AreEqual(
                expectedWeight, layer.W[0][0], Tolerance, context);

            Assert.AreEqual(
                expectedBias, layer.B[0], Tolerance, context);

            Assert.AreEqual(0, ai.HistoryCount, context);
            Assert.AreEqual(
                oldEpisodes + 1, ai.EpisodesTrained, context);

            Assert.AreEqual(
                Math.Max(ai.EpsilonMin, oldEpsilon * ai.EpsilonDecay),
                ai.Epsilon, Tolerance, context);
        }
    }

}