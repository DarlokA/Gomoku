using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ============ MatchTask.cs ============
namespace GomokuGame.Trainer
{
    public readonly struct MatchTask
    {
        public readonly int XIndex;
        public readonly int OIndex;

        public MatchTask(int xIndex, int oIndex)
        {
            XIndex = xIndex;
            OIndex = oIndex;
        }
    }
}
