using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GomokuGame.Utils
{
    public static class CursorFactory
    {
        private const int Size = 64;   // размер холста курсора
        private const int HotspotX = Padding; // кончик стрелки
        private const int HotspotY = Padding;

        /// <summary>
        /// Создаёт курсор "стрелка + символ X/O" заданного цвета.
        /// </summary>
        public static Cursor CreatePlayerCursor(char symbol, Color color)
        {
            var visual = BuildVisual(symbol, color);

            var rtb = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            using var ms = new MemoryStream();
            WriteCur(rtb, ms, HotspotX, HotspotY);   // ← hotspot явно

            ms.Position = 0;
            return new Cursor(ms);
        }

        // ---------- Отрисовка ----------

        private static DrawingVisual BuildVisual(char symbol, Color color)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                DrawArrow(dc);
                DrawSymbol(dc, symbol, color);
            }
            return visual;
        }

        private const int Padding = 2;

        /// <summary>
        /// Рисует стандартную стрелку с обводкой в точке (0,0).
        /// </summary>
        private static void DrawArrow(DrawingContext dc)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(Padding, Padding), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(Padding, Padding + 22), true, false);
                ctx.LineTo(new Point(Padding + 6, Padding + 16), true, false);
                ctx.LineTo(new Point(Padding + 11, Padding + 26), true, false);
                ctx.LineTo(new Point(Padding + 15, Padding + 24), true, false);
                ctx.LineTo(new Point(Padding + 10, Padding + 14), true, false);
                ctx.LineTo(new Point(Padding + 18, Padding + 14), true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1.5), geometry);
        }

        /// <summary>
        /// Рисует символ X или O справа-снизу от стрелки.
        /// </summary>
        private static void DrawSymbol(DrawingContext dc, char symbol, Color color)
        {
            // Область под символ
            var origin = new Point(20, 18);
            double symbolSize = 22;

            var brush = new SolidColorBrush(color);
            brush.Freeze();

            var whitePen = new Pen(Brushes.White, 4) { LineJoin = PenLineJoin.Round };
            whitePen.Freeze();
            var colorPen = new Pen(brush, 3) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            colorPen.Freeze();

            if (symbol == 'X')
            {
                var p1 = origin;
                var p2 = new Point(origin.X + symbolSize, origin.Y + symbolSize);
                var p3 = new Point(origin.X + symbolSize, origin.Y);
                var p4 = new Point(origin.X, origin.Y + symbolSize);

                // Сначала «подложка» белая, потом цветная — эффект обводки
                dc.DrawLine(whitePen, p1, p2);
                dc.DrawLine(whitePen, p3, p4);
                dc.DrawLine(colorPen, p1, p2);
                dc.DrawLine(colorPen, p3, p4);
            }
            else // 'O'
            {
                var rect = new Rect(origin, new Size(symbolSize, symbolSize));
                dc.DrawEllipse(null, whitePen,
                    new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2),
                    rect.Width / 2, rect.Height / 2);
                dc.DrawEllipse(null, colorPen,
                    new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2),
                    rect.Width / 2, rect.Height / 2);
            }
        }

        // ---------- ICO ----------

        /// <summary>
        /// Кодирует RenderTargetBitmap в формат .cur (одна иконка 64x64, 32bpp)
        /// с явно заданной горячей точкой.
        /// </summary>
        private static void WriteCur(RenderTargetBitmap rtb, Stream output, int hotspotX, int hotspotY)
        {
            int w = rtb.PixelWidth;
            int h = rtb.PixelHeight;
            int stride = w * 4;
            var pixels = new byte[h * stride];
            rtb.CopyPixels(pixels, stride, 0);

            int andStride = ((w + 31) / 32) * 4;
            int andSize = andStride * h;
            int imageSize = 40 + pixels.Length + andSize;

            var bw = new BinaryWriter(output);

            // --- ICONDIR ---
            bw.Write((ushort)0);       // reserved
            bw.Write((ushort)2);       // type: 2 = cursor   ← ВАЖНО (не 1!)
            bw.Write((ushort)1);       // count

            // --- ICONDIRENTRY (для CUR!) ---
            bw.Write((byte)w);              // width
            bw.Write((byte)h);              // height
            bw.Write((byte)0);              // palette colors
            bw.Write((byte)0);              // reserved
            bw.Write((ushort)hotspotX);     // ← hotspot X вместо planes
            bw.Write((ushort)hotspotY);     // ← hotspot Y вместо bpp
            bw.Write(imageSize);            // size of image data
            bw.Write(22);                   // offset (6 + 16)

            // --- BITMAPINFOHEADER (тут всё как в ICO) ---
            bw.Write(40);
            bw.Write(w);
            bw.Write(h * 2);           // высота ×2 (XOR + AND)
            bw.Write((ushort)1);       // planes — тут всегда 1
            bw.Write((ushort)32);      // bpp — тут всегда 32
            bw.Write(0);
            bw.Write(imageSize - 40);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);

            // --- XOR (BGRA, снизу вверх) ---
            for (int y = h - 1; y >= 0; y--)
                bw.Write(pixels, y * stride, stride);

            // --- AND (пустая маска) ---
            bw.Write(new byte[andSize]);

            bw.Flush();
        }
    }
}