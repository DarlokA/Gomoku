using GomokuGame.Models;
using System.Collections.Generic;
using System.Globalization;

namespace GomokuGame.Trainer
{
    /// <summary>
    /// Парсер аргументов для команды "matchlog" (логирование матчей между двумя сетями).
    /// </summary>
    public static class MatchLogArgumentsParser
    {
        /// <summary>
        /// Парсит аргументы командной строки для создания лога матча.
        /// Загружает две нейросети (X и O) и настраивает параметры логирования.
        /// </summary>
        /// <param name="args">Словарь с парами ключ-значение из командной строки.</param>
        /// <returns>Объект MatchLogArguments с настройками логирования матча.</returns>
        internal static MatchLogArguments ParseMatchLogArguments(Dictionary<string, string> args)
        {
            // Создаем объект MatchLogArguments с настройками игры и логирования
            return new MatchLogArguments
            {
                Game = new GameSettings(
                    GetArg(args, "board", 15),      // размер доски (по умолчанию 15x15 для матчей)
                    GetArg(args, "win", 5)),       // длина выигрышной линии (по умолчанию 5)

                FileX = GetArgString(args, "x", "network"), // файл сети X (игрока крестика)
                FileO = GetArgString(args, "o", "network"), // файл сети O (игрока нолика)

                LogFile = args.TryGetValue("log", out var logFile)
                    ? logFile                       // путь к файлу лога матча (опционально)
                    : null,

                Epsilon = GetArgDouble(args, "epsilon", 0.0)  // epsilon для exploration (по умолчанию 0.0 — deterministic)
            };
        }

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
    }
}
