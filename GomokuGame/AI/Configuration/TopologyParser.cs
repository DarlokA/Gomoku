using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GomokuGame.AI.Configuration
{
    public static class TopologyParser
    {
        public const string DefaultValue =
            "{324,128,64,1}[Grade](Оценка позиции)";

        private static readonly Regex StagePattern = new Regex(
                @"\A\s*" +
                @"\{(?<sizes>[^{}]*)\}\s*" +
                @"\[(?<names>[^\[\]{}()<>]*)\]\s*" +
                @"\((?<trainer>[^()\[\]{}<>]*)\)" +
                @"\s*\z",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static NetworkTopology Parse(string? value)
        {
            string text = value ?? DefaultValue;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException(
                    "Параметр Topology указан, но его значение пустое.");
            }

            string[] parts = text.Split(
                new[] { "->" },
                StringSplitOptions.None);

            var stages = new NetworkStageTopology[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                stages[i] = ParseStage(parts[i], i + 1);
            }

            if (stages[stages.Length - 1].OutputSize != 1)
            {
                throw new FormatException(
                    "Последняя сеть должна иметь ровно один выход.");
            }

            return new NetworkTopology(stages);
        }

        private static NetworkStageTopology ParseStage(
            string text,
            int stageNumber)
        {
            Match match = StagePattern.Match(text);

            if (!match.Success)
            {
                throw StageError(
                    stageNumber,
                    "Ожидается запись " +
                    "{вход,скрытые слои,выход}" +
                    "[подписи выходов](имя обработчика обучения).");
            }

            int[] sizes = ParseSizes(
                match.Groups["sizes"].Value,
                stageNumber);

            string[] outputNames = ParseOutputNames(
                match.Groups["names"].Value,
                sizes[sizes.Length - 1],
                stageNumber);

            string trainerName = match.Groups["trainer"].Value.Trim();

            if (trainerName.Length == 0)
            {
                throw StageError(
                    stageNumber,
                    "Имя обработчика обучения не может быть пустым.");
            }

            return new NetworkStageTopology(
                sizes,
                outputNames,
                trainerName);
        }

        private static int[] ParseSizes(
            string text,
            int stageNumber)
        {
            string[] parts = text.Split(',');

            if (parts.Length < 2)
            {
                throw StageError(
                    stageNumber,
                    "Нужно указать как минимум размеры входа и выхода.");
            }

            var sizes = new int[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();

                if (!int.TryParse(
                        token,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int size)
                    || size <= 0)
                {
                    throw StageError(
                        stageNumber,
                        $"Размер №{i + 1} должен быть положительным " +
                        $"целым числом в диапазоне Int32. Получено: '{token}'.");
                }

                sizes[i] = size;
            }

            return sizes;
        }

        private static string[] ParseOutputNames(
            string text,
            int outputSize,
            int stageNumber)
        {
            string[] names = text.Split(',');

            var usedNames = new HashSet<string>(
                StringComparer.Ordinal);

            for (int i = 0; i < names.Length; i++)
            {
                names[i] = names[i].Trim();

                if (names[i].Length == 0)
                {
                    throw StageError(
                        stageNumber,
                        $"Подпись выхода №{i + 1} не может быть пустой.");
                }

                if (!usedNames.Add(names[i]))
                {
                    throw StageError(
                        stageNumber,
                        $"Повторяется подпись выхода '{names[i]}'.");
                }
            }

            if (names.Length != outputSize)
            {
                throw StageError(
                    stageNumber,
                    $"У сети {outputSize} выходов, " +
                    $"а подписей указано {names.Length}.");
            }

            return names;
        }

        private static FormatException StageError(
            int stageNumber,
            string message)
        {
            return new FormatException(
                $"Ошибка Topology, сеть №{stageNumber}: {message}");
        }
    }
}