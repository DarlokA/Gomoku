using System;
using System.Collections.Generic;

namespace GomokuGame.Models
{
    public class GameBoard
    {
        private readonly CellState[,] _cells;

        public int Size { get; }
        public int WinLength { get; }

        public GameBoard(int size, int winLength)
        {
            if (size < 5 || size > 30) throw new ArgumentOutOfRangeException(nameof(size));
            if (winLength < 3 || winLength > size) throw new ArgumentOutOfRangeException(nameof(winLength));

            Size = size;
            WinLength = winLength;
            _cells = new CellState[size, size];
        }

        public CellState this[int row, int col]
        {
            get => _cells[row, col];
            set => _cells[row, col] = value;
        }

        public void Reset() => Array.Clear(_cells, 0, _cells.Length);

        /// <summary>
        /// Проверяет победу и, если она есть, возвращает список клеток
        /// выигрышной линии. Иначе — null.
        /// </summary>
        public List<(int Row, int Col)>? GetWinningLine(int row, int col, CellState player)
        {
            int[][] directions =
            {
                new[] { 0, 1 },   // горизонталь
                new[] { 1, 0 },   // вертикаль
                new[] { 1, 1 },   // диагональ \
                new[] { 1, -1 }   // диагональ /
            };

            foreach (var dir in directions)
            {
                var line = new List<(int, int)> { (row, col) };

                // в одну сторону
                CollectInDirection(row, col, dir[0], dir[1], player, line);
                // в другую
                CollectInDirection(row, col, -dir[0], -dir[1], player, line);

                if (line.Count >= WinLength)
                {
                    // На всякий случай отсортируем — по строке, потом по столбцу
                    line.Sort((a, b) =>
                        a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1)
                                           : a.Item2.CompareTo(b.Item2));
                    return line;
                }
            }

            return null;
        }

        private void CollectInDirection(int row, int col, int dRow, int dCol,
                                        CellState player, List<(int, int)> line)
        {
            int r = row + dRow;
            int c = col + dCol;
            while (r >= 0 && r < Size && c >= 0 && c < Size && _cells[r, c] == player)
            {
                line.Add((r, c));
                r += dRow;
                c += dCol;
            }
        }

        public bool IsFull()
        {
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    if (_cells[r, c] == CellState.Empty) return false;
            return true;
        }

        /// <summary>
        /// Для клетки (row, col) с фишкой player находит направление максимальной линии
        /// через эту клетку и возвращает её длину плюс число открытых (свободных) концов (0..2).
        /// </summary>
        public (int Length, int OpenEnds, int DRow, int DCol) GetMaxLineWithOpenEnds(int row, int col, CellState player)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size) return (0, 0, 0, 0);
            if (_cells[row, col] != player) return (0, 0, 0, 0);

            int[][] dirs = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, -1 } };
            int bestLen = 0, bestOpen = 0, bestDr = 0, bestDc = 0;

            foreach (var d in dirs)
            {
                int dRow = d[0], dCol = d[1];
                int leftCount = CountDir(row, col, -dRow, -dCol, player);
                int rightCount = CountDir(row, col, dRow, dCol, player);
                int len = 1 + leftCount + rightCount;

                int lr = row - dRow * (leftCount + 1), lc = col - dCol * (leftCount + 1);
                bool leftOpen = lr >= 0 && lr < Size && lc >= 0 && lc < Size && _cells[lr, lc] == CellState.Empty;

                int rr = row + dRow * (rightCount + 1), rc = col + dCol * (rightCount + 1);
                bool rightOpen = rr >= 0 && rr < Size && rc >= 0 && rc < Size && _cells[rr, rc] == CellState.Empty;

                int openEnds = (leftOpen ? 1 : 0) + (rightOpen ? 1 : 0);

                if (len > bestLen || (len == bestLen && openEnds > bestOpen))
                {
                    bestLen = len; bestOpen = openEnds; bestDr = dRow; bestDc = dCol;
                }
            }

            return (bestLen, bestOpen, bestDr, bestDc);
        }

        /// <summary>
        /// Длина максимальной непрерывной линии фишек player, проходящей через (row, col).
        /// Если клетка не принадлежит player — возвращает 0.
        /// </summary>
        public int GetLineLengthAt(int row, int col, CellState player)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size) return 0;
            if (_cells[row, col] != player) return 0;

            int[][] dirs =
            {
        new[] { 0, 1 },
        new[] { 1, 0 },
        new[] { 1, 1 },
        new[] { 1, -1 }
    };

            int maxLen = 1;
            foreach (var d in dirs)
            {
                int len = 1;
                len += CountDir(row, col, d[0], d[1], player);
                len += CountDir(row, col, -d[0], -d[1], player);
                if (len > maxLen) maxLen = len;
            }
            return maxLen;
        }

        private int CountDir(int row, int col, int dRow, int dCol, CellState player)
        {
            int count = 0;
            int r = row + dRow;
            int c = col + dCol;
            while (r >= 0 && r < Size && c >= 0 && c < Size && _cells[r, c] == player)
            {
                count++;
                r += dRow;
                c += dCol;
            }
            return count;
        }

        /// <summary>
        /// Взвешенный потенциал линии через клетку (row, col): насколько опасна/перспективна
        /// линия player, проходящая через эту клетку, с учётом открытости концов и того,
        /// сколько ходов осталось до победы.
        ///
        /// Правила веса:
        ///  - линию физически невозможно дотянуть до winLength (закрыта с обоих концов
        ///    или упирается в края с обеих сторон) — потенциал 0, линии как будто нет;
        ///  - до победы остался 1 ход (need == 1) — вес всегда максимальный (5.0),
        ///    независимо от того, закрыт второй конец или нет: достаточно, что ход
        ///    в единственную свободную клетку уже даёт winLength;
        ///  - до победы остаётся 2 и более ходов — вес снижается по уровням срочности,
        ///    и линия, закрытая с одного конца, весит вдвое меньше полностью открытой
        ///    линии того же уровня.
        /// </summary>
        public double GetLinePotential(int row, int col, CellState player, int winLength)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size) return 0.0;
            if (_cells[row, col] != player) return 0.0;

            int[][] dirs =
            {
                new[] { 0, 1 },
                new[] { 1, 0 },
                new[] { 1, 1 },
                new[] { 1, -1 }
            };

            double best = 0.0;

            foreach (var d in dirs)
            {
                int dRow = d[0], dCol = d[1];

                // считаем, сколько своих фишек подряд влево и сколько свободных клеток за ними
                int leftOwn = 0;
                int r = row - dRow, c = col - dCol;
                while (r >= 0 && r < Size && c >= 0 && c < Size && _cells[r, c] == player)
                {
                    leftOwn++;
                    r -= dRow;
                    c -= dCol;
                }
                int leftFree = 0;
                int rr = r, cc = c;
                while (rr >= 0 && rr < Size && cc >= 0 && cc < Size && _cells[rr, cc] == CellState.Empty)
                {
                    leftFree++;
                    rr -= dRow;
                    cc -= dCol;
                }

                // то же самое вправо
                int rightOwn = 0;
                r = row + dRow; c = col + dCol;
                while (r >= 0 && r < Size && c >= 0 && c < Size && _cells[r, c] == player)
                {
                    rightOwn++;
                    r += dRow;
                    c += dCol;
                }
                int rightFree = 0;
                rr = r; cc = c;
                while (rr >= 0 && rr < Size && cc >= 0 && cc < Size && _cells[rr, cc] == CellState.Empty)
                {
                    rightFree++;
                    rr += dRow;
                    cc += dCol;
                }

                int n = 1 + leftOwn + rightOwn;
                if (n < 2) continue; // одиночная фишка без соседей по этому направлению — не линия

                int need = winLength - n;
                double weighted;

                if (need <= 0)
                {
                    // уже готовая линия нужной длины или длиннее
                    weighted = n;
                }
                else
                {
                    int maxReach = n + leftFree + rightFree;
                    if (maxReach < winLength)
                    {
                        // линию никогда не дотянуть до победы — угрозы нет
                        weighted = 0.0;
                    }
                    else if (need == 1)
                    {
                        // один ход до победы: свободная клетка гарантированно есть
                        // (иначе maxReach < winLength сработал бы выше) — закрытость
                        // второго конца тут ни на что не влияет.
                        weighted = 5.0;
                    }
                    else
                    {
                        int openEnds = (leftFree > 0 ? 1 : 0) + (rightFree > 0 ? 1 : 0);

                        double tierBase = need switch
                        {
                            2 => 3.0,
                            3 => 1.5,
                            _ => 1.0
                        };

                        weighted = openEnds == 2 ? tierBase : tierBase / 2.0;
                    }
                }

                if (weighted > best) best = weighted;
            }

            return best;
        }

        /// <summary>
        /// Максимальный потенциал игрока по всей доске.
        /// </summary>
        public double GetMaxPotential(CellState player, int winLength)
        {
            double max = 0;
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                {
                    if (_cells[r, c] != player) continue;
                    double p = GetLinePotential(r, c, player, winLength);
                    if (p > max) max = p;
                }
            return max;
        }

        public int GetMoveCount()
        {
            int count = 0;
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    if (_cells[r, c] != CellState.Empty)
                        count++;
            return count;
        }

        /// <summary>
        /// Псевдографическое представление доски.
        /// X — фишки X, O — фишки O, . — пусто.
        /// </summary>
        public string ToAsciiString(IReadOnlyList<(int Row, int Col)>? winLine = null)
        {
            var sb = new System.Text.StringBuilder();
            var winSet = winLine != null
                ? new HashSet<(int, int)>(winLine)
                : null;

            // Заголовок колонок
            sb.Append("    ");
            for (int c = 0; c < Size; c++)
                sb.Append((c % 10).ToString()).Append(' ');
            sb.AppendLine();

            // Верхняя рамка
            sb.Append("   +");
            for (int c = 0; c < Size; c++)
                sb.Append("--");
            sb.AppendLine("+");

            for (int r = 0; r < Size; r++)
            {
                sb.Append((r % 10).ToString()).Append(" |");
                for (int c = 0; c < Size; c++)
                {
                    char ch = _cells[r, c] switch
                    {
                        CellState.X => 'X',
                        CellState.O => 'O',
                        _ => '.'
                    };

                    // Подсветка выигрышной линии
                    if (winSet != null && winSet.Contains((r, c)))
                        ch = '*';

                    sb.Append(' ').Append(ch);
                }
                sb.AppendLine(" |");
            }

            // Нижняя рамка
            sb.Append("   +");
            for (int c = 0; c < Size; c++)
                sb.Append("--");
            sb.AppendLine("+");

            return sb.ToString();
        }
    }
}