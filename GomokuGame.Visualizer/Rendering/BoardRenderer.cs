using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GomokuGame.Visualizer.Rendering
{
    public enum LayerMode { All, Own, Opponent, Border }

    public static class BoardRenderer
    {
        public const int WindowSize = 9;
        public const double CellSize = 28;
        public const int CenterIndex = 4 * WindowSize + 4; // row=4,col=4 -> 40

        public static readonly Color OwnColor = Color.FromRgb(0, 170, 0);     // зелёный — свои
        public static readonly Color OppColor = Color.FromRgb(210, 30, 30);   // красный — чужие
        public static readonly Color BorderColor = Color.FromRgb(30, 90, 210); // синий — пусто/граница

        /// <summary>
        /// probs может быть null (сеть ещё не прогонялась) — тогда рисуем пустую сетку.
        /// targetForCenter — значение слайдера (0..1), им красится центральная клетка.
        /// </summary>
        public static void Render(Canvas canvas, double[][]? probs, LayerMode mode, double targetForCenter)
        {
            canvas.Children.Clear();
            canvas.Width = WindowSize * CellSize;
            canvas.Height = WindowSize * CellSize;

            for (int row = 0; row < WindowSize; row++)
            {
                for (int col = 0; col < WindowSize; col++)
                {
                    int cell = row * WindowSize + col;
                    bool isCenter = cell == CenterIndex;

                    var rect = new Rectangle
                    {
                        Width = CellSize,
                        Height = CellSize,
                        Stroke = Brushes.Gainsboro,
                        StrokeThickness = 0.5,
                        Fill = isCenter
                            ? CenterBrush(mode, targetForCenter)
                            : CellBrush(mode, probs, cell)
                    };

                    Canvas.SetLeft(rect, col * CellSize);
                    Canvas.SetTop(rect, row * CellSize);
                    canvas.Children.Add(rect);
                }
            }
        }

        private static Brush CellBrush(LayerMode mode, double[][]? probs, int cell)
        {
            if (probs == null) return Brushes.White;

            double wOwn = probs[cell][0];
            double wOpp = probs[cell][1];
            double wBorder = probs[cell][2];

            switch (mode)
            {
                case LayerMode.All:
                    // Веса в сумме дают 1 (softmax) — смешиваем цвета по вкладу каждого канала
                    byte r = ClampByte(OwnColor.R * wOwn + OppColor.R * wOpp + BorderColor.R * wBorder);
                    byte g = ClampByte(OwnColor.G * wOwn + OppColor.G * wOpp + BorderColor.G * wBorder);
                    byte b = ClampByte(OwnColor.B * wOwn + OppColor.B * wOpp + BorderColor.B * wBorder);
                    return new SolidColorBrush(Color.FromRgb(r, g, b));

                case LayerMode.Own:
                    return new SolidColorBrush(Color.FromArgb(ClampByte(wOwn * 255), OwnColor.R, OwnColor.G, OwnColor.B));

                case LayerMode.Opponent:
                    return new SolidColorBrush(Color.FromArgb(ClampByte(wOpp * 255), OppColor.R, OppColor.G, OppColor.B));

                case LayerMode.Border:
                    return new SolidColorBrush(Color.FromArgb(ClampByte(wBorder * 255), BorderColor.R, BorderColor.G, BorderColor.B));

                default:
                    return Brushes.White;
            }
        }

        private static Brush CenterBrush(LayerMode mode, double target)
        {
            // Центр окна в StateEncoder жёстко = "моя фишка" (не результат оптимизации).
            // По договорённости показываем его прозрачностью по значению слайдера,
            // а не вероятностью — это визуальный маркер "вот цель, которую мы просили".
            byte alpha = ClampByte(target * 255);
            return (mode == LayerMode.All || mode == LayerMode.Own)
                ? new SolidColorBrush(Color.FromArgb(alpha, OwnColor.R, OwnColor.G, OwnColor.B))
                : Brushes.White; // в "чужие" и "границы" центру нечего показывать
        }

        private static byte ClampByte(double v) => (byte)Math.Clamp(v, 0, 255);
    }
}