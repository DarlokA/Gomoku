using System;
using System.IO;
using GomokuGame.AI;
using GomokuGame.AI.Configuration;
using GomokuGame.AI.Training;
using GomokuGame.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GomokuGame.Tests
{
    [TestClass]
    public class GradeMainFactoryTests
    {
        [TestMethod]
        public void Factory_KnownName_CreatesGradeMain()
        {
            var trainer = BoardTrainerFactory.Create(
                "Оценка позиции");

            Assert.IsInstanceOfType(trainer, typeof(GradeMain));
            Assert.AreEqual(324, trainer.InputSize);
        }

        [TestMethod]
        public void Factory_UnknownName_Throws()
        {
            Assert.ThrowsException<FormatException>(() =>
            {
                BoardTrainerFactory.Create("Неизвестный обработчик");
            });
        }

        [TestMethod]
        public void CreateFresh_DefaultTopology_MatchesLegacyWeights()
        {
            var legacyRng = new Random(42);

            var legacy = new NeuralNetwork(
                new Layer(
                    324, 128, ActivationType.ReLU, legacyRng),
                new Layer(
                    128, 64, ActivationType.ReLU, legacyRng),
                new Layer(
                    64, 1, ActivationType.Sigmoid, legacyRng));

            var player = AiPlayer.CreateFresh(
                seed: 42,
                topology: TopologyParser.Parse(null));

            AssertNetworksEqual(legacy, player.Network);
        }

        [TestMethod]
        public void CreateFresh_ExplicitTopology_CreatesRequestedLayers()
        {
            var topology = TopologyParser.Parse(
                "{324,16,8,1}[Grade](Оценка позиции)");

            var player = AiPlayer.CreateFresh(
                seed: 42,
                topology: topology);

            Assert.AreEqual(3, player.Network.LayerCount);

            Assert.AreEqual(
                324, player.Network.GetLayer(0).InputSize);
            Assert.AreEqual(
                16, player.Network.GetLayer(0).OutputSize);
            Assert.AreEqual(
                8, player.Network.GetLayer(1).OutputSize);
            Assert.AreEqual(
                1, player.Network.GetLayer(2).OutputSize);

            Assert.AreEqual(
                ActivationType.ReLU,
                player.Network.GetLayer(0).Act);

            Assert.AreEqual(
                ActivationType.Sigmoid,
                player.Network.GetLayer(2).Act);
        }

        [TestMethod]
        public void CreateFresh_IncompatibleEncoderInput_Throws()
        {
            var topology = TopologyParser.Parse(
                "{323,16,1}[Grade](Оценка позиции)");

            Assert.ThrowsException<FormatException>(() =>
            {
                AiPlayer.CreateFresh(42, topology);
            });
        }

        [TestMethod]
        public void CreateFresh_Cascade_ThrowsUntilSupported()
        {
            var topology = TopologyParser.Parse(
                "{324,2}[My,Opp](Свои и чужие)" +
                "->{2,2,1}[Grade](Объединение оценок)");

            Assert.ThrowsException<FormatException>(() =>
            {
                AiPlayer.CreateFresh(42, topology);
            });
        }

        [TestMethod]
        public void SaveLoad_PreservesTopologyAndWeights()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"gomoku_topology_{Guid.NewGuid():N}.json");

            try
            {
                var topology = TopologyParser.Parse(
                    "{324,16,1}[Оценка](Оценка позиции)");

                var original = AiPlayer.CreateFresh(42, topology);

                NetworkSerializer.Save(original, path);
                var restored = NetworkSerializer.Load(path);

                Assert.IsNotNull(restored);

                Assert.AreEqual(
                    SingleNetworkFactory.Format(original.Topology),
                    SingleNetworkFactory.Format(restored!.Topology));

                AssertNetworksEqual(
                    original.Network,
                    restored.Network);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void AssertNetworksEqual(
            NeuralNetwork expected,
            NeuralNetwork actual)
        {
            Assert.AreEqual(expected.InputSize, actual.InputSize);
            Assert.AreEqual(expected.LayerCount, actual.LayerCount);

            for (int i = 0; i < expected.LayerCount; i++)
            {
                var a = expected.GetLayer(i);
                var b = actual.GetLayer(i);

                Assert.AreEqual(a.InputSize, b.InputSize);
                Assert.AreEqual(a.OutputSize, b.OutputSize);
                Assert.AreEqual(a.Act, b.Act);

                CollectionAssert.AreEqual(a.B, b.B);

                for (int row = 0; row < a.OutputSize; row++)
                    CollectionAssert.AreEqual(a.W[row], b.W[row]);
            }
        }
    }
}