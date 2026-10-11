using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.Models;

namespace GomokuGame.Tests
{
    [TestClass]
    public class GameBoardGapTests
    {
        [TestMethod]
        public void GetMaxLineWithGap_FourStonesOneGap_OpenBothEnds_ReturnsLength4()
        {
            // . X . X X X .  — анкер (7,6), дырка (7,5), слева ещё X на (7,4)
            var board = new GameBoard(15, 5);
            board[7, 4] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;

            var line = board.GetMaxLineWithGap(7, 6, CellState.X);

            Assert.AreEqual(4, line.Length);
            Assert.AreEqual(2, line.OpenEnds);
            Assert.AreEqual(7, line.GapRow);
            Assert.AreEqual(5, line.GapCol);
        }

        [TestMethod]
        public void GetMaxLineWithGap_FourStonesOneGap_BlockedOneEnd_ReturnsOpenEnds1()
        {
            // O X . X X X .  — левый конец закрыт чужим камнем
            var board = new GameBoard(15, 5);
            board[7, 3] = CellState.O;
            board[7, 4] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;

            var line = board.GetMaxLineWithGap(7, 6, CellState.X);

            Assert.AreEqual(4, line.Length);
            Assert.AreEqual(1, line.OpenEnds);
        }

        [TestMethod]
        public void GetMaxLineWithGap_NoGapAnywhere_ReturnsLengthZero()
        {
            // .XXX. — обычная контигуальная тройка, разрыва нигде нет
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;

            var line = board.GetMaxLineWithGap(7, 6, CellState.X);

            Assert.AreEqual(0, line.Length);
            Assert.AreEqual(-1, line.GapRow);
        }

        [TestMethod]
        public void GetMaxLineWithGap_TwoConsecutiveEmptyCells_NotTreatedAsGap()
        {
            // X . . X X — два пустых подряд не считаются "разрывом",
            // иначе любые две далёкие фишки искусственно слипались бы в угрозу
            var board = new GameBoard(15, 5);
            board[7, 4] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;

            var line = board.GetMaxLineWithGap(7, 7, CellState.X);

            Assert.AreEqual(0, line.Length);
        }

        [TestMethod]
        public void GetMaxLineWithGap_Diagonal_DetectsGap()
        {
            // диагональ: X . X X — дырка на (6,6)
            var board = new GameBoard(15, 5);
            board[5, 5] = CellState.X;
            board[7, 7] = CellState.X;
            board[8, 8] = CellState.X;

            var line = board.GetMaxLineWithGap(7, 7, CellState.X);

            Assert.AreEqual(3, line.Length);
            Assert.AreEqual(6, line.GapRow);
            Assert.AreEqual(6, line.GapCol);
        }

        [TestMethod]
        public void GetMaxLineWithGap_EmptyCell_ReturnsLengthZero()
        {
            // вызов от пустой клетки — не относится к player, результат нулевой
            var board = new GameBoard(15, 5);
            board[7, 6] = CellState.X;

            var line = board.GetMaxLineWithGap(7, 7, CellState.X);

            Assert.AreEqual(0, line.Length);
            Assert.AreEqual(-1, line.GapRow);
        }

        [TestMethod]
        public void GetMaxLineWithGap_GapOnBothSidesSimultaneously_NotCombined()
        {
            // X . X . X — анкер в центре, дырки с обеих сторон сразу;
            // метод допускает разрыв только с одной стороны за раз,
            // поэтому эта конфигурация не должна давать length 5
            var board = new GameBoard(15, 5);
            board[7, 4] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 8] = CellState.X;

            var line = board.GetMaxLineWithGap(7, 6, CellState.X);

            Assert.IsTrue(line.Length < 5);
        }
    }
}
