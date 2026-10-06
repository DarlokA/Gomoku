using System;
using System.Collections.Generic;
using GomokuGame.Models;

namespace GomokuGame.AI
{
    /// <summary>
    /// Бот на основе обученной сети, но с фиксированной ориентацией улитки.
    /// Используется для обучения: сеть играет против себя же, но видящей поле
    /// под другим углом. Веса сети НЕ меняются — бот не обучается.
    /// </summary>
    public class RotatingBot
    {
        private readonly AiPlayer _aiPlayer;
        private readonly int _orientation;

        /// <summary>
        /// Ориентация бота: 0=гориз, 1=верт, 2=диаг\, 3=диаг/.
        /// </summary>
        public int Orientation => _orientation;

        public AiPlayer AiPlayer => _aiPlayer;

        public RotatingBot(AiPlayer aiPlayer, int orientation)
        {
            if (orientation < 0 || orientation > 3)
                throw new ArgumentOutOfRangeException(nameof(orientation),
                    "Ориентация должна быть 0..3");

            _aiPlayer = aiPlayer;
            _orientation = orientation;
        }

        /// <summary>
        /// Оценка хода: сеть с ФИКСИРОВАННОЙ ориентацией.
        /// TTA (UseRotatingEvaluation) здесь НЕ используется.
        /// </summary>
        public double EvaluateMove(GameBoard board, int row, int col, CellState me)
        {
            var input = StateEncoder.Encode(board, row, col, me, _orientation);
            var output = AiPlayer.Network.Forward(input);
            return output[0];
        }

        /// <summary>
        /// Выбор хода: перебор пустых клеток, выбор максимальной оценки.
        /// Возвращает null, если ходов нет.
        /// </summary>
        public (int Row, int Col)? ChooseMove(GameBoard board, CellState me)
        {
            var empties = new List<(int Row, int Col)>();
            for (int r = 0; r < board.Size; r++)
                for (int c = 0; c < board.Size; c++)
                    if (board[r, c] == CellState.Empty)
                        empties.Add((r, c));

            if (empties.Count == 0) return null;

            (int Row, int Col)? best = null;
            double bestScore = double.NegativeInfinity;

            foreach (var (r, c) in empties)
            {
                double score = EvaluateMove(board, r, c, me);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = (r, c);
                }
            }

            return best;
        }
    }
}