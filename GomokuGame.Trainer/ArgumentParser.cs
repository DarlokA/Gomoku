using System.Collections.Generic;

namespace GomokuGame.Trainer
{
    /// <summary>
    /// Парсер аргументов командной строки в формате "ключ:значение".
    /// Поддерживает аргументы с кавычками и игнорирует некорректные форматы.
    /// </summary>
    public static class ArgumentParser
    {
        /// <summary>
        /// Парсит список аргументов в словарь ключ:значение (case-insensitive).
        /// Аргументы без разделителя ":" игнорируются с предупреждением.
        /// </summary>
        /// <param name="tokens">Список аргументов командной строки.</param>
        /// <returns>Словарь с парами ключ-значение.</returns>
        public static Dictionary<string, string> ParseKeyValueArgs(List<string> tokens)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var token in tokens)
            {
                int sep = token.IndexOf(':');
                
                // Игнорируем аргументы без разделителя ":" или с пустым значением
                if (sep <= 0)
                {
                    Console.WriteLine($"!! Аргумент без ключа проигнорирован: \"{token}\" (ожидался формат ключ:значение)");
                    continue;
                }
                
                string key = token.Substring(0, sep);
                string value = token.Substring(sep + 1);
                result[key] = value;
            }
            
            return result;
        }
    }
}
