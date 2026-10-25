using System;
using System.Linq;
using GomokuGame.AI.Training;

namespace GomokuGame.AI.Configuration
{
    public static class SingleNetworkFactory
    {
        public static NetworkStageTopology ValidateTopology(
            NetworkTopology topology)
        {
            if (topology == null)
                throw new ArgumentNullException(nameof(topology));

            if (topology.Stages.Count != 1)
            {
                throw new FormatException(
                    "На этом этапе поддерживается одна сеть. " +
                    "Выполнение каскада ещё не подключено.");
            }

            var stage = topology.Stages[0];
            var trainer = BoardTrainerFactory.Create(stage.TrainerName);

            if (stage.InputSize != trainer.InputSize)
            {
                throw new FormatException(
                    $"Обработчик '{stage.TrainerName}' формирует " +
                    $"{trainer.InputSize} входных значений, " +
                    $"а в топологии указано {stage.InputSize}.");
            }

            if (stage.OutputSize != 1)
            {
                throw new FormatException(
                    $"Обработчик '{stage.TrainerName}' " +
                    "поддерживает ровно один выход.");
            }

            return stage;
        }

        public static NeuralNetwork Create(
            NetworkTopology topology,
            Random rng)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            var stage = ValidateTopology(topology);
            var layers = new Layer[stage.Sizes.Count - 1];

            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = new Layer(
                    stage.Sizes[i],
                    stage.Sizes[i + 1],
                    GetActivation(i, layers.Length),
                    rng);
            }

            return new NeuralNetwork(layers);
        }

        public static void ValidateNetwork(
            NeuralNetwork network,
            NetworkTopology topology)
        {
            var stage = ValidateTopology(topology);

            if (network.LayerCount != stage.Sizes.Count - 1)
            {
                throw new FormatException(
                    "Количество слоёв сети не соответствует топологии.");
            }

            for (int i = 0; i < network.LayerCount; i++)
            {
                var layer = network.GetLayer(i);

                if (layer.InputSize != stage.Sizes[i]
                    || layer.OutputSize != stage.Sizes[i + 1]
                    || layer.Act != GetActivation(i, network.LayerCount))
                {
                    throw new FormatException(
                        $"Слой №{i + 1} сети " +
                        "не соответствует указанной топологии.");
                }
            }
        }

        public static NetworkTopology DescribeGradeMainNetwork(
            NeuralNetwork network)
        {
            if (network.LayerCount == 0)
            {
                throw new FormatException(
                    "Сеть должна содержать хотя бы один обучаемый слой.");
            }

            var sizes = new int[network.LayerCount + 1];
            sizes[0] = network.InputSize;

            for (int i = 0; i < network.LayerCount; i++)
                sizes[i + 1] = network.GetLayer(i).OutputSize;

            var stage = new NetworkStageTopology(
                sizes,
                new[] { "Grade" },
                BoardTrainerFactory.GradeMainName);

            return new NetworkTopology(new[] { stage });
        }

        public static string Format(NetworkTopology topology)
        {
            return string.Join(
                "->",
                topology.Stages.Select(stage =>
                    $"{{{string.Join(",", stage.Sizes)}}}" +
                    $"[{string.Join(",", stage.OutputNames)}]" +
                    $"({stage.TrainerName})"));
        }

        private static ActivationType GetActivation(
            int layerIndex,
            int layerCount)
        {
            return layerIndex == layerCount - 1
                ? ActivationType.Sigmoid
                : ActivationType.ReLU;
        }
    }
}