using System;
using GomokuGame.Models;

namespace GomokuGame.AI
{
    public static class StateEncoder
    {
        /// <summary>
        /// Размер окна. K=9 покрывает winLength до 9.
        /// </summary>
        public const int WindowSize = 9;

        /// <summary>
        /// Полуразмер окна: 4 (то есть -4..+4, всего 9).
        /// </summary>
        public const int Half = WindowSize / 2;

        /// <summary>
        /// Число каналов: [мои][чужие][пусто+граница].
        /// </summary>
        public const int Channels = 3;

        /// <summary>
        /// Размер входа сети: 9*9*3 = 243.
        /// </summary>
        public const int InputSize = WindowSize * WindowSize * Channels;


        //TODO: Убрал улитку
        /// <summary>
        /// Кэшированный порядок обхода по спирали (улитка).
        /// </summary>
        //private static readonly (int dr, int dc)[] _spiralOrder = BuildSpiralOrder();

        //private static (int dr, int dc)[] BuildSpiralOrder()
        /*
        {
            int size = WindowSize;
            int half = Half;
            var result = new (int, int)[size * size];

            result[0] = (0, 0);
            int idx = 1;

            int x = 0, y = 0;
            int dx = 1, dy = 0;
            int stepLength = 1;
            int stepCount = 0;

            while (idx < size * size)
            {
                for (int i = 0; i < stepLength; i++)
                {
                    x += dx;
                    y += dy;

                    if (Math.Abs(x) <= half && Math.Abs(y) <= half)
                    {
                        result[idx++] = (y, x);
                        if (idx >= size * size) break;
                    }
                }

                int newDx = -dy;
                int newDy = dx;
                dx = newDx;
                dy = newDy;

                stepCount++;
                if (stepCount % 2 == 0) stepLength++;
            }

            return result;
        }
        */
        private static readonly (int dr, int dc)[] _traversalOrder = BuildRowMajorOrder();

        private static (int dr, int dc)[] BuildRowMajorOrder()
        {
            int size = WindowSize;
            int half = Half;
            var result = new (int, int)[size * size];

            int idx = 0;
            for (int dr = -half; dr <= half; dr++)
                for (int dc = -half; dc <= half; dc++)
                    result[idx++] = (dr, dc);

            return result;
        }
        //TODO: Конец правки

        /// <summary>
        /// Применяет ориентацию к смещению (dr, dc) в окне.
        /// 0 = горизонталь (без изменений),
        /// 1 = вертикаль (транспонирование),
        /// 2 = диагональ \ ,
        /// 3 = диагональ / .
        /// </summary>
        private static (int, int) ApplySymmetry(int dr, int dc, int orientation)
        {
            return orientation switch
            {
                0 => (dr, dc),        // горизонталь
                1 => (dc, dr),        // вертикаль (транспонирование)
                2 => (dc, -dr),       // диагональ \
                3 => (-dc, -dr),      // диагональ /
                _ => (dr, dc)
            };
        }

        /// <summary>
        /// Кодирует окно 9×9 вокруг клетки (centerRow, centerCol)
        /// с точки зрения игрока 'me'.
        /// orientation: 0=гориз, 1=верт, 2=диаг\, 3=диаг/.
        /// </summary>
        public static double[] Encode(
            GameBoard board,
            int centerRow,
            int centerCol,
            CellState me,
            int orientation = 0)
        {
            var result = new double[InputSize];
            const int channelStride = WindowSize * WindowSize;

            //TODO: Переименовал _spiralOrder -> _traversalOrder
            for (int i = 0; i < _traversalOrder.Length; i++)
            {
                var (dr, dc) = _traversalOrder[i];

                // Применяем ориентацию
                var (rdr, rdc) = ApplySymmetry(dr, dc, orientation);

                bool isCenter = (rdr == 0 && rdc == 0);

                if (isCenter)
                {
                    // Симуляция хода: моя фишка в центре
                    result[0 * channelStride + i] = 1.0;
                    continue;
                }

                int r = centerRow + rdr;
                int c = centerCol + rdc;

                if (r < 0 || r >= board.Size || c < 0 || c >= board.Size)
                {
                    result[2 * channelStride + i] = 1.0;
                    continue;
                }

                var state = board[r, c];

                if (state == CellState.Empty)
                    result[2 * channelStride + i] = 1.0;
                else if (state == me)
                    result[0 * channelStride + i] = 1.0;
                else
                    result[1 * channelStride + i] = 1.0;
            }

            return result;
        }
    }
}