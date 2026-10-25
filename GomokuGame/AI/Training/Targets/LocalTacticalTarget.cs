using System;

namespace GomokuGame.AI.Training.Targets
{
    /// <summary>
    /// E: локальная тактическая оценка центрального хода.
    ///
    /// Используется только окно MoveRecord.State.
    /// Исход партии, индекс хода и глобальные потенциалы
    /// на целевое значение не влияют.
    ///
    /// Приоритеты:
    /// 1. Собственная немедленная победа.
    /// 2. Если остаётся видимая немедленная победа противника,
    ///    ход получает минимальную оценку.
    /// 3. Блокировка единственной видимой немедленной угрозы.
    /// 4. Блокировка варианта, которому не хватает двух камней.
    /// 5. Непобедное развитие своей линии.
    ///
    /// Анализируются только полностью видимые отрезки.
    /// Неизвестное продолжение за границей окна не достраивается.
    /// </summary>
    internal sealed class LocalTacticalTarget : ITrainingTarget
    {
        private const double WinningTarget = 1.0;
        private const double ImmediateDefenseTarget = 0.95;
        private const double TwoStonesDefenseTarget = 0.85;
        private const double MaxDevelopmentTarget = 0.70;
        private const double UnsafeTarget = 0.0;

        private static readonly (int Dr, int Dc)[] Directions =
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        private enum LocalCell
        {
            Empty,
            Own,
            Opponent,
            Wall
        }

        public double Compute(
            MoveRecord move,
            int index,
            TrainingContext context)
        {
            if (move == null)
                throw new ArgumentNullException(nameof(move));

            int winLength = context.WinLength;

            // Более длинная победная линия целиком
            // не помещается ни в одно направление окна.
            // Не выдаём фиктивную цель для неподдержанного случая.
            if (winLength < 3
                || winLength > StateEncoder.WindowSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(context),
                    $"Для локального расчёта E длина победной линии " +
                    $"должна быть от 3 до {StateEncoder.WindowSize}.");
            }

            var cells = Decode(move.State);
            int center = StateEncoder.Half;

            if (cells[center, center] != LocalCell.Own)
            {
                throw new ArgumentException(
                    "В закодированном окне центральная клетка " +
                    "должна содержать свой камень.",
                    nameof(move));
            }

            // Окно уже содержит результат нашего центрального хода.
            int ownStones = GetBestOwnWindow(cells, winLength);

            if (ownStones == winLength)
                return WinningTarget;

            // Восстанавливаем положение ДО центрального хода.
            // Изменяется только наша локальная копия, не move.State.
            cells[center, center] = LocalCell.Empty;

            var threats = AnalyzeOpponent(cells, winLength);

            // Если противник всё ещё может выиграть в другой клетке,
            // блокировка центра не спасает.
            if (threats.OtherImmediate)
                return UnsafeTarget;

            if (threats.BlocksImmediate)
                return ImmediateDefenseTarget;

            if (threats.BlocksTwoStones)
                return TwoStonesDefenseTarget;

            // Отдельный нижний диапазон для собственной атаки.
            // Один свой камень -> 0.
            // До победы остался один камень -> 0.70.
            if (ownStones <= 1)
                return 0.0;

            return MaxDevelopmentTarget
                * (ownStones - 1)
                / (winLength - 2);
        }

        /// <summary>
        /// Раскодирует каналы StateEncoder:
        /// [свои][чужие][пусто][стена].
        ///
        /// Проверяет штатный one-hot формат.
        /// </summary>
        private static LocalCell[,] Decode(double[] state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (state.Length != StateEncoder.InputSize)
            {
                throw new ArgumentException(
                    $"Ожидался вход длиной {StateEncoder.InputSize}, " +
                    $"получено {state.Length}.",
                    nameof(state));
            }

            int size = StateEncoder.WindowSize;
            int stride = size * size;
            var cells = new LocalCell[size, size];

            for (int i = 0; i < stride; i++)
            {
                int activeChannel = -1;

                for (int channel = 0;
                     channel < StateEncoder.Channels;
                     channel++)
                {
                    double value = state[channel * stride + i];

                    if (value == 0.0)
                        continue;

                    if (value != 1.0 || activeChannel >= 0)
                    {
                        throw new ArgumentException(
                            "Окно должно иметь one-hot кодировку: " +
                            "ровно один активный канал на клетку.",
                            nameof(state));
                    }

                    activeChannel = channel;
                }

                cells[i / size, i % size] = activeChannel switch
                {
                    0 => LocalCell.Own,
                    1 => LocalCell.Opponent,
                    2 => LocalCell.Empty,
                    3 => LocalCell.Wall,

                    _ => throw new ArgumentException(
                        "В окне обнаружена клетка без активного канала.",
                        nameof(state))
                };
            }

            return cells;
        }

