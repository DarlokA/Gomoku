using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GomokuGame.Models;

namespace GomokuGame.Services
{
    public static class SettingsService
    {
        private static readonly string Dir =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games",
                "GomokuGame");

        private static readonly string FilePath =
            Path.Combine(Dir, "settings.json");

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string SettingsPath => FilePath;

        public static string SettingsDirectory =>
             Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "My Games",
                "GomokuGame");

        public static GameSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new GameSettings();

                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<GameSettings>(json, Options)
                               ?? new GameSettings();

                // Миграция: если XIsComputer не был в JSON, но PlayerType был — конвертируем
                // (System.Text.Json оставит дефолт false, если поля нет)
                // Но если в старом JSON было XPlayerType = "Computer" — надо это учесть.
                // Простейший способ: прочитать старый PlayerType через отдельный парс.
                TryMigratePlayerType(json, settings);

                return settings;
            }
            catch
            {
                return new GameSettings();
            }
        }

        private static void TryMigratePlayerType(string json, GameSettings settings)
        {
            // Если в JSON есть XIsComputer или OIsComputer — миграция не нужна.
            if (json.Contains("\"XIsComputer\"") || json.Contains("\"OIsComputer\""))
                return;

            // Иначе — старый формат с PlayerType. Читаем.
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("XPlayerType", out var xProp))
                {
                    string xVal = xProp.GetString() ?? "Human";
                    settings.XIsComputer = xVal.Equals("Computer", StringComparison.OrdinalIgnoreCase);
                }

                if (root.TryGetProperty("OPlayerType", out var oProp))
                {
                    string oVal = oProp.GetString() ?? "Human";
                    settings.OIsComputer = oVal.Equals("Computer", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Битый JSON — оставляем дефолты
            }
        }

        public static void Save(GameSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var json = JsonSerializer.Serialize(settings, Options);
                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // Сохранение настроек не критично — молча игнорируем
            }
        }
    }
}