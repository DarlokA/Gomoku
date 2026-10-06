using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GomokuGame.Models;

namespace GomokuGame.Converters
{
    public class CellStateToBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3) return Brushes.Black;

            var state = (CellState)values[0];
            var xColor = values[1] as string ?? "#1976D2";
            var oColor = values[2] as string ?? "#D32F2F";

            string hex = state switch
            {
                CellState.X => xColor,
                CellState.O => oColor,
                _ => "#000000"
            };

            try
            {
                return (Brush)new BrushConverter().ConvertFromString(hex)!;
            }
            catch
            {
                return Brushes.Black;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}