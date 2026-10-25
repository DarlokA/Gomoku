using System;
using System.Collections.Generic;

namespace GomokuGame.AI.Training
{
    public static class BoardTrainerFactory
    {
        public const string GradeMainName = "Оценка позиции";

        private static readonly
            IReadOnlyDictionary<string, Func<IBoardNetworkTrainer>>
            Factories =
                new Dictionary<string, Func<IBoardNetworkTrainer>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    [GradeMainName] = () => new GradeMain()
                };

        public static IBoardNetworkTrainer Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new FormatException(
                    "Не указано имя обработчика обучения.");
            }

            if (!Factories.TryGetValue(name.Trim(), out var factory))
            {
                throw new FormatException(
                    $"Неизвестный обработчик обучения '{name}'. " +
                    $"Доступные имена: {string.Join(", ", Factories.Keys)}.");
            }

            return factory();
        }
    }
}