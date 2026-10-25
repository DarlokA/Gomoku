using GomokuGame.Models;
using System;
using System.Text;

namespace GomokuGame.AI
{
    public static class StateEncoder
    {
        public const int WindowSize = Training.GradeMain.StateEncoder.WindowSize;
        public const int Half = Training.GradeMain.StateEncoder.Half;
        public const int Channels = Training.GradeMain.StateEncoder.Channels;
        public const int InputSize = Training.GradeMain.StateEncoder.InputSize;

        public static double[] Encode(
            GameBoard board,
            int centerRow,
            int centerCol,
            CellState me,
            int orientation = 0)
        {
            return Training.GradeMain.StateEncoder.Encode(
                board,
                centerRow,
                centerCol,
                me,
                orientation);
        }
    }
}