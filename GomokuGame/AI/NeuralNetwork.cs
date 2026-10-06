using System;

namespace GomokuGame.AI
{
    public class NeuralNetwork
    {
        private readonly Layer[] _layers;

        // Буферы для градиентов: по одному на каждый слой
        private readonly double[][][] _gradW;
        private readonly double[][] _gradB;

        public int InputSize => _layers[0].InputSize;
        public int OutputSize => _layers[^1].OutputSize;
        public int LayerCount => _layers.Length;
        public Layer GetLayer(int i) => _layers[i];

        public NeuralNetwork(params Layer[] layers)
        {
            if (layers.Length == 0)
                throw new ArgumentException("Сеть должна содержать хотя бы один слой");

            _layers = layers;

            _gradW = new double[_layers.Length][][];
            _gradB = new double[_layers.Length][];

            for (int i = 0; i < _layers.Length; i++)
            {
                var (gw, gb) = Layer.CreateGradientBuffers(_layers[i]);
                _gradW[i] = gw;
                _gradB[i] = gb;
            }
        }

        /// <summary>
        /// Прямой проход через все слои. Возвращает выход последнего слоя.
        /// </summary>
        public double[] Forward(double[] input)
        {
            var current = input;
            for (int i = 0; i < _layers.Length; i++)
                current = _layers[i].Forward(current);
            return current;
        }

        /// <summary>
        /// Обратный проход. gradOutput — производная loss по выходу сети.
        /// Возвращает производную loss по входу сети (не всегда нужна, но пусть будет).
        /// Градиенты по весам/смещениям накапливаются во внутренних буферах.
        /// </summary>
        public double[] Backward(double[] gradOutput)
        {
            var currentGrad = gradOutput;

            for (int i = _layers.Length - 1; i >= 0; i--)
            {
                currentGrad = _layers[i].Backward(
                    currentGrad,
                    _gradW[i],
                    _gradB[i]);
            }

            return currentGrad;
        }

        /// <summary>
        /// Обучение на одном примере: forward, MSE loss, backward, SGD-шаг.
        /// Возвращает значение loss до обновления.
        /// </summary>
        public double TrainOnExample(double[] input, double[] target, double learningRate)
        {
            if (target.Length != OutputSize)
                throw new ArgumentException(
                    $"Ожидался target длины {OutputSize}, получен {target.Length}");

            // 1. Forward
            var output = Forward(input);

            // 2. MSE loss: L = Σ (output[i] - target[i])^2
            double loss = 0;
            for (int i = 0; i < output.Length; i++)
            {
                double d = output[i] - target[i];
                loss += d * d;
            }

            // 3. dL/dOutput[i] = 2 * (output[i] - target[i])
            var gradOutput = new double[output.Length];
            for (int i = 0; i < output.Length; i++)
                gradOutput[i] = 2.0 * (output[i] - target[i]);

            // 4. Backward
            ZeroGradients();
            Backward(gradOutput);

            // 5. Обновляем веса
            ApplyGradients(learningRate);

            return loss;
        }

        public void ApplyGradients(double learningRate)
        {
            for (int i = 0; i < _layers.Length; i++)
                _layers[i].ApplyGradients(_gradW[i], _gradB[i], learningRate);
        }

        public void ZeroGradients()
        {
            for (int i = 0; i < _layers.Length; i++)
            {
                var gw = _gradW[i];
                for (int k = 0; k < gw.Length; k++)
                    Array.Clear(gw[k], 0, gw[k].Length);

                Array.Clear(_gradB[i], 0, _gradB[i].Length);
            }
        }
    }
}