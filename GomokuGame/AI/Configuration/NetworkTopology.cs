using System;
using System.Collections.Generic;

namespace GomokuGame.AI.Configuration
{
    public sealed class NetworkTopology
    {
        public IReadOnlyList<NetworkStageTopology> Stages { get; }

        internal NetworkTopology(NetworkStageTopology[] stages)
        {
            Stages = Array.AsReadOnly(
                (NetworkStageTopology[])stages.Clone());
        }
    }

    public sealed class NetworkStageTopology
    {
        /// <summary>
        /// Размеры: вход, скрытые слои, выход.
        /// Входной размер не является обучаемым слоем.
        /// </summary>
        public IReadOnlyList<int> Sizes { get; }

        /// <summary>
        /// Подписи выходов в порядке их индексов.
        /// Используются для отображения, а не для выбора целей.
        /// </summary>
        public IReadOnlyList<string> OutputNames { get; }

        public string TrainerName { get; }

        public int InputSize => Sizes[0];

        public int OutputSize => Sizes[Sizes.Count - 1];

        internal NetworkStageTopology(
            int[] sizes,
            string[] outputNames,
            string trainerName)
        {
            Sizes = Array.AsReadOnly((int[])sizes.Clone());
            OutputNames = Array.AsReadOnly(
                (string[])outputNames.Clone());

            TrainerName = trainerName;
        }
    }
}