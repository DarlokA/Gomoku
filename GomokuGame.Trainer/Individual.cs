using GomokuGame.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// ============ Individual.cs ============
namespace GomokuGame.Trainer
{
    public class Individual
    {
        public int Id { get; set; }
        public AiPlayer Ai { get; set; } = null!;
        public double Score { get; set; }       // сумма очков за чанк турнира (1/0.5/0)
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public double LastLoss { get; set; }
        public double Epsilon { get; set; }
        public int CriticalBufferSize { get; set; }
        public int CriticalSamplesLearnedCount { get; set; }
        public double AverageAttemptsToLearn { get; set; }

        public void ResetTournamentStats()
        {
            Score = 0;
            Wins = 0;
            Draws = 0;
            Losses = 0;
        }
    }
}
