using GomokuGame.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GomokuGame.Tests
{
    [TestClass]
    public class LayerBackwardTests
    {
        [TestMethod]
        public void Backward_GradientCheck_MatchesNumeric()
        {
            const double eps = 1e-6;
            const double tolerance = 1e-4;

            var rng = new Random(42);
            var layer = new Layer(3, 1, ActivationType.Sigmoid, rng);
            var input = new double[] { 0.5, -1.2, 0.3 };
            var target = new double[] { 0.7 };

            double Loss(double[] output)
            {
                double d = output[0] - target[0];
                return 0.5 * d * d;
            }

            // 1. Forward + аналитический Backward
            var output = layer.Forward(input);
            var gradOutput = new double[] { output[0] - target[0] };

            var (gw, gb) = Layer.CreateGradientBuffers(layer);
            layer.Backward(gradOutput, gw, gb);

            // 2. Численная проверка весов
            for (int i = 0; i < layer.OutputSize; i++)
            {
                for (int j = 0; j < layer.InputSize; j++)
                {
                    double original = layer.W[i][j];

                    layer.W[i][j] = original + eps;
                    double lossPlus = Loss(layer.Forward(input));

                    layer.W[i][j] = original - eps;
                    double lossMinus = Loss(layer.Forward(input));

                    layer.W[i][j] = original;

                    double numeric = (lossPlus - lossMinus) / (2 * eps);
                    Assert.AreEqual(numeric, gw[i][j], tolerance,
                        $"W[{i}][{j}]: analytic={gw[i][j]}, numeric={numeric}");
                }

                // 3. Численная проверка смещений
                double bOrig = layer.B[i];
                layer.B[i] = bOrig + eps;
                double lPlus = Loss(layer.Forward(input));
                layer.B[i] = bOrig - eps;
                double lMinus = Loss(layer.Forward(input));
                layer.B[i] = bOrig;

                double numericB = (lPlus - lMinus) / (2 * eps);
                Assert.AreEqual(numericB, gb[i], tolerance,
                    $"B[{i}]: analytic={gb[i]}, numeric={numericB}");
            }
        }

        [TestMethod]
        public void Backward_GradientCheck_HiddenLayer_MatchesNumeric()
        {
            const double eps = 1e-6;
            const double tolerance = 1e-4;

            var rng = new Random(42);
            var layer = new Layer(3, 4, ActivationType.Sigmoid, rng);
            var input = new double[] { 0.5, -1.2, 0.3 };

            // Произвольный "верхний" градиент длины 4
            var gradOutput = new double[] { 0.1, -0.2, 0.3, 0.4 };

            // Функция потерь, у которой dL/dOutput[i] == gradOutput[i]
            double Loss(double[] output)
            {
                double s = 0;
                for (int i = 0; i < output.Length; i++)
                    s += gradOutput[i] * output[i];
                return s;
            }

            // 1. Forward + аналитический Backward (сохраняем кэш слоя)
            layer.Forward(input);
            var (gw, gb) = Layer.CreateGradientBuffers(layer);
            layer.Backward(gradOutput, gw, gb);

            // 2. Численная проверка весов
            for (int i = 0; i < layer.OutputSize; i++)
            {
                for (int j = 0; j < layer.InputSize; j++)
                {
                    double original = layer.W[i][j];

                    layer.W[i][j] = original + eps;
                    double lossPlus = Loss(layer.Forward(input));

                    layer.W[i][j] = original - eps;
                    double lossMinus = Loss(layer.Forward(input));

                    layer.W[i][j] = original;

                    double numeric = (lossPlus - lossMinus) / (2 * eps);
                    Assert.AreEqual(numeric, gw[i][j], tolerance,
                        $"W[{i}][{j}]: analytic={gw[i][j]}, numeric={numeric}");
                }
            }

            // 3. Численная проверка смещений
            for (int i = 0; i < layer.OutputSize; i++)
            {
                double bOrig = layer.B[i];

                layer.B[i] = bOrig + eps;
                double lPlus = Loss(layer.Forward(input));

                layer.B[i] = bOrig - eps;
                double lMinus = Loss(layer.Forward(input));

                layer.B[i] = bOrig;

                double numeric = (lPlus - lMinus) / (2 * eps);
                Assert.AreEqual(numeric, gb[i], tolerance,
                    $"B[{i}]: analytic={gb[i]}, numeric={numeric}");
            }
        }
    }
}
