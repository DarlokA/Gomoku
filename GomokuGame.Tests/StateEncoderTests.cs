using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.AI;
using GomokuGame.Models;
using System.Collections.Generic;

namespace GomokuGame.Tests
{
    [TestClass]
    public class StateEncoderTests
    {
        [TestMethod]
        public void Constants_AreCorrect()
        {
            Assert.AreEqual(9, StateEncoder.WindowSize);
            Assert.AreEqual(4, StateEncoder.Half);
            Assert.AreEqual(3, StateEncoder.Channels);
            Assert.AreEqual(9 * 9 * 3, StateEncoder.InputSize); // 243
        }

        [TestMethod]
        public void Encode_CenterIsSimulatedMove()
        {
            var board = new GameBoard(15, 5);
            var data = StateEncoder.Encode(board, 7, 7, CellState.X);

            int k = StateEncoder.WindowSize;
            int stride = k * k;
            int centerIdx = StateEncoder.Half * k + StateEncoder.Half;   // 4*9+4 = 40

            Assert.AreEqual(1.0, data[0 * stride + centerIdx]);
            Assert.AreEqual(0.0, data[1 * stride + centerIdx]);
            Assert.AreEqual(0.0, data[2 * stride + centerIdx]);
        }

        [TestMethod]
        public void Encode_AllPositionsCovered()
        {
            var board = new GameBoard(15, 5);

            // Заполняем всё окно 9×9 вокруг (7,7) своими фишками
            for (int r = 3; r <= 11; r++)
                for (int c = 3; c <= 11; c++)
                    board[r, c] = CellState.X;

            var data = StateEncoder.Encode(board, 7, 7, CellState.X);

            int stride = StateEncoder.WindowSize * StateEncoder.WindowSize;

            // Каждая из 81 позиций должна иметь ровно одну 1 в одном из каналов
            for (int i = 0; i < stride; i++)
            {
                double sum = data[0 * stride + i] + data[1 * stride + i] + data[2 * stride + i];
                Assert.AreEqual(1.0, sum, $"Позиция {i} не закодирована");
            }
        }

        [TestMethod]
        public void Encode_EmptyBoard_AllChannelsCorrect()
        {
            var board = new GameBoard(15, 5);
            var data = StateEncoder.Encode(board, 7, 7, CellState.X);

            int k = StateEncoder.WindowSize;
            int stride = k * k;
            int centerIdx = StateEncoder.Half * k + StateEncoder.Half;

            Assert.AreEqual(StateEncoder.InputSize, data.Length);

            for (int i = 0; i < stride; i++)
            {
                if (i == centerIdx)
                {
                    Assert.AreEqual(1.0, data[0 * stride + i]);
                    Assert.AreEqual(0.0, data[1 * stride + i]);
                    Assert.AreEqual(0.0, data[2 * stride + i]);
                }
                else
                {
                    Assert.AreEqual(0.0, data[0 * stride + i]);
                    Assert.AreEqual(0.0, data[1 * stride + i]);
                    Assert.AreEqual(1.0, data[2 * stride + i]);
                }
            }
        }

        [TestMethod]
        public void Encode_RowMajorOrder_IsCorrect()
        {
            var board = new GameBoard(15, 5);

            // X справа от центра: (7, 8) → dr=0, dc=+1
            board[7, 8] = CellState.X;

            var data = StateEncoder.Encode(board, 7, 7, CellState.O);

            int k = StateEncoder.WindowSize;
            int stride = k * k;

            // Построчный обход: idx = (dr+Half)*k + (dc+Half) = 4*9 + 5 = 41
            Assert.AreEqual(1.0, data[1 * stride + 41]);
        }

        [TestMethod]
        public void Encode_DifferentOrientations_ProduceDifferentData()
        {
            var board = new GameBoard(15, 5);

            // X справа от центра: (7, 8)
            board[7, 8] = CellState.X;

            var data0 = StateEncoder.Encode(board, 7, 7, CellState.O, 0);
            var data1 = StateEncoder.Encode(board, 7, 7, CellState.O, 1);
            var data2 = StateEncoder.Encode(board, 7, 7, CellState.O, 2);
            var data3 = StateEncoder.Encode(board, 7, 7, CellState.O, 3);

            int stride = StateEncoder.WindowSize * StateEncoder.WindowSize;

            // Находим индекс, где стоит 1 в канале 1 (чужие — X)
            int idx0 = -1, idx1 = -1, idx2 = -1, idx3 = -1;
            for (int i = 0; i < stride; i++)
            {
                if (data0[1 * stride + i] > 0.5) idx0 = i;
                if (data1[1 * stride + i] > 0.5) idx1 = i;
                if (data2[1 * stride + i] > 0.5) idx2 = i;
                if (data3[1 * stride + i] > 0.5) idx3 = i;
            }

            // X должен быть в разных позициях вектора при разных ориентациях
            Assert.AreNotEqual(idx0, idx1, "Ориентации 0 и 1 должны различаться");
            Assert.AreNotEqual(idx0, idx2, "Ориентации 0 и 2 должны различаться");
            Assert.AreNotEqual(idx0, idx3, "Ориентации 0 и 3 должны различаться");
        }

        [TestMethod]
        public void Debug_CenterIndex()
        {
            var board = new GameBoard(15, 5);
            var data = StateEncoder.Encode(board, 7, 7, CellState.X);

            int k = StateEncoder.WindowSize;
            int stride = k * k;

            // Найти, где в канале 0 стоит 1 (центр)
            int centerIdx = -1;
            for (int i = 0; i < stride; i++)
            {
                if (data[0 * stride + i] > 0.5)
                {
                    centerIdx = i;
                    break;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Center index = {centerIdx}");
            Assert.AreEqual(40, centerIdx, "Центр должен быть по индексу 40 (4*9+4)");
        }
    }
}