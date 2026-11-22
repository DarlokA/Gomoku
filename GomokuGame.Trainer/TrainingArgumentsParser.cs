using GomokuGame.AI.Configuration;
using System.Globalization;

using static GomokuGame.AI.AiPlayer;
namespace GomokuGame.Trainer
{
    /// <summary>
    /// Парсер аргументов обучения для команды "train".
    /// </summary>
    public static class TrainingArgumentsParser
    {
        /// <summary>
        /// Помогает получить строковое значение из аргументов или возвращает дефолтное.
        /// </summary>
        private static string GetArgString(Dictionary<string, string> a, string key, string def)
            => a.TryGetValue(key, out var s) ? s : def;

        /// <summary>
        /// Помогает получить целочисленное значение из аргументов или возвращает дефолтное.
        /// </summary>
        private static int GetArg(Dictionary<string, string> a, string key, int def)
            => a.TryGetValue(key, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v : def;

        /// <summary>
        /// Помогает получить действительное число из аргументов или возвращает дефолтное.
        /// </summary>
        private static double GetArgDouble(Dictionary<string, string> a, string key, double def)
            => a.TryGetValue(key, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : def;

        /// <summary>
        /// Парсит режим обучения по буквенному значению (A-E).
        /// </summary>
        internal static LearningMode ParseLearningMode(string value)
        {
            return value.ToUpperInvariant() switch
            {
                "A" => LearningMode.LineLengthOnly,
                "B" => LearningMode.DiscountedOutcome,
                "C" => LearningMode.DiscountedWithShaping,
                "D" => LearningMode.AttackDefenseShaping,
                "E" => LearningMode.LocalTactical,
                _ => LearningMode.DiscountedWithShaping  // по умолчанию режим C
            };
        }

        /// <summary>
        /// Парсит режим коррекции весов по строковому значению.
        /// </summary>
        internal static CorrectionMode ParseCorrectionMode(string value)
        {
            return value.ToUpperInvariant() switch
            {
                "FULL" => CorrectionMode.Full,      // полная коррекция после каждого шага
                "ONCE" => CorrectionMode.Once,      // коррекция один раз
                "OFF" => CorrectionMode.Off,        // отключение коррекции
                "ALL" => CorrectionMode.All,        // коррекция всех весов сразу
                _ => CorrectionMode.Full            // по умолчанию FULL
            };
        }

        /// <summary>
        /// Основной метод парсинга аргументов обучения.
        /// Создает объект TrainingArguments с настройками игры, алгоритма и параметров обучения.
        /// </summary>
        /// <param name="args">Словарь с парами ключ-значение из командной строки.</param>
        /// <returns>Объект TrainingArguments с заполненными полями.</returns>
        internal static TrainingArguments ParseTrainingArguments(Dictionary<string, string> args)
        {
            // Парсим алгоритм обучения (по умолчанию "standard")
            string algorithm = GetArgString(args, "algorithm", "standard");

            // Парсим топологию сети из аргумента topology
            args.TryGetValue("topology", out var topologyValue);
            NetworkTopology topology = TopologyParser.Parse(topologyValue);

            // Создаем объект TrainingArguments с настройками игры
            var result = new TrainingArguments
            {
                Game = new GameSettings(
                    GetArg(args, "board", 9),      // размер доски (по умолчанию 9)
                    GetArg(args, "win", 5)),       // длина выигрышной линии (по умолчанию 5)

                Algorithm = algorithm,              // алгоритм обучения
                SaveName = GetArgString(args, "save", "network"),  // имя файла сохранения
                Chunk = GetArg(args, "chunk", 500), // размер чанка для обучения

                Seed = GetArg(args, "seed", 0),     // seed для случайной инициализации

                Topology = topology,                 // топология нейросети

                Mode = ParseLearningMode(
                    GetArgString(args, "mode", "C")) // режим обучения (A-E)
            };

            // В зависимости от алгоритма заполняем соответствующие поля
            if (algorithm.Equals("genetic", StringComparison.OrdinalIgnoreCase))
            {
                result.Genetic = ParseGeneticTrainingArguments(args);
            }
            else
            {
                result.Gradient = ParseGradientTrainingArguments(args);
            }

            return result;
        }

        /// <summary>
        /// Парсит аргументы для градиентного обучения (gradient descent).
        /// </summary>
        /// <param name="args">Словарь с параметрами градиентного обучения.</param>
        /// <returns>Объект GradientTrainingArguments.</returns>
        internal static GradientTrainingArguments ParseGradientTrainingArguments(Dictionary<string, string> args)
        {
            return new GradientTrainingArguments
            {
                Games = GetArg(args, "games", 10_000),        // общее количество партий
                LearningRate = GetArgDouble(args, "lr", 0.001), // скорость обучения
                Epsilon = GetArgDouble(args, "epsilon", 0.3), // epsilon для exploration

                DecayExplicit = args.ContainsKey("decay"),     // явное затухание или автоматическое
                Decay = GetArgDouble(args, "decay", 0.9995),  // коэффициент затухания (если explicit)
                DecayTargetFraction = GetArgDouble(
                    args, "decayTarget", 0.85),               // целевая дробь партий для затухания

                ShowDemo = GetArg(args, "demo", 1) == 1,      // показывать демо-партию после каждого чанка

                Correction = ParseCorrectionMode(
                    GetArgString(args, "correction", "Full")),// режим коррекции весов (FULL/ONCE/OFF/ALL)

                League = new LeagueTrainingSettings(
                    GetArg(args, "warmup", 500),              // количество партий warmup в лиге
                    GetArg(args, "snapshotEvery", 100))       // сохранять чекпоинт каждые N партий
            };
        }

        /// <summary>
        /// Парсит аргументы для генетического обучения (genetic algorithm).
        /// </summary>
        /// <param name="args">Словарь с параметрами генетического обучения.</param>
        /// <returns>Объект GeneticTrainingArguments.</returns>
        internal static GeneticTrainingArguments ParseGeneticTrainingArguments(Dictionary<string, string> args)
        {
            // Размер популяции (по умолчанию — количество ядер процессора)
            int population = GetArg(args, "population", Environment.ProcessorCount);

            return new GeneticTrainingArguments
            {
                Population = population,                    // размер популяции нейросетей
                EliteCount = GetArg(
                    args, "eliteCount", Math.Max(1, population / 2)),  // элита (лучшие особи)

                MutationSigma = GetArgDouble(
                    args, "mutationSigma", 0.05),           // стандартное отклонение для мутаций

                GamesPerPair = GetArg(args, "gamesPerPair", 100),  // партий на сравнение пар особей
                MaxCycles = GetArg(args, "cycles", 0)            // максимальные циклы эволюции (0 = бесконечно)
            };
        }
    }
}
