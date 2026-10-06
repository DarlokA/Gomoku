using System;

namespace GomokuGame.AI
{
    public class Layer
    {
        public int InputSize { get; }
        public int OutputSize { get; }
        public ActivationType Act { get; }

        // W[i][j] — вес от j-го входа к i-му нейрону
        public double[][] W { get; }
        // B[i] — смещение i-го нейрона
        public double[] B { get; }

        // Кэш для backprop
        private double[] _lastInput = Array.Empty<double>();
        private double[] _lastZ = Array.Empty<double>();   // pre-activation
        private double[] _lastA = Array.Empty<double>();   // post-activation

        public Layer(int inputSize, int outputSize, ActivationType type, Random rng)
        {
            InputSize = inputSize;
            OutputSize = outputSize;
            Act = type;

            W = new double[outputSize][];
            B = new double[outputSize];

            // Xavier/Glorot-инициализация: масштаб ~ 1/sqrt(inputSize)
            double scale = Math.Sqrt(2.0 / inputSize);

            for (int i = 0; i < outputSize; i++)
            {
                W[i] = new double[inputSize];
                for (int j = 0; j < inputSize; j++)
                    W[i][j] = (rng.NextDouble() * 2 - 1) * scale;

                B[i] = 0.0;
            }
        }

        /// <summary>
        /// Прямой проход. Возвращает выход слоя (массив длины OutputSize).
        /// </summary>
        public double[] Forward(double[] input)
        {
            if (input.Length != InputSize)
                throw new ArgumentException($"Ожидался вход длины {InputSize}, получен {input.Length}");

            _lastInput = input;
            _lastZ = new double[OutputSize];
            _lastA = new double[OutputSize];

            for (int i = 0; i < OutputSize; i++)
            {
                double z = B[i];
                var wi = W[i];
                for (int j = 0; j < InputSize; j++)
                    z += wi[j] * input[j];

                _lastZ[i] = z;
                _lastA[i] = Activation.Apply(Act, z);
            }

            return _lastA;
        }

        /// <summary>
        /// Обратный проход. Принимает градиент по выходу слоя (dL/dA),
        /// возвращает градиент по входу (dL/dInput).
        /// Параллельно накапливает градиенты по весам/смещениям в outGradW/outGradB.
        /// </summary>
        public double[] Backward(double[] gradOutput, double[][] outGradW, double[] outGradB)
        {
            if (gradOutput.Length != OutputSize)
                throw new ArgumentException($"Ожидался градиент длины {OutputSize}");

            // dL/dZ = dL/dA * f'(z)
            var gradZ = new double[OutputSize];
            for (int i = 0; i < OutputSize; i++)
            {
                double dA = gradOutput[i];
                double dAct = Activation.Derivative(Act, _lastZ[i], _lastA[i]);
                gradZ[i] = dA * dAct;
            }

            // Градиент по входу: dL/dInput[j] = sum_i (dL/dZ[i] * W[i][j])
            var gradInput = new double[InputSize];
            for (int i = 0; i < OutputSize; i++)
            {
                double gz = gradZ[i];
                if (gz == 0.0) continue;
                var wi = W[i];
                for (int j = 0; j < InputSize; j++)
                    gradInput[j] += gz * wi[j];
            }

            // Градиент по весам: dL/dW[i][j] = dL/dZ[i] * input[j]
            // Градиент по смещениям: dL/dB[i] = dL/dZ[i]
            for (int i = 0; i < OutputSize; i++)
            {
                double gz = gradZ[i];
                var gw = outGradW[i];
                for (int j = 0; j < InputSize; j++)
                    gw[j] += gz * _lastInput[j];

                outGradB[i] += gz;
            }

            return gradInput;
        }

        /// <summary>
        /// Применяет накопленные градиенты (SGD).
        /// </summary>
        public void ApplyGradients(double[][] gradW, double[] gradB, double learningRate)
        {
            for (int i = 0; i < OutputSize; i++)
            {
                var wi = W[i];
                var gw = gradW[i];
                for (int j = 0; j < InputSize; j++)
                    wi[j] -= learningRate * gw[j];

                B[i] -= learningRate * gradB[i];
            }
        }

        /// <summary>
        /// Обнуляет накопленные градиенты (для нового примера/батча).
        /// </summary>
        public static (double[][], double[]) CreateGradientBuffers(Layer layer)
        {
            var gw = new double[layer.OutputSize][];
            for (int i = 0; i < layer.OutputSize; i++)
                gw[i] = new double[layer.InputSize];

            var gb = new double[layer.OutputSize];
            return (gw, gb);
        }
    }
}