using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.AI;
using GomokuGame.Models;
using System;
using System.Diagnostics;

namespace GomokuGame.Tests
{
    [TestClass]
    [TestCategory("LongRunning")]
    public class GameSessionTrainingTests
    {
        [TestMethod]
        public void Train_10000Games_OnBoard9x9_Win5()
        {
            const int boardSize = 9;
            const int winLength = 5;
            const int games = 10_000;

            var ai = AiPlayer.CreateFresh(seed: 42);
            ai.LearningRate = 0.001;
            ai.Epsilon = 0.3;
            ai.EpsilonMin = 0.02;
            ai.EpsilonDecay = 0.9995;

            var sw = Stopwatch.StartNew();

            int winsX = 0, winsO = 0, draws = 0;

            // Обучаем батчами, чтобы видеть прогресс
            const int chunk = 1000;
            for (int batch = 0; batch < games / chunk; batch++)
            {
                var stats = GameSession.TrainMany(
                    ai,
                    chunk,
                    boardSize,
                    winLength,
                    progressEvery: chunk);

                winsX += stats.XWins;
                winsO += stats.OWins;
                draws += stats.Draws;

                Debug.WriteLine(
                    $"Batch {batch + 1}: " +
                    $"X={stats.XWins}/{chunk}, " +
                    $"O={stats.OWins}/{chunk}, " +
                    $"Draw={stats.Draws}/{chunk}, " +
                    $"Loss={stats.LastAverageLoss:F4}, " +
                    $"Eps={ai.Epsilon:F3}, " +
                    $"Time={sw.ElapsedMilliseconds}ms");
            }

            sw.Stop();

            double totalGames = winsX + winsO + draws;
            double xRate = winsX / totalGames;
            double oRate = winsO / totalGames;
            double dRate = draws / totalGames;

            Debug.WriteLine($"=== FINAL ===");
            Debug.WriteLine($"Games: {totalGames}");
            Debug.WriteLine($"X wins: {winsX} ({xRate:P1})");
            Debug.WriteLine($"O wins: {winsO} ({oRate:P1})");
            Debug.WriteLine($"Draws:  {draws} ({dRate:P1})");
            Debug.WriteLine($"Total time: {sw.Elapsed.TotalSeconds:F1}s");
            Debug.WriteLine($"Avg per game: {sw.Elapsed.TotalMilliseconds / totalGames:F2}ms");

            // Проверки:
            // 1. Все партии завершились
            Assert.AreEqual((double)games, totalGames);

            // 2. Ничьих должно быть не слишком много (иначе странно)
            Assert.IsTrue(dRate < 0.5,
                $"Слишком много ничьих: {dRate:P1}");

            // 3. Не должно быть "залипания" — например, X всегда выигрывает
            //    (это означало бы, что сеть выучила тривиальную стратегию)
            Assert.IsTrue(xRate < 0.99, $"X выигрывает слишком часто: {xRate:P1}");
            Assert.IsTrue(oRate < 0.99, $"O выигрывает слишком часто: {oRate:P1}");
        }
    }
}