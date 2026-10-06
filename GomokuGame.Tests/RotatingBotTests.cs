using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.AI;
using GomokuGame.Models;

namespace GomokuGame.Tests
{
    [TestClass]
    public class RotatingBotTests
    {
        [TestMethod]
        public void Bot_CanChooseMove_OnEmptyBoard()
        {
            var ai = AiPlayer.CreateFresh(seed: 42);
            var bot = new RotatingBot(ai, orientation: 0);

            var board = new GameBoard(15, 5);
            var move = bot.ChooseMove(board, CellState.X);

            Assert.IsNotNull(move, "Бот должен найти ход на пустой доске");
        }

        [TestMethod]
        public void Bot_DifferentOrientations_SameBoard()
        {
            var ai = AiPlayer.CreateFresh(seed: 42);
            var board = new GameBoard(15, 5);

            // Ставим несколько X, чтобы оценки различались
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;

            var bot0 = new RotatingBot(ai, 0);
            var bot1 = new RotatingBot(ai, 1);

            // Оценка одного и того же хода должна различаться
            double score0 = bot0.EvaluateMove(board, 6, 7, CellState.X);
            double score1 = bot1.EvaluateMove(board, 6, 7, CellState.X);

            Assert.AreNotEqual(score0, score1, "Разные ориентации должны давать разные оценки");
        }

        [TestMethod]
        public void Bot_DoesNotAffectNetworkHistory()
        {
            var ai = AiPlayer.CreateFresh(seed: 42);
            var bot = new RotatingBot(ai, 0);
            var board = new GameBoard(15, 5);

            int historyBefore = ai.HistoryCount;

            // Бот делает несколько ходов
            bot.ChooseMove(board, CellState.X);
            board[7, 7] = CellState.X;
            bot.ChooseMove(board, CellState.O);

            int historyAfter = ai.HistoryCount;

            Assert.AreEqual(historyBefore, historyAfter, "Бот не должен писать в историю сети");
        }

        [TestMethod]
        public void Bot_InvalidOrientation_Throws()
        {
            var ai = AiPlayer.CreateFresh(seed: 42);
            Assert.ThrowsException<System.ArgumentOutOfRangeException>(
                () => new RotatingBot(ai, orientation: 5));
        }
    }
}