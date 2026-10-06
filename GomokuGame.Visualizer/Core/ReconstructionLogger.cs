using System;
using System.Globalization;
using System.IO;

namespace GomokuGame.Visualizer.Core
{
    public static class ReconstructionLogger
    {
        private static readonly int[] NearOffsets = { 30, 31, 32, 39, 41, 48, 49, 50 }; // 8 соседей центра (cell=40) в сетке 9x9

        public record Summary(double MaxOppNear, double AvgOppNear, double AvgOppFar);

        public static Summary Summarize(double[][] probs)
        {
            double maxNear = 0, sumNear = 0, sumFar = 0;
            int nearCount = 0, farCount = 0;

            for (int cell = 0; cell < probs.Length; cell++)
            {
                double wOpp = probs[cell][1];
                bool isNear = Array.IndexOf(NearOffsets, cell) >= 0;

                if (isNear)
                {
                    sumNear += wOpp;
                    nearCount++;
                    if (wOpp > maxNear) maxNear = wOpp;
                }
                else
                {
                    sumFar += wOpp;
                    farCount++;
                }
            }

            return new Summary(maxNear, sumNear / nearCount, sumFar / farCount);
        }

        public static void Append(string filePath, string networkName, double target, int seed,
            ReconstructionResult result)
        {
            bool isNew = !File.Exists(filePath);
            var summary = Summarize(result.Probs);

            using var writer = new StreamWriter(filePath, append: true);
            if (isNew)
                writer.WriteLine("Timestamp;NetworkName;Target;Seed;Converged;Iterations;FinalOutput;MaxOppNear;AvgOppNear;AvgOppFar");

            writer.WriteLine(string.Join(";",
                DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
                networkName,
                target.ToString("F3", CultureInfo.InvariantCulture),
                seed,
                result.Converged,
                result.Iterations,
                result.FinalOutput.ToString("F4", CultureInfo.InvariantCulture),
                summary.MaxOppNear.ToString("F2", CultureInfo.InvariantCulture),
                summary.AvgOppNear.ToString("F2", CultureInfo.InvariantCulture),
                summary.AvgOppFar.ToString("F2", CultureInfo.InvariantCulture)));
        }
    }
}