using System.Collections.Generic;

namespace GomokuGame.Models
{
    public static class ColorPalette
    {
        public static IReadOnlyList<ColorItem> Standard { get; } = new List<ColorItem>
        {
            new ColorItem("Красный",      "#D32F2F"),
            new ColorItem("Розовый",      "#EC407A"),
            new ColorItem("Фиолетовый",   "#7B1FA2"),
            new ColorItem("Индиго",       "#303F9F"),
            new ColorItem("Синий",        "#1976D2"),
            new ColorItem("Голубой",      "#0288D1"),
            new ColorItem("Циан",         "#0097A7"),
            new ColorItem("Бирюзовый",    "#00897B"),
            new ColorItem("Зелёный",      "#388E3C"),
            new ColorItem("Лаймовый",     "#689F38"),
            new ColorItem("Оливковый",    "#AFB42B"),
            new ColorItem("Жёлтый",       "#FBC02D"),
            new ColorItem("Оранжевый",    "#F57C00"),
            new ColorItem("Тёмно-оранж.", "#E64A19"),
            new ColorItem("Коричневый",   "#5D4037"),
            new ColorItem("Серый",        "#616161"),
            new ColorItem("Сине-серый",   "#455A64"),
            new ColorItem("Чёрный",       "#000000"),
        };

        public static ColorItem FindByHex(string hex)
        {
            foreach (var c in Standard)
                if (string.Equals(c.Hex, hex, System.StringComparison.OrdinalIgnoreCase))
                    return c;
            return Standard[4]; // Синий по умолчанию
        }
    }
}