        /// <summary>
        /// Максимальное число своих камней в полностью видимом
        /// отрезке победной длины, проходящем через центр.
        ///
        /// Чужой камень или стена делают отрезок непригодным.
        /// Пустые клетки допускаются.
        /// </summary>
        private static int GetBestOwnWindow(
            LocalCell[,] cells,
            int winLength)
        {
            int center = StateEncoder.Half;
            int best = 0;

            foreach (var direction in Directions)
            {
                // Центральная клетка может занимать любую позицию
                // внутри рассматриваемого отрезка.
                for (int offset = 0; offset < winLength; offset++)
                {
                    int startRow = center - offset * direction.Dr;
                    int startCol = center - offset * direction.Dc;

                    int endRow =
                        startRow + (winLength - 1) * direction.Dr;

                    int endCol =
                        startCol + (winLength - 1) * direction.Dc;

                    if (!IsInside(startRow, startCol)
                        || !IsInside(endRow, endCol))
                    {
                        continue;
                    }

                    int own = 0;
                    bool blocked = false;

                    for (int k = 0; k < winLength; k++)
                    {
                        var cell = cells[
                            startRow + k * direction.Dr,
                            startCol + k * direction.Dc];

                        if (cell == LocalCell.Opponent
                            || cell == LocalCell.Wall)
                        {
                            blocked = true;
                            break;
                        }

                        if (cell == LocalCell.Own)
                            own++;
                    }

                    if (!blocked && own > best)
                        best = own;
                }
            }

            return best;
        }

        /// <summary>
        /// Анализирует положение до центрального хода.
        ///
        /// BlocksImmediate:
        /// центр является клеткой немедленной победы противника.
        ///
        /// OtherImmediate:
        /// у противника есть другая видимая клетка немедленной победы
        /// либо уже собранная видимая победная линия.
        ///
        /// BlocksTwoStones:
        /// центр входит в отрезок с двумя пустыми клетками,
        /// остальные клетки которого принадлежат противнику.
        /// </summary>
        private static (
            bool BlocksImmediate,
            bool OtherImmediate,
            bool BlocksTwoStones)
            AnalyzeOpponent(LocalCell[,] cells, int winLength)
        {
            int size = StateEncoder.WindowSize;
            int center = StateEncoder.Half;

            bool blocksImmediate = false;
            bool otherImmediate = false;
            bool blocksTwoStones = false;

            foreach (var direction in Directions)
            {
                for (int row = 0; row < size; row++)
                {
                    for (int col = 0; col < size; col++)
                    {
                        int endRow =
                            row + (winLength - 1) * direction.Dr;

                        int endCol =
                            col + (winLength - 1) * direction.Dc;

                        if (!IsInside(endRow, endCol))
                            continue;

                        bool blocked = false;
                        bool containsCenter = false;

                        int emptyCount = 0;
                        int emptyRow = -1;
                        int emptyCol = -1;

                        for (int k = 0; k < winLength; k++)
                        {
                            int r = row + k * direction.Dr;
                            int c = col + k * direction.Dc;
                            var cell = cells[r, c];

                            if (cell == LocalCell.Own
                                || cell == LocalCell.Wall)
                            {
                                blocked = true;
                                break;
                            }

                            if (r == center && c == center)
                                containsCenter = true;

                            if (cell == LocalCell.Empty)
                            {
                                emptyCount++;
                                emptyRow = r;
                                emptyCol = c;
                            }
                        }

                        if (blocked)
                            continue;

                        if (emptyCount == 0)
                        {
                            otherImmediate = true;
                        }
                        else if (emptyCount == 1)
                        {
                            bool completionIsCenter =
                                emptyRow == center
                                && emptyCol == center;

                            if (completionIsCenter)
                                blocksImmediate = true;
                            else
                                otherImmediate = true;
                        }
                        else if (emptyCount == 2 && containsCenter)
                        {
                            blocksTwoStones = true;
                        }
                    }
                }
            }

            return (
                blocksImmediate,
                otherImmediate,
                blocksTwoStones);
        }

        private static bool IsInside(int row, int col)
        {
            return row >= 0
                && row < StateEncoder.WindowSize
                && col >= 0
                && col < StateEncoder.WindowSize;
        }
    }
}