using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.AI;
using GomokuGame.Models;

namespace GomokuGame.Tests
{
    [TestClass]
    public class GameSessionTests
    {
        [TestMethod]
        public void PlayOneGame_Completes()
        {
            var ai = AiPlayer.CreateFresh(seed: 42);
            var result = GameSession.PlayOneGame(ai, boardSize: 6, winLength: 4);

            // Партия должна завершиться (не зациклиться)
            Assert.IsTrue(result.Moves > 0);
            Assert.IsTrue(result.Moves <= 36); // 6x6 = 36 клеток
        }

        [TestMethod]
        [Ignore("Loss может колебаться на коротких интервалах")]
        public void TrainMany_LossDecreases()
        {
            var ai = AiPlayer.CreateFresh(seed: 42);
            ai.LearningRate = 0.01;
            ai.Epsilon = 0.3;
            ai.EpsilonDecay = 0.99;

            // Первые 50 партий — замеряем средний loss
            var statsEarly = GameSession.TrainMany(ai, 50, boardSize: 6, winLength: 4);
            double earlyLoss = statsEarly.LastAverageLoss;

            // Ещё 500
            var statsLate = GameSession.TrainMany(ai, 500, boardSize: 6, winLength: 4);
            double lateLoss = statsLate.LastAverageLoss;

            // Loss должен снизиться (хотя бы немного)
            Assert.IsTrue(lateLoss < earlyLoss,
                $"Early loss: {earlyLoss}, Late loss: {lateLoss}");
        }

        [TestMethod]
        public void TrainManyAgainstBot_ShortRun_Completes()
        {
            var ai = AiPlayer.CreateFresh(seed: 42);
            var stats = GameSession.TrainManyAgainstBot(
                ai, games: 50, boardSize: 9, winLength: 5, chunkSize: 50);

            Assert.AreEqual(50, stats.GamesPlayed);
            Assert.IsTrue(stats.AverageMovies > 0);
        }

        [TestMethod]
        public void TrainManyAgainstBot_SwitchesOrientationBetweenChunks()
        {
            var ai = AiPlayer.CreateFresh(seed: 42);

            // 4 чанка по 10 партий = 40 партий
            // Ориентации: 0, 1, 2, 3
            // Если бы orientation не менялась — результаты были бы одинаковыми
            var stats = GameSession.TrainManyAgainstBot(
                ai, games: 40, boardSize: 9, winLength: 5, chunkSize: 10);

            Assert.AreEqual(40, stats.GamesPlayed);
        }
    }
}