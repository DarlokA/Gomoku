using System;
using GomokuGame.AI;
using GomokuGame.AI.Training;
using GomokuGame.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GomokuGame.Tests
{
    [TestClass]
    public class LocalTacticalTargetTests
    {
        private const double Tolerance = 1e-12;
        private const int BoardSize = 15;
        private const int Center = BoardSize / 2;

        [DataTestMethod]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        [DataRow(7)]
        public void OwnWinningMove_TargetIsOne(int winLength)
        {
            var move = OwnWinningMove(winLength);

            double target = Compute(move, winLength);

            Assert.AreEqual(1.0, target, Tolerance);
        }

        [DataTestMethod]
        [DataRow(4)]
        [DataRow(5)]
        public void Targets_FollowRequiredPriorityOrder(int winLength)
        {
            double win = Compute(
                OwnWinningMove(winLength), winLength);

            double immediateDefense = Compute(
                BlockImmediateThreat(winLength), winLength);

            double earlyDefense = Compute(
                BlockTwoStonesThreat(winLength), winLength);

            double ownDevelopment = Compute(
                OwnNonWinningDevelopment(winLength), winLength);

            AssertInRange(win);
            AssertInRange(immediateDefense);
            AssertInRange(earlyDefense);
            AssertInRange(ownDevelopment);

            Assert.IsTrue(
                win > immediateDefense,
                "Своя победа должна быть выше срочной защиты.");

            Assert.IsTrue(
                immediateDefense > earlyDefense,
                "Защита от немедленной победы должна быть выше ранней защиты.");

            Assert.IsTrue(
                earlyDefense > ownDevelopment,
                "Защита от угрозы за два камня должна быть выше непобедной атаки.");
        }

        [TestMethod]
        public void Target_DoesNotDependOnGameOutcomeOrHistoryIndex()
        {
            var move = BlockTwoStonesThreat(5);

            var calculator = TrainingTargetFactory.Get(
                AiPlayer.LearningMode.LocalTactical)!;

            var drawContext = Context(
                winLength: 5,
                winner: CellState.Empty,
                lastWinnerIndex: -1);

            var wonContext = Context(
                winLength: 5,
                winner: CellState.X,
                lastWinnerIndex: 100);

            var lostContext = Context(
                winLength: 5,
                winner: CellState.O,
                lastWinnerIndex: 100);

            double draw = calculator.Compute(move, 0, drawContext);
            double won = calculator.Compute(move, 10, wonContext);
            double lost = calculator.Compute(move, 50, lostContext);

            AssertInRange(draw);
            Assert.AreEqual(draw, won, Tolerance);
            Assert.AreEqual(draw, lost, Tolerance);
        }

        [TestMethod]
        public void SameWindow_IsWinningForFive_ButNotForSix()
        {
            // В окне четыре своих камня вокруг пустого центра.
            // После кодирования центральный ход даёт линию длиной 5.
            var move = OwnWinningMove(5);

            double targetForFive = Compute(move, 5);
            double targetForSix = Compute(move, 6);

            Assert.AreEqual(1.0, targetForFive, Tolerance);
            AssertInRange(targetForSix);

            Assert.IsTrue(
                targetForSix < targetForFive,
                "При длине победы 6 линия из пяти ещё не победная.");
        }

        [TestMethod]
        public void E_ThroughLearnFromGame_WithCorrectionOff_UsesWinningTarget()
        {
            var move = OwnWinningMove(5);

            // Диагностическая линейная сеть.
            // При нулевых весах её первый выход равен 0.
            var layer = new Layer(
                StateEncoder.InputSize,
                1,
                ActivationType.Linear,
                new Random(42));

            Array.Clear(layer.W[0], 0, layer.W[0].Length);
            layer.B[0] = 0.0;

            var ai = new AiPlayer(
                new NeuralNetwork(layer),
                new Random(42))
            {
                Mode = AiPlayer.LearningMode.LocalTactical,
                Correction = AiPlayer.CorrectionMode.Off,
                LearningRate = 0.1
            };

            ai.RecordMove(
                stateBefore: move.State,
                row: move.Row,
                col: move.Col,
                player: move.Player,
                lineLengthAfter: move.LineLengthAfter,
                attackPotential: move.AttackPotential,
                opponentPotential: move.OpponentPotential);

            // Намеренно передаём проигрыш X:
            // локальная победная цель E не зависит от этого аргумента.
            double loss = ai.LearnFromGame(CellState.O, 5);

            // Цель 1, исходный выход 0:
            // ошибка 1, обновление смещения при lr=0.1 равно +0.2.
            Assert.AreEqual(1.0, loss, Tolerance);
            Assert.AreEqual(0.2, layer.B[0], Tolerance);

            Assert.AreEqual(0, ai.HistoryCount);
            Assert.AreEqual(1, ai.EpisodesTrained);
            Assert.AreEqual(0, ai.CriticalBuffer.BufferSize);
        }

        [TestMethod]
        public void ClosingOneOfTwoWinningEnds_DoesNotCountAsRescue()
        {
            var board = NewBoard(4);

            board[Center, Center - 3] = CellState.O;
            board[Center, Center - 2] = CellState.O;
            board[Center, Center - 1] = CellState.O;

            // До хода противник может выиграть:
            // в центре либо в Center - 4.
            var move = EncodeMove(board);

            double target = Compute(move, 4);

            Assert.AreEqual(0.0, target, Tolerance);
        }

        [TestMethod]
        public void ClosingOnlyWinningEnd_HasHigherTargetThanFailedRescue()
        {
            var openBoard = NewBoard(4);

            openBoard[Center, Center - 3] = CellState.O;
            openBoard[Center, Center - 2] = CellState.O;
            openBoard[Center, Center - 1] = CellState.O;

            var closedBoard = NewBoard(4);

            closedBoard[Center, Center - 4] = CellState.X;
            closedBoard[Center, Center - 3] = CellState.O;
            closedBoard[Center, Center - 2] = CellState.O;
            closedBoard[Center, Center - 1] = CellState.O;

            double failedRescue = Compute(EncodeMove(openBoard), 4);
            double actualDefense = Compute(EncodeMove(closedBoard), 4);

            Assert.IsTrue(actualDefense > failedRescue);
            Assert.IsTrue(actualDefense < 1.0);
        }

        [TestMethod]
        public void OwnWin_OverridesRemainingOpponentWinningThreat()
        {
            var board = NewBoard(5);

            board[Center, Center - 2] = CellState.X;
            board[Center, Center - 1] = CellState.X;
            board[Center, Center + 1] = CellState.X;
            board[Center, Center + 2] = CellState.X;

            // Другая строка: видимая угроза противника.
            for (int dc = -2; dc <= 1; dc++)
                board[Center + 2, Center + dc] = CellState.O;

            double target = Compute(EncodeMove(board), 5);

            Assert.AreEqual(1.0, target, Tolerance);
        }

        [TestMethod]
        public void OpponentLineWithoutEnoughSpace_IsNotAThreat()
        {
            var board = NewBoard(5);

            board[Center, Center - 4] = CellState.X;

            board[Center, Center - 3] = CellState.O;
            board[Center, Center - 2] = CellState.O;
            board[Center, Center - 1] = CellState.O;

            board[Center, Center + 1] = CellState.X;

            // Контрольная позиция: те же свои камни,
            // но без предполагаемой угрозы противника.
            var baselineBoard = NewBoard(5);

            baselineBoard[Center, Center - 4] = CellState.X;
            baselineBoard[Center, Center + 1] = CellState.X;

            double baselineTarget = Compute(EncodeMove(baselineBoard), 5);
            double actualTarget = Compute(EncodeMove(board), 5);

            // Чужая линия не вмещает победную пятёрку,
            // поэтому защитная оценка не должна появляться.
            // Обычная оценка собственного развития сохраняется.
            Assert.AreEqual(
                baselineTarget,
                actualTarget,
                Tolerance);
        }

        [TestMethod]
        public void Target_IsInvariantUnderEncoderOrientations()
        {
            var board = NewBoard(5);

            board[Center, Center - 3] = CellState.O;
            board[Center, Center - 2] = CellState.O;
            board[Center, Center - 1] = CellState.O;

            double expected = Compute(EncodeMove(board), 5);

            for (int orientation = 0; orientation < 4; orientation++)
            {
                var state = StateEncoder.Encode(
                    board,
                    Center,
                    Center,
                    CellState.X,
                    orientation);

                var move = new MoveRecord(
                    state,
                    Center,
                    Center,
                    CellState.X);

                double actual = Compute(move, 5);

                Assert.AreEqual(
                    expected,
                    actual,
                    Tolerance,
                    $"Ориентация: {orientation}");
            }
        }

        [TestMethod]
        public void Compute_DoesNotModifyEncodedState()
        {
            var move = BlockTwoStonesThreat(5);
            var original = (double[])move.State.Clone();

            Compute(move, 5);

            CollectionAssert.AreEqual(original, move.State);
        }

        private static MoveRecord OwnWinningMove(int winLength)
        {
            var board = NewBoard(winLength);

            // Победная линия проходит через центр.
            // Камни располагаем с двух сторон,
            // чтобы примеры помещались в окно 9x9.
            int leftCount = winLength / 2;
            int rightCount = winLength - 1 - leftCount;

            for (int i = 1; i <= leftCount; i++)
                board[Center, Center - i] = CellState.X;

            for (int i = 1; i <= rightCount; i++)
                board[Center, Center + i] = CellState.X;

            return EncodeMove(board);
        }

        private static MoveRecord BlockImmediateThreat(int winLength)
        {
            var board = NewBoard(winLength);

            // Противник может победить, заполнив центральный разрыв.
            // Внешние концы закрыты нашими камнями:
            // другого победного завершения этой линии нет.
            int leftCount = winLength / 2;
            int rightCount = winLength - 1 - leftCount;

            for (int i = 1; i <= leftCount; i++)
                board[Center, Center - i] = CellState.O;

            for (int i = 1; i <= rightCount; i++)
                board[Center, Center + i] = CellState.O;

            board[Center, Center - leftCount - 1] = CellState.X;
            board[Center, Center + rightCount + 1] = CellState.X;

            return EncodeMove(board);
        }

        private static MoveRecord BlockTwoStonesThreat(int winLength)
        {
            var board = NewBoard(winLength);

            // Открытая линия противника:
            // до победной длины не хватает двух камней.
            // Центр — один из её свободных концов.
            for (int i = 1; i <= winLength - 2; i++)
                board[Center, Center - i] = CellState.O;

            return EncodeMove(board);
        }

        private static MoveRecord OwnNonWinningDevelopment(int winLength)
        {
            var board = NewBoard(winLength);

            // После центрального хода своей линии
            // всё ещё будет не хватать одного камня до победы.
            for (int i = 1; i <= winLength - 2; i++)
                board[Center, Center - i] = CellState.X;

            return EncodeMove(board);
        }

        private static GameBoard NewBoard(int winLength)
        {
            return new GameBoard(BoardSize, winLength);
        }

        private static MoveRecord EncodeMove(GameBoard board)
        {
            var state = StateEncoder.Encode(
                board,
                Center,
                Center,
                CellState.X);

            // Намеренно не заполняем глобальные потенциалы D:
            // E должен оценивать доступное сети локальное окно.
            return new MoveRecord(
                state,
                Center,
                Center,
                CellState.X);
        }

        private static double Compute(
            MoveRecord move,
            int winLength)
        {
            var calculator = TrainingTargetFactory.Get(
                AiPlayer.LearningMode.LocalTactical)!;

            return calculator.Compute(
                move,
                0,
                Context(winLength));
        }

        private static TrainingContext Context(
            int winLength,
            CellState winner = CellState.Empty,
            int lastWinnerIndex = -1)
        {
            return new TrainingContext(
                winner: winner,
                lastWinnerIndex: lastWinnerIndex,
                winLength: winLength,
                discount: 0.9,
                attackWeight: 0.4,
                defenseWeight: 0.6);
        }

        private static void AssertInRange(double target)
        {
            Assert.IsTrue(
                double.IsFinite(target)
                && target >= 0.0
                && target <= 1.0,
                $"Цель должна находиться в диапазоне [0; 1], получено: {target}");
        }
    }
}