using GomokuGame.AI.Configuration;

namespace GomokuGame.Trainer
{
    /// <summary>
    /// Утилита для вывода топологии нейросети в консоль.
    /// </summary>
    public static class TopologyPrinter
    {
        /// <summary>
        /// Выводит описание топологии сети в формате:
        /// "Topology: {{sizes}}[output_names](trainer_name)"
        /// </summary>
        /// <param name="topology">Топология нейросети с этапами обучения.</param>
        public static void PrintRequestedTopology(NetworkTopology topology)
        {
            string description = string.Join(
                "->",
                topology.Stages.Select(stage =>
                    $"{{{string.Join(",", stage.Sizes)}}}" +
                    $"[{string.Join(",", stage.OutputNames)}]" +
                    $"({stage.TrainerName})"));

            Console.WriteLine($"Topology: {description}");
            Console.WriteLine();
        }
    }
}
