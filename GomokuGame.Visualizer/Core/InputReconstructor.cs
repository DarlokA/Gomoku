 using System;
 using GomokuGame.AI;
namespace GomokuGame.Visualizer.Core
{
    /// <summary>
    /// Результат реконструкции входа для заданного целевого выхода сети.
    /// Probs[cell][channel]: cell = 0..80 (строка-мажорный порядок 9x9,
    /// cell = row*9+col), channel: 0=свои, 1=чужие, 2=пусто/граница.
    /// </summary>
    public class ReconstructionResult
    {
        public double[][] Probs { get; }
        public double FinalOutput { get; }
        public int Iterations { get; }
        public bool Converged { get; }

        public ReconstructionResult(double[][] probs, double finalOutput, int iterations, bool converged)
        {
            Probs = probs;
            FinalOutput = finalOutput;
            Iterations = iterations;
            Converged = converged;
        }
    }

    /// <summary>
    /// Подбирает вход сети (окно 9x9x3) так, чтобы Forward(вход) давал
    /// заданное целевое значение. Веса сети не меняются — оптимизация
    /// идёт только по входу (activation maximization / inversion).
    /// </summary>
    public static class InputReconstructor
    {
        private const int Cells = StateEncoder.WindowSize * StateEncoder.WindowSize; // 81
        private const int ChannelStride = Cells;
        private const int CenterCell = StateEncoder.Half * StateEncoder.WindowSize + StateEncoder.Half; // 40


        #region AggregationResult
        /// <summary>
        /// Результат агрегации по нескольким независимым запускам реконструкции.
        /// ConvergedCount/TotalAttempts — мера доверия к усреднённому результату.
        /// </summary>
        public class AggregationResult
        {
            public double[][] Probs { get; }
            public int ConvergedCount { get; }
            public int TotalAttempts { get; }

            public AggregationResult(double[][] probs, int convergedCount, int totalAttempts)
            {
                Probs = probs;
                ConvergedCount = convergedCount;
                TotalAttempts = totalAttempts;
            }
        }

        public static AggregationResult ReconstructAggregate(
            NeuralNetwork? net,
            double target,
            int attempts = 20,
            int maxIterations = 500,
            double learningRate = 0.5,
            double tolerance = 0.001,
            IProgress<AggregationResult>? progress = null)
        {
            var sum = new double[Cells][];
            for (int cell = 0; cell < Cells; cell++)
                sum[cell] = new double[3];

            int convergedCount = 0;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var result = Reconstruct(net, target, maxIterations, learningRate, tolerance, seed: attempt);
                if (!result.Converged) continue; // несошедшиеся отбрасываем целиком

                convergedCount++;
                for (int cell = 0; cell < Cells; cell++)
                    for (int ch = 0; ch < 3; ch++)
                        sum[cell][ch] += result.Probs[cell][ch];

                if (progress != null)
                {
                    // Снимок текущего усреднения — это и будет "кадр" анимации
                    var snapshot = new double[Cells][];
                    for (int cell = 0; cell < Cells; cell++)
                    {
                        snapshot[cell] = new double[3];
                        for (int ch = 0; ch < 3; ch++)
                            snapshot[cell][ch] = sum[cell][ch] / convergedCount;
                    }
                    progress.Report(new AggregationResult(snapshot, convergedCount, attempt + 1));
                }
            }

            var avg = new double[Cells][];
            for (int cell = 0; cell < Cells; cell++)
            {
                avg[cell] = new double[3];
                if (convergedCount > 0)
                    for (int ch = 0; ch < 3; ch++)
                        avg[cell][ch] = sum[cell][ch] / convergedCount;
            }

            return new AggregationResult(avg, convergedCount, attempts);
        }
        #endregion



        public static ReconstructionResult Reconstruct(
            NeuralNetwork? net,
            double target,
            int maxIterations = 500,
            double learningRate = 0.5,
            double tolerance = 0.001,
            int? seed = null)
        {
            if (net?.OutputSize != 1)
                throw new ArgumentException("Реконструкция рассчитана на сеть с одним выходным нейроном");

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();

            // 240 свободных логитов: 80 клеток (без центра) x 3 канала
            var logits = new double[Cells][];
            for (int cell = 0; cell < Cells; cell++)
            {
                logits[cell] = new double[3];
                if (cell == CenterCell)
                {
                    // Центр жёстко = "моя фишка", как в StateEncoder.Encode
                    logits[cell][0] = 10.0; // большой логит -> softmax ~ [1,0,0]
                    logits[cell][1] = -10.0;
                    logits[cell][2] = -10.0;
                }
                else
                {
                    for (int ch = 0; ch < 3; ch++)
                        logits[cell][ch] = (rng.NextDouble() * 2 - 1) * 0.5;
                }
            }

            double lastOutput = 0;
            int iter = 0;
            bool converged = false;

            for (iter = 0; iter < maxIterations; iter++)
            {
                // 1. Softmax по каждой клетке -> вероятности
                var probs = new double[Cells][];
                for (int cell = 0; cell < Cells; cell++)
                    probs[cell] = Softmax(logits[cell]);

                // 2. Собираем плоский вход сети (243) в раскладке StateEncoder
                var input = new double[StateEncoder.InputSize];
                for (int cell = 0; cell < Cells; cell++)
                    for (int ch = 0; ch < 3; ch++)
                        input[ch * ChannelStride + cell] = probs[cell][ch];

                // 3. Forward
                var output = net.Forward(input);
                lastOutput = output[0];

                double diff = lastOutput - target;
                if (Math.Abs(diff) < tolerance)
                {
                    converged = true;
                    break;
                }

                // 4. Градиент MSE-подобной цели по выходу сети
                var gradOutput = new[] { 2.0 * diff };

                // 5. Backward сети -> градиент по входу (243 числа)
                var gradInput = net.Backward(gradOutput);

                // 6. Градиент по логитам через производную softmax,
                //    клетка-центр не трогаем (она не является параметром)
                for (int cell = 0; cell < Cells; cell++)
                {
                    if (cell == CenterCell) continue;

                    var p = probs[cell];
                    var g = new double[3];
                    for (int ch = 0; ch < 3; ch++)
                        g[ch] = gradInput[ch * ChannelStride + cell];

                    double dot = p[0] * g[0] + p[1] * g[1] + p[2] * g[2];
                    for (int ch = 0; ch < 3; ch++)
                    {
                        double gradLogit = p[ch] * (g[ch] - dot);
                        logits[cell][ch] -= learningRate * gradLogit;
                    }
                }
            }

            // Финальные вероятности для результата (пересчёт после последнего шага логитов)
            var finalProbs = new double[Cells][];
            for (int cell = 0; cell < Cells; cell++)
                finalProbs[cell] = Softmax(logits[cell]);

            return new ReconstructionResult(finalProbs, lastOutput, iter, converged);
        }

        public static ReconstructionResult? ReconstructWithRetries(
                                            NeuralNetwork? net, double target, int maxAttempts = 5,
                                            int maxIterations = 500, double learningRate = 0.5, double tolerance = 0.001)
        {
            ReconstructionResult? best = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var result = Reconstruct(net, target, maxIterations, learningRate, tolerance, seed: attempt);
                if (result.Converged) return result;
                if (best == null || Math.Abs(result.FinalOutput - target) < Math.Abs(best.FinalOutput - target))
                    best = result;
            }
            return best; // не сошлось ни разу — возвращаем самый близкий результат
        }

        private static double[] Softmax(double[] x)
        {
            double max = Math.Max(x[0], Math.Max(x[1], x[2]));
            double e0 = Math.Exp(x[0] - max);
            double e1 = Math.Exp(x[1] - max);
            double e2 = Math.Exp(x[2] - max);
            double sum = e0 + e1 + e2;
            return new[] { e0 / sum, e1 / sum, e2 / sum };
        }
    }
}