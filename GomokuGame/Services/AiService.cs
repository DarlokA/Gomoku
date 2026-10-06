using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GomokuGame.AI;

namespace GomokuGame.Services
{
    public static class AiService
    {
        public const string DefaultNetwork = "network_a_15_big";

        public static string NetworkPath =>
            Path.Combine(SettingsService.SettingsDirectory, DefaultNetwork + ".json");

        /// <summary>
        /// Список доступных сетей (имён без .json) в папке настроек.
        /// </summary>
        public static List<string> GetAvailableNetworks()
        {
            var dir = SettingsService.SettingsDirectory;

            if (!Directory.Exists(dir))
            {
                List<string> res = new List<string>();
                res.Add(DefaultNetwork);
                return res;
            }

            var files = Directory.GetFiles(dir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .Cast<string>()
                .OrderBy(n => n);

            List<string> networks = new List<string>();
            foreach (var file in files)
            {
                if (!file.Equals("settings"))
                    networks.Add(file);
            }

            if (networks.Count == 0)
                networks.Add(DefaultNetwork);

            

            return networks;
        }

        /// <summary>
        /// Загружает сеть по имени. Если файла нет — создаёт новую.
        /// </summary>
        public static AiPlayer LoadOrCreateNamed(string name)
        {
            var path = Path.Combine(SettingsService.SettingsDirectory, name + ".json");
            return AiPlayer.LoadOrCreate(path);
        }

        /// <summary>
        /// Устаревший метод — оставлен для обратной совместимости.
        /// </summary>
        public static AiPlayer LoadOrCreate() => LoadOrCreateNamed(DefaultNetwork);

        public static void Save(AiPlayer player)
        {
            player.Save();
        }
    }
}