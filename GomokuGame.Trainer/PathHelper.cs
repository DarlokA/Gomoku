using System.IO;
using System;

namespace GomokuGame.Trainer
{
    /// <summary>
    /// Утилита для работы с путями сохранения нейросетей.
    /// </summary>
    public static class PathHelper
    {
        /// <summary>
        /// Возвращает полный путь к файлу сохранения нейросети в папке "My Documents\My Games\GomokuGame".
        /// Формирует путь вида: %Documents%\My Games\GomokuGame\{name}.json
        /// </summary>
        /// <param name="name">Имя файла без расширения .json.</param>
        /// <returns>Полный путь к файлу сохранения.</returns>
        public static string GetSavePath(string name)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "GomokuGame", name + ".json");
        }
    }
}
