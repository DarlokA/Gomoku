using System.Windows.Media;

namespace GomokuGame.Models
{
    public class ColorItem
    {
        public string Name { get; }
        public string Hex { get; }

        public Brush Brush { get; }

        public ColorItem(string name, string hex)
        {
            Name = name;
            Hex = hex;
            Brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        }

        public override string ToString() => Name;
    }
}