using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace GomokuGame.Utils
{
    public static class TextMeasurer
    {
        /// <summary>
        /// Возвращает ширину самой длинной строки из переданных,
        /// измеренную в заданном шрифте. Возвращает 0, если список пуст.
        /// </summary>
        public static double MeasureMaxWidth(
            IEnumerable<string> texts,
            FontFamily fontFamily,
            double fontSize,
            FontWeight fontWeight)
        {
            double max = 0;
            var dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow);
            var typeface = new Typeface(fontFamily, FontStyles.Normal, fontWeight, FontStretches.Normal);

            foreach (var text in texts)
            {
                if (string.IsNullOrEmpty(text)) continue;

                var ft = new FormattedText(
                    text,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black,
                    dpi.PixelsPerDip);

                if (ft.Width > max) max = ft.Width;
            }

            return max;
        }
    }
}