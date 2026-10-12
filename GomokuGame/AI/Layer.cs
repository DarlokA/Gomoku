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
        private readonly double[] _lastZ;   // pre-activation
        private readonly double[] _lastA;   // post-activation

        // Буферы под градиенты в Backward — тоже выделяются один раз.
        private readonly double[] _gradZBuffer;
        private readonly double[] _gradInputBuffer;

        // Буфер под ForwardStateless — свой на каждый поток, т.к. вызывается из Parallel.For.
        private readonly ThreadLocal<double[]> _statelessBuffer;

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
            _lastZ = new double[outputSize];
            _lastA = new double[outputSize];
            _gradZBuffer = new double[outputSize];
            _gradInputBuffer = new double[inputSize];
            _statelessBuffer = new ThreadLocal<double[]>(() => new double[outputSize]);
        }

        /// <summary>
        /// Прямой проход (путь обучения, однопоточный). Пишет в переиспользуемые
        /// _lastZ/_lastA вместо новых массивов на каждый вызов.
        /// Важно: возвращаемый массив — тот же объект при следующем вызове Forward
        /// будет перезаписан, поэтому результат нужно использовать сразу, не хранить
        /// между вызовами (как и раньше по факту и делалось).
        /// </summary>
        public double[] Forward(double[] input)
        {
            if (input.Length != InputSize)
                throw new ArgumentException($"Ожидался вход длины {InputSize}, получен {input.Length}");

            _lastInput = input;

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
        /// Прямой проход без записи в кэш backward — путь выбора хода, вызывается
        /// параллельно из Parallel.For. Буфер вывода — свой на каждый поток (ThreadLocal),
        /// чтобы потоки не затирали результаты друг друга.
        /// </summary>
        public double[] ForwardStateless(double[] input)
        {
            if (input.Length != InputSize)
                throw new ArgumentException($"Ожидался вход длины {InputSize}, получен {input.Length}");

            var output = _statelessBuffer.Value!;
            for (int i = 0; i < OutputSize; i++)
            {
                double z = B[i];
                var wi = W[i];
                for (int j = 0; j < InputSize; j++)
                    z += wi[j] * input[j];
                output[i] = Activation.Apply(Act, z);
            }
            return output;
        }

        /// <summary>
        /// Обратный проход. Принимает градиент по выходу слоя (dL/dA),
        /// возвращает градиент по входу (dL/dInput).
        /// Параллельно накапливает градиенты по весам/смещениям в outGradW/outGradB.
        /// Обратный проход (путь обучения, однопоточный). gradZ и gradInput теперь
        /// переиспользуемые буферы вместо новых массивов на каждый вызов.
        /// </summary>
        public double[] Backward(double[] gradOutput, double[][] outGradW, double[] outGradB)
        {
            if (gradOutput.Length != OutputSize)
                throw new ArgumentException($"Ожидался градиент длины {OutputSize}");

            // dL/dZ = dL/dA * f'(z)
            for (int i = 0; i < OutputSize; i++)
            {
                double dA = gradOutput[i];
                double dAct = Activation.Derivative(Act, _lastZ[i], _lastA[i]);
                _gradZBuffer[i] = dA * dAct;
            }

            // Буфер переиспользуется между вызовами — старые значения нужно обнулить
            // перед накоплением через +=, раньше это гарантировалось новым массивом.
            Array.Clear(_gradInputBuffer, 0, _gradInputBuffer.Length);

            // Градиент по входу: dL/dInput[j] = sum_i (dL/dZ[i] * W[i][j])
            for (int i = 0; i < OutputSize; i++)
            {
                double gz = _gradZBuffer[i];
                if (gz == 0.0) continue;
                var wi = W[i];
                for (int j = 0; j < InputSize; j++)
                    _gradInputBuffer[j] += gz * wi[j];
            }

            // Градиент по весам: dL/dW[i][j] = dL/dZ[i] * input[j]
            // Градиент по смещениям: dL/dB[i] = dL/dZ[i]
            for (int i = 0; i < OutputSize; i++)
            {
                double gz = _gradZBuffer[i];
                var gw = outGradW[i];
                for (int j = 0; j < InputSize; j++)
                    gw[j] += gz * _lastInput[j];

                outGradB[i] += gz;
            }

            return _gradInputBuffer;
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