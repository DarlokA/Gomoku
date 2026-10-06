namespace GomokuGame.Models
{
    public static class StatusStrings
    {
        // Строка при старте/ходе
        public static string Turn(CellState player)
            => $"Ход игрока {(player == CellState.X ? "X" : "O")}";

        // Победа
        public static string Win(CellState player)
            => $"Победил игрок {(player == CellState.X ? "X" : "O")}!";

        // Ничья
        public const string Draw = "Ничья!";

        // Все возможные строки — для измерения
        public static IEnumerable<string> All()
        {
            yield return Turn(CellState.X);
            yield return Turn(CellState.O);
            yield return Win(CellState.X);
            yield return Win(CellState.O);
            yield return Draw;
        }

        // Строка "Сейчас ходят: X" — вторая строка с Run
        public static string CurrentPlayerLine(CellState player)
            => $"Сейчас ходят: {(player == CellState.X ? "X" : "O")}";

        public static IEnumerable<string> AllCurrentPlayerLines()
        {
            yield return CurrentPlayerLine(CellState.X);
            yield return CurrentPlayerLine(CellState.O);
        }
    }
}