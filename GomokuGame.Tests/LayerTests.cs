using Microsoft.VisualStudio.TestTools.UnitTesting;
using GomokuGame.AI;
using System;

namespace GomokuGame.Tests
{
    [TestClass]
    public class LayerTests
    {
        [TestMethod]
        public void Forward_LinearLayer_ComputesDotProduct()
        {
            // Arrange
            var rng = new Random(42);
            var layer = new Layer(3, 1, ActivationType.Linear, rng);
            layer.W[0][0] = 1; layer.W[0][1] = 2; layer.W[0][2] = 3;
            layer.B[0] = 0.5;

            // Act
            var output = layer.Forward(new double[] { 1, 1, 1 });

            // Assert
            Assert.AreEqual(6.5, output[0], 1e-9);
        }
    }
}