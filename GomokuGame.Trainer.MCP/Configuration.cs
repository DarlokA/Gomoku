using System.Text.Json;

namespace GomokuGame.Trainer.MCP
{
    /// <summary>
    /// Настройки окружения для MCP сервера Gomoku Trainer.
    /// </summary>
    public sealed class TrainingEnvironmentConfig
    {
        /// <summary>
        /// Путь к исполняемому файлу тренера.
        /// </summary>
        public string TrainerPath { get; set; } = "";

        /// <summary>
        /// Папка для сохранённых сетей (.json).
        /// </summary>
        public string NetworksPath { get; set; } = "";

        /// <summary>
        /// Папка для CSV логов обучения (паттерны).
        /// </summary>
        public string PatternLogsPath { get; set; } = "";

        /// <summary>
        /// Папка для CSV логов живых партий.
        /// </summary>
        public string LiveLogsPath { get; set; } = "";

        /// <summary>
        /// Включать демонические игры в трене (по умолчанию true).
        /// </summary>
        public bool DemoGamesEnabled { get; set; } = true;

        /// <summary>
        /// Снимки каждые N партий.
        /// </summary>
        public int SnapshotEvery { get; set; } = 100;
    }

    /// <summary>
    /// Фабрика для загрузки конфигурации из файла или по умолчанию.
    /// </summary>
    public static class ConfigurationLoader
    {
        /// <summary>
        /// Загрузить настройки конфигурации из JSON файла.
        /// </summary>
        public static TrainingEnvironmentConfig LoadFromJson(string configPath)
        {
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[WARN] Файл конфигурации не найден: {configPath}");
                Console.WriteLine("[INFO] Используются значения по умолчанию.");
                return new TrainingEnvironmentConfig();
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<TrainingEnvironmentConfig>(json);

                if (config == null)
                {
                    throw new FormatException("Не удалось десериализовать конфигурацию.");
                }

                Console.WriteLine($"[INFO] Конфигурация загружена из: {configPath}");
                return config;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException)
            {
                Console.WriteLine($"[WARN] Ошибка чтения конфигурации: {ex.Message}");
                Console.WriteLine("[INFO] Используются значения по умолчанию.");
                return new TrainingEnvironmentConfig();
            }
        }

        /// <summary>
        /// Загрузить настройки из папки проекта MCP (автоопределение).
        /// </summary>
        public static TrainingEnvironmentConfig LoadDefault()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var configPath = Path.Combine(baseDir, "config.json");

            return LoadFromJson(configPath);
        }

        /// <summary>
        /// Получить путь к тренеру (с fallback на дефолтный).
        /// </summary>
        public static string GetTrainerPath()
        {
            var config = LoadDefault();
            if (!string.IsNullOrWhiteSpace(config.TrainerPath))
                return config.TrainerPath;

            // Дефолтный путь если конфигурации нет
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GomokuGame.Trainer.exe");
        }

        /// <summary>
        /// Получить базовую директорию для сетей.
        /// </summary>
        public static string GetNetworksPath()
        {
            var config = LoadDefault();
            return !string.IsNullOrWhiteSpace(config.NetworksPath)
                ? config.NetworksPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "settings");
        }

        /// <summary>
        /// Получить папку для паттерн-логов.
        /// </summary>
        public static string GetPatternLogsPath()
        {
            var config = LoadDefault();
            return !string.IsNullOrWhiteSpace(config.PatternLogsPath)
                ? config.PatternLogsPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "pattern_logs");
        }

        /// <summary>
        /// Получить папку для логов живых партий.
        /// </summary>
        public static string GetLiveLogsPath()
        {
            var config = LoadDefault();
            return !string.IsNullOrWhiteSpace(config.LiveLogsPath)
                ? config.LiveLogsPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "logs");
        }

        /// <summary>
        /// Получить флаг демонических игр.
        /// </summary>
        public static bool IsDemoGamesEnabled()
        {
            var config = LoadDefault();
            return config.DemoGamesEnabled;
        }

        /// <summary>
        /// Получить интервал для снимков.
        /// </summary>
        public static int GetSnapshotEvery()
        {
            var config = LoadDefault();
            return config.SnapshotEvery;
        }
    }
}