using GomokuGame.Models;

namespace GomokuGame.AI
{
    /// <summary>
    /// Кандидат на ход — результат оценки одной пустой клетки.
    /// Хранит закодированное состояние, оценку сети и критичность.
    /// </summary>
    public class MoveCandidate
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public CellState Player { get; set; }

        /// <summary>Закодированное окно 9×9 для этого хода.</summary>
        public double[] State { get; set; } = System.Array.Empty<double>();

        /// <summary>Оценка сети (выход forward).</summary>
        public double Value { get; set; }

        /// <summary>Критичность клетки по правилам Darlo/Евы.</summary>
        public double Criticality { get; set; }
    }
}