using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.Models;

namespace GomokuGame.Tests
{
    [TestClass]
    public class GameBoardPotentialTests
    {

        [TestMethod]
        public void Potential_ThreeInRow_OpenBothSides_Returns5()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            double p = board.GetLinePotential(7, 6, CellState.X, 5);
            Assert.AreEqual(5, p);   // .XXX.
        }

        [TestMethod]
        public void Potential_ThreeInRow_BlockedRight_Returns5()
        {
            // .XXXO — слева свободно 5 → 5
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.O;
            double p = board.GetLinePotential(7, 6, CellState.X, 5);
            Assert.AreEqual(5, p);
        }

        [TestMethod]
        public void Potential_ThreeInRow_BlockedBothSides_Returns3()
        {
            // OXXXO — оба блока → 3
            var board = new GameBoard(15, 5);
            board[7, 4] = CellState.O;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.O;
            double p = board.GetLinePotential(7, 6, CellState.X, 5);
            Assert.AreEqual(3, p);
        }

        [TestMethod]
        public void Potential_FourInRow_OpenOneSide_Returns5()
        {
            // .XXXX — 5
            var board = new GameBoard(15, 5);
            board[7, 4] = CellState.X;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            double p = board.GetLinePotential(7, 5, CellState.X, 5);
            Assert.AreEqual(5, p);
        }

        [TestMethod]
        public void Potential_FourInRow_BlockedBothSides_Returns4()
        {
            // OXXXXO — мёртвая → 4
            var board = new GameBoard(15, 5);
            board[7, 3] = CellState.O;
            board[7, 4] = CellState.X;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.O;
            double p = board.GetLinePotential(7, 5, CellState.X, 5);
            Assert.AreEqual(4, p);
        }

        [TestMethod]
        public void Potential_Diagonal_Works()
        {
            var board = new GameBoard(15, 5);
            board[5, 5] = CellState.X;
            board[6, 6] = CellState.X;
            board[7, 7] = CellState.X;
            double p = board.GetLinePotential(6, 6, CellState.X, 5);
            Assert.AreEqual(5, p);   // диагональ .XXX.
        }

        [TestMethod]
        public void GetMaxPotential_ReturnsBestAcrossBoard()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[10, 10] = CellState.X;
            double p = board.GetMaxPotential(CellState.X, 5);
            Assert.AreEqual(5, p);
        }

        [TestMethod]
        public void Potential_FourInRow_BlockedOneSide_Returns5()
        {
            // O X X X X .  — справа свободно → 5
            var board = new GameBoard(15, 5);
            board[7, 3] = CellState.O;
            board[7, 4] = CellState.X;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            double p = board.GetLinePotential(7, 5, CellState.X, 5);
            Assert.AreEqual(5, p);
        }
    }
}