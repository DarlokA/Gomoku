using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.AI;
using System;

namespace GomokuGame.Tests
{
    [TestClass]
    public class NeuralNetworkTests
    {
        [TestMethod]
        public void Network_LearnsXOR()
        {
            var rng = new Random(42);

            // 2 → 4 → 1
            var net = new NeuralNetwork(
                new Layer(2, 4, ActivationType.Sigmoid, rng),
                new Layer(4, 1, ActivationType.Sigmoid, rng));

            // Датасет XOR
            var inputs = new[]
            {
                new double[] { 0, 0 },
                new double[] { 0, 1 },
                new double[] { 1, 0 },
                new double[] { 1, 1 },
            };
            var targets = new[]
            {
                new double[] { 0 },
                new double[] { 1 },
                new double[] { 1 },
                new double[] { 0 },
            };

            const double lr = 0.5;
            const int epochs = 5000;

            // Обучение
            for (int epoch = 0; epoch < epochs; epoch++)
            {
                for (int i = 0; i < inputs.Length; i++)
                    net.TrainOnExample(inputs[i], targets[i], lr);
            }

            // Проверка: выходы должны быть близки к целевым
            for (int i = 0; i < inputs.Length; i++)
            {
                var output = net.Forward(inputs[i]);
                double diff = Math.Abs(output[0] - targets[i][0]);
                Assert.IsTrue(diff < 0.1,
                    $"XOR({inputs[i][0]},{inputs[i][1]}) = {output[0]:F3}, ожидалось {targets[i][0]}");
            }
        }

        [TestMethod]
        public void Network_LearnsAND()
        {
            var rng = new Random(42);
            var net = new NeuralNetwork(
                new Layer(2, 4, ActivationType.Sigmoid, rng),
                new Layer(4, 1, ActivationType.Sigmoid, rng));

            var inputs = new[]
            {
                new double[] { 0, 0 }, new double[] { 0, 1 },
                new double[] { 1, 0 }, new double[] { 1, 1 },
            };
            var targets = new[]
            {
                new double[] { 0 }, new double[] { 0 },
                new double[] { 0 }, new double[] { 1 },
            };

            for (int epoch = 0; epoch < 2000; epoch++)
                for (int i = 0; i < 4; i++)
                    net.TrainOnExample(inputs[i], targets[i], 0.5);

            for (int i = 0; i < 4; i++)
            {
                var output = net.Forward(inputs[i]);
                Assert.IsTrue(Math.Abs(output[0] - targets[i][0]) < 0.1,
                    $"AND({inputs[i][0]},{inputs[i][1]}) = {output[0]:F3}");
            }
        }
    }
}