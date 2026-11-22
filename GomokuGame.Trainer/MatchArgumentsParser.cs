using GomokuGame.Models;
using System.Collections.Generic;
using System.Globalization;

namespace GomokuGame.Trainer
{
    /// <summary>
    /// Парсер аргументов для команды "match" (игровые матчи между двумя сетями).
    /// </summary>
    public static class MatchArgumentsParser
    {
        /// <summary>
        /// Парсит аргументы командной строки для проведения матча.
        /// Загружает две нейросети (X и O) и настраивает параметры игры.
        /// </summary>
        /// <param name="args">Словарь с парами ключ-значение из командной строки.</param>
        /// <returns>Объект MatchArguments с настройками матча.</returns>
        internal static MatchArguments ParseMatchArguments(Dictionary<string, string> args)
        {
            // Создаем объект MatchArguments с настройками игры
            return new MatchArguments
            {
                Game = new GameSettings(
                    GetArg(args, "board", 9),      // размер доски (по умолчанию 9x9)
                    GetArg(args, "win", 5)),       // длина выигрышной линии (по умолчанию 5)

                Games = GetArg(args, "games", 1000),        // общее количество партий в матче
                FileX = GetArgString(args, "x", "network"), // файл сети X (игрока крестика)
                FileO = GetArgString(args, "o", "network")  // файл сети O (игрока нолика)
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
    }
}
