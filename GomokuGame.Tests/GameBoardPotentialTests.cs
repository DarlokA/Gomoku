using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.Models;

namespace GomokuGame.Tests
{
    [TestClass]
    public class GameBoardPotentialTests
    {

        [TestMethod]
        public void Potential_ThreeInRow_OpenBothSides_Returns3()
        {
            // .XXX. — need=2, оба конца открыты → tier(2) = 3.0
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            double p = board.GetLinePotential(7, 6, CellState.X, 5);
            Assert.AreEqual(3.0, p);
        }

        [TestMethod]
        public void Potential_ThreeInRow_BlockedRight_Returns1_5()
        {
            // .XXXO — need=2, один конец закрыт → tier(2)/2 = 1.5
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.O;
            double p = board.GetLinePotential(7, 6, CellState.X, 5);
            Assert.AreEqual(1.5, p);
        }

        [TestMethod]
        public void Potential_ThreeInRow_BlockedBothSides_Returns0()
        {
            // OXXXO — maxReach=3 < winLength=5, дотянуть невозможно → 0
            var board = new GameBoard(15, 5);
            board[7, 4] = CellState.O;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.O;
            double p = board.GetLinePotential(7, 6, CellState.X, 5);
            Assert.AreEqual(0.0, p);
        }

        [TestMethod]
        public void Potential_FourInRow_OpenOneSide_Returns5()
        {
            // .XXXX — need=1 → всегда 5.0
            var board = new GameBoard(15, 5);
            board[7, 4] = CellState.X;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            double p = board.GetLinePotential(7, 5, CellState.X, 5);
            Assert.AreEqual(5.0, p);
        }

        [TestMethod]
        public void Potential_FourInRow_BlockedBothSides_Returns0()
        {
            // OXXXXO — мёртвая четвёрка: maxReach=4 < winLength=5 → 0
            var board = new GameBoard(15, 5);
            board[7, 3] = CellState.O;
            board[7, 4] = CellState.X;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.O;
            double p = board.GetLinePotential(7, 5, CellState.X, 5);
            Assert.AreEqual(0.0, p);
        }

        [TestMethod]
        public void Potential_Diagonal_Works()
        {
            // диагональ .XXX. — need=2, оба конца открыты → 3.0
            var board = new GameBoard(15, 5);
            board[5, 5] = CellState.X;
            board[6, 6] = CellState.X;
            board[7, 7] = CellState.X;
            double p = board.GetLinePotential(6, 6, CellState.X, 5);
            Assert.AreEqual(3.0, p);
        }

        [TestMethod]
        public void GetMaxPotential_ReturnsBestAcrossBoard()
        {
            // лучший результат на доске — открытая тройка (3.0);
            // изолированная фишка (10,10) не формирует линию (n=1 во всех направлениях)
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[10, 10] = CellState.X;
            double p = board.GetMaxPotential(CellState.X, 5);
            Assert.AreEqual(3.0, p);
        }

        [TestMethod]
        public void Potential_FourInRow_BlockedOneSide_Returns5()
        {
            // O X X X X . — need=1, правило «независимо от второго конца» → 5.0
            var board = new GameBoard(15, 5);
            board[7, 3] = CellState.O;
            board[7, 4] = CellState.X;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            double p = board.GetLinePotential(7, 5, CellState.X, 5);
            Assert.AreEqual(5.0, p);
        }

        [TestMethod]
        public void Potential_TwoInRow_OpenBothSides_Returns1_5()
        {
            // .XX. — n=2, need=3, оба конца открыты → tier(3) = 1.5
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            double p = board.GetLinePotential(7, 5, CellState.X, 5);
            Assert.AreEqual(1.5, p);
        }

        [TestMethod]
        public void Potential_TwoInRow_BlockedOneSide_Returns0_75()
        {
            // .XXO — n=2, need=3, один конец закрыт → tier(3)/2 = 0.75
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.O;
            double p = board.GetLinePotential(7, 5, CellState.X, 5);
            Assert.AreEqual(0.75, p);
        }

        [TestMethod]
        public void Potential_TwoInRow_BlockedBothSides_Returns0()
        {
            // OXXO — maxReach=2 < winLength=5, дотянуть невозможно → 0
            var board = new GameBoard(15, 5);
            board[7, 4] = CellState.O;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.O;
            double p = board.GetLinePotential(7, 5, CellState.X, 5);
            Assert.AreEqual(0.0, p);
        }
    }
}