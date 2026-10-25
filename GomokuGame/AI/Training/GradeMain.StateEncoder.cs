using GomokuGame.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GomokuGame.AI.Training
{
    public partial class GradeMain
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
            /// Число каналов: [мои][чужие][пусто][стена].
            /// Пусто и стена разведены по разным каналам — раньше обе ситуации
            /// писались в один и тот же канал, и сеть не могла отличить
            /// «рядом свободно» от «рядом край доски» (отсюда и привычка начинать игру с угла).
            /// </summary>
            public const int Channels = 4;

            /// <summary>
            /// Размер входа сети: 9*9*4 = 324.
            /// </summary>
            public const int InputSize = WindowSize * WindowSize * Channels;


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
                        result[3 * channelStride + i] = 1.0;   // канал стены (новый, был не отделён от "пусто")
                        continue;
                    }

                    var state = board[r, c];

                    if (state == CellState.Empty)
                        result[2 * channelStride + i] = 1.0;   // канал "пусто", номер не менялся
                    else if (state == me)
                        result[0 * channelStride + i] = 1.0;
                    else
                        result[1 * channelStride + i] = 1.0;
                }

                return result;
            }
        }
    }
}
