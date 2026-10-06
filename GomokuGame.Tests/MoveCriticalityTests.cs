using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.AI;
using GomokuGame.Models;

namespace GomokuGame.Tests
{
    [TestClass]
    public class MoveCriticalityTests
    {
        // ================================================================
        // IsWinningMove
        // ================================================================

        [TestMethod]
        public void IsWinningMove_FourInRow_OpenEnd_ReturnsTrue()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;
            // . X X X X .  — ставим X в (7, 4) или (7, 9)

            Assert.IsTrue(MoveCriticality.IsWinningMove(board, 7, 4, CellState.X, 5));
            Assert.IsTrue(MoveCriticality.IsWinningMove(board, 7, 9, CellState.X, 5));
        }

        [TestMethod]
        public void IsWinningMove_FourInRow_BlockedOneSide_ReturnsTrue()
        {
            var board = new GameBoard(15, 5);
            board[7, 4] = CellState.O;
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;
            // O X X X X .  — ставим X в (7, 9) — победа

            Assert.IsTrue(MoveCriticality.IsWinningMove(board, 7, 9, CellState.X, 5));
        }

        [TestMethod]
        public void IsWinningMove_ThreeInRow_ReturnsFalse()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;

            Assert.IsFalse(MoveCriticality.IsWinningMove(board, 7, 8, CellState.X, 5));
        }

        [TestMethod]
        public void IsWinningMove_OccupiedCell_ReturnsFalse()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;

            Assert.IsFalse(MoveCriticality.IsWinningMove(board, 7, 5, CellState.X, 5));
        }

        // ================================================================
        // CouldWinNow
        // ================================================================

        [TestMethod]
        public void CouldWinNow_FourInRow_ReturnsTrue()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;

            Assert.IsTrue(MoveCriticality.CouldWinNow(board, CellState.X, 5));
        }

        [TestMethod]
        public void CouldWinNow_ThreeInRow_ReturnsFalse()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;

            Assert.IsFalse(MoveCriticality.CouldWinNow(board, CellState.X, 5));
        }

        // ================================================================
        // MissedWin
        // ================================================================

        [TestMethod]
        public void MissedWin_WinAvailable_ChoseOther_ReturnsTrue()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;
            // Победа доступна в (7, 4) или (7, 9). Сеть выбрала (0, 0).

            Assert.IsTrue(MoveCriticality.MissedWin(board, 0, 0, CellState.X, 5));
        }

        [TestMethod]
        public void MissedWin_WinAvailable_ChoseWinning_ReturnsFalse()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;

            Assert.IsFalse(MoveCriticality.MissedWin(board, 7, 9, CellState.X, 5));
        }

        [TestMethod]
        public void MissedWin_NoWinAvailable_ReturnsFalse()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;

            Assert.IsFalse(MoveCriticality.MissedWin(board, 0, 0, CellState.X, 5));
        }

        // ================================================================
        // ComputeCriticalityForMove
        // ================================================================

        [TestMethod]
        public void Criticality_WinningMove_ReturnsOne()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;

            double c = MoveCriticality.ComputeCriticalityForMove(board, 7, 9, CellState.X, 5);
            Assert.AreEqual(1.0, c, 1e-6);
        }

        [TestMethod]
        public void Criticality_WinAvailable_ButNotHere_ReturnsZero()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;
            board[7, 7] = CellState.X;
            board[7, 8] = CellState.X;
            // Победа в (7, 4) или (7, 9). Оцениваем (0, 0).

            double c = MoveCriticality.ComputeCriticalityForMove(board, 0, 0, CellState.X, 5);
            Assert.AreEqual(0.0, c, 1e-6);
        }

        [TestMethod]
        public void Criticality_BlockingMove_L1_Returns09()
        {
            var board = new GameBoard(15, 5);
            // O имеет 4 в ряд: (7, 5)-(7, 8). Блокировка в (7, 4) или (7, 9).
            board[7, 5] = CellState.O;
            board[7, 6] = CellState.O;
            board[7, 7] = CellState.O;
            board[7, 8] = CellState.O;

            double c = MoveCriticality.ComputeCriticalityForMove(board, 7, 9, CellState.X, 5);
            Assert.AreEqual(0.9, c, 1e-6);
        }

        [TestMethod]
        public void Criticality_IrrelevantCell_ReturnsZero()
        {
            var board = new GameBoard(15, 5);
            board[7, 5] = CellState.O;
            board[7, 6] = CellState.O;
            board[7, 7] = CellState.O;
            board[7, 8] = CellState.O;
            // Блокировка в (7, 4) или (7, 9). Оцениваем (0, 0) — не касается.

            double c = MoveCriticality.ComputeCriticalityForMove(board, 0, 0, CellState.X, 5);
            Assert.AreEqual(0.0, c, 1e-6);
        }

        [TestMethod]
        public void Criticality_OwnLine50Percent_Returns03()
        {
            var board = new GameBoard(15, 5);
            // X имеет 2 в ряд (ниже порога 3), ставим 3-ю.
            board[7, 5] = CellState.X;
            board[7, 6] = CellState.X;

            double c = MoveCriticality.ComputeCriticalityForMove(board, 7, 7, CellState.X, 5);
            Assert.AreEqual(0.3, c, 1e-6);
        }

        [TestMethod]
        public void Criticality_DeadOpponentLine_Ignored()
        {
            var board = new GameBoard(15, 5);
            // Мёртвая линия O: 4 в ряд, оба конца заблокированы.
            board[7, 4] = CellState.X;
            board[7, 5] = CellState.O;
            board[7, 6] = CellState.O;
            board[7, 7] = CellState.O;
            board[7, 8] = CellState.O;
            board[7, 9] = CellState.X;

            // Оцениваем ход X в (0, 0) — не касается мёртвой линии.
            // Мёртвая линия не должна дать criticality 0.9.
            double c = MoveCriticality.ComputeCriticalityForMove(board, 0, 0, CellState.X, 5);
            Assert.AreEqual(0.0, c, 1e-6);
        }

        [TestMethod]
        public void Criticality_AliveShortLine_NotOverriddenByDeadLongLine()
        {
            var board = new GameBoard(15, 5);
            // Мёртвая линия O: 4 в ряд, оба конца заблокированы.
            board[7, 4] = CellState.X;
            board[7, 5] = CellState.O;
            board[7, 6] = CellState.O;
            board[7, 7] = CellState.O;
            board[7, 8] = CellState.O;
            board[7, 9] = CellState.X;

            // Живая линия O: 3 в ряд, оба конца открыты.
            board[10, 5] = CellState.O;
            board[10, 6] = CellState.O;
            board[10, 7] = CellState.O;

            // Оцениваем блокировку живой линии: (10, 4) — закрывает левый конец.
            // Живая линия длиной 3 (L-2 при winLength=5) — критично.
            double c = MoveCriticality.ComputeCriticalityForMove(board, 10, 4, CellState.X, 5);
            Assert.AreEqual(0.9, c, 1e-6);
        }
    }
}
