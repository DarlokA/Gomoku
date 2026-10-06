using GomokuGame.Models;

namespace GomokuGame.AI
{
    public class MoveRecord
    {
        public double[] State { get; }
        public int Row { get; }
        public int Col { get; }
        public CellState Player { get; }

        public int LineLengthAfter { get; set; }
        public double AttackPotential { get; set; }
        public double OpponentPotential { get; set; }

        // объективная критичность момента (0..1), посчитанная MoveCriticality
        // именно для ЭТОГО хода, в момент его выбора — не зависит от исхода партии.
        public double Criticality { get; set; } = 0.0;

        // true, если это не реально сыгранный ход, а подмена — тот самый
        // mostCritical, который должен был выиграть/заблокировать угрозу, но не был сыгран.
        public bool IsForcedCorrection { get; set; } = false;

        /// <summary>
        /// Вес семпла в обучении.
        /// 1.0 — обычный ход.
        /// 3.0 — правильный критический ход.
        /// 5.0 — пропуск победы или угрозы.
        /// </summary>
        public double CriticalityWeight { get; set; } = 1.0;

        /// <summary>Заполнено только для критичных семплов (вес 5.0) — 4 симметрии для буфера.</summary>
        public double[][]? AllOrientationStates { get; set; }

        public MoveRecord(double[] state, int row, int col, CellState player)
        {
            State = state;
            Row = row;
            Col = col;
            Player = player;
        }
    }
}