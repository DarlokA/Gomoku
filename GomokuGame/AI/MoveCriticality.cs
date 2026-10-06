using System;
using GomokuGame.Models;

namespace GomokuGame.AI
{
    public static class MoveCriticality
    {
        /// <summary>
        /// Если поставить me в (row, col) — собирается ли winLength подряд?
        /// </summary>
        public static bool IsWinningMove(GameBoard board, int row, int col, CellState me, int winLength)
        {
            if (board[row, col] != CellState.Empty) return false;

            board[row, col] = me;
            int len = board.GetLineLengthAt(row, col, me);
            board[row, col] = CellState.Empty;

            return len >= winLength;
        }

        /// <summary>
        /// Есть ли хоть одна клетка, дающая me немедленную победу?
        /// </summary>
        public static bool CouldWinNow(GameBoard board, CellState me, int winLength)
        {
            for (int r = 0; r < board.Size; r++)
                for (int c = 0; c < board.Size; c++)
                    if (board[r, c] == CellState.Empty && IsWinningMove(board, r, c, me, winLength))
                        return true;
            return false;
        }

        /// <summary>
        /// Своя линия после хода me в (row, col).
        /// </summary>
        private static int GetMaxOwnLineAfter(GameBoard board, int row, int col, CellState me)
        {
            if (board[row, col] != CellState.Empty) return 0;

            board[row, col] = me;
            int len = board.GetLineLengthAt(row, col, me);
            board[row, col] = CellState.Empty;

            return len;
        }

        /// <summary>
        /// Самая сильная ЖИВАЯ линия opponent в окне 9×9 вокруг (centerRow, centerCol).
        /// Мёртвые линии (OpenEnds == 0) игнорируются — они не угроза.
        /// Возвращает (Length, OpenEnds, Row, Col) — координаты опорной клетки.
        /// </summary>
        private static (int Length, int OpenEnds, int Row, int Col) FindStrongestOpponentLineInWindow(
            GameBoard board, int centerRow, int centerCol, CellState opponent)
        {
            int half = StateEncoder.Half;
            int bestLen = 0, bestOpen = 0, bestRow = -1, bestCol = -1;

            for (int dr = -half; dr <= half; dr++)
                for (int dc = -half; dc <= half; dc++)
                {
                    int r = centerRow + dr, c = centerCol + dc;
                    if (r < 0 || r >= board.Size || c < 0 || c >= board.Size) continue;
                    if (board[r, c] != opponent) continue;

                    var line = board.GetMaxLineWithOpenEnds(r, c, opponent);

                    // Мёртвая линия — не угроза. Пропускаем сразу,
                    // чтобы она не «перебила» живую линию меньшей длины.
                    if (line.OpenEnds == 0) continue;

                    if (line.Length > bestLen || (line.Length == bestLen && line.OpenEnds > bestOpen))
                    {
                        bestLen = line.Length;
                        bestOpen = line.OpenEnds;
                        bestRow = r;
                        bestCol = c;
                    }
                }

            return (bestLen, bestOpen, bestRow, bestCol);
        }


        /// <summary>
        /// Критичность клетки (row, col) как потенциального хода me.
        /// Диапазон [0.0, 1.0] — это ВЕС для отбора обучающего семпла, не target.
        /// </summary>
        public static double ComputeCriticalityForMove(GameBoard board, int row, int col, CellState me, int winLength)
        {
            if (board[row, col] != CellState.Empty) return 0.0;

            var opponent = me == CellState.X ? CellState.O : CellState.X;

            // 1. Это и есть клетка последнего камня — максимальная критичность.
            if (IsWinningMove(board, row, col, me, winLength))
                return 1.0;

            // 2. Победа доступна где-то ЕЩЁ (не здесь) — эта клетка не нужное место.
            if (CouldWinNow(board, me, winLength))
                return 0.0;

            // 3. Угроза противника от L-2 и выше, взятая строго в окне видимости сети.
            var threat = FindStrongestOpponentLineInWindow(board, row, col, opponent);
            if (threat.Row >= 0 && threat.Length >= winLength - 2 && threat.OpenEnds > 0)
            {
                board[row, col] = me;
                var after = board.GetMaxLineWithOpenEnds(threat.Row, threat.Col, opponent);
                board[row, col] = CellState.Empty;

                // (row, col) реально уменьшил число открытых концов угрозы —
                // значит это один из её концов, корректный защитный ход.
                if (after.OpenEnds < threat.OpenEnds)
                    return 0.9;

                // клетка вообще не относится к этой угрозе
                return 0.0;
            }

            // 4. Своя линия ≥ 50% — усиление, но не критично.
            int ownLen = GetMaxOwnLineAfter(board, row, col, me);
            int ownThreshold = (int)Math.Ceiling(winLength * 0.5);
            if (ownLen >= ownThreshold)
                return 0.3;

            return 0.0;
        }

        /// <summary>
        /// Сеть могла победить, но выбранный ход — не победный.
        /// </summary>
        public static bool MissedWin(GameBoard boardBefore, int chosenRow, int chosenCol, CellState me, int winLength)
            => CouldWinNow(boardBefore, me, winLength)
               && !IsWinningMove(boardBefore, chosenRow, chosenCol, me, winLength);
    }
}