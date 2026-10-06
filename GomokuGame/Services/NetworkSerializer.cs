using GomokuGame.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using static GomokuGame.AI.AiPlayer;

namespace GomokuGame.Services
{
    public static class NetworkSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = false, // веса много места занимают, без отступов компактнее
            Converters = { new JsonStringEnumConverter() }
        };

        public static void Save(AiPlayer player, string path)
        {
            var dto = ToDto(player.Network);
            dto.Mode = player.Mode.ToString();

            // Накопленная статистика по всем сессиям обучения — чтобы продолжение
            // "бесконечного" обучения не стартовало со статистикой с нуля.
            dto.TotalGamesPlayed = player.TotalGamesPlayed;
            dto.TotalXWins = player.TotalXWins;
            dto.TotalOWins = player.TotalOWins;
            dto.TotalDraws = player.TotalDraws;
            dto.TotalMissedWins = player.TotalMissedWins;
            dto.TotalMoves = player.TotalMoves;
            dto.EpisodesTrained = player.EpisodesTrained;
            dto.LastAverageLoss = player.LastAverageLoss;
            dto.BestWinRate = player.BestWinRate;
            dto.RecentLossHistory = player.RecentLossHistory.ToArray();

            // ← НОВОЕ: буфер критичных семплов — иначе он теряется при каждом
            // перезапуске обучения, и сеть заново ждёт, пока редкие критичные
            // позиции накопятся и закрепятся.
            dto.CriticalSamples = player.CriticalBuffer.Snapshot()
                .Select(s => new CriticalSampleDto
                {
                    States = s.States,
                    Target = s.Target,
                    Weight = s.Weight,
                    ConsecutiveGoodChecks = s.ConsecutiveGoodChecks,
                    AttemptsSinceAdded = s.AttemptsSinceAdded,
                    Key = s.Key
                })
                .ToArray();
            dto.CriticalLearnedSamplesCount = player.CriticalBuffer.LearnedSamplesCount;
            dto.CriticalTotalAttemptsToLearn = player.CriticalBuffer.TotalAttemptsToLearn;

            var json = JsonSerializer.Serialize(dto, Options);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }

        public static AiPlayer? Load(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                var dto = JsonSerializer.Deserialize<NetworkDto>(json, Options);
                if (dto == null) return null;

                var net = FromDto(dto);
                var player = new AiPlayer(net);

                // Восстанавливаем Mode
                if (Enum.TryParse<LearningMode>(dto.Mode, out var mode))
                    player.Mode = mode;

                // Восстанавливаем накопленную статистику (у старых файлов без этих полей
                // System.Text.Json подставит значения по умолчанию — 0 / пустой массив,
                // это безопасно и не ломает совместимость).
                player.TotalGamesPlayed = dto.TotalGamesPlayed;
                player.TotalXWins = dto.TotalXWins;
                player.TotalOWins = dto.TotalOWins;
                player.TotalDraws = dto.TotalDraws;
                player.TotalMissedWins = dto.TotalMissedWins;
                player.TotalMoves = dto.TotalMoves;
                player.EpisodesTrained = dto.EpisodesTrained;
                player.LastAverageLoss = dto.LastAverageLoss;
                player.BestWinRate = dto.BestWinRate;
                player.RecentLossHistory = dto.RecentLossHistory?.ToList() ?? new List<double>();

                // ← НОВОЕ: восстанавливаем буфер критичных семплов.
                // У старых файлов без этих полей dto.CriticalSamples будет пустым
                // массивом (по умолчанию) — буфер просто стартует пустым, как раньше.
                var restoredSamples = (dto.CriticalSamples ?? Array.Empty<CriticalSampleDto>())
                    .Select(d => new CriticalSample(d.States, d.Target, d.Weight, d.Key)
                    {
                        ConsecutiveGoodChecks = d.ConsecutiveGoodChecks,
                        AttemptsSinceAdded = d.AttemptsSinceAdded
                    });
                player.CriticalBuffer.RestoreState(
                    restoredSamples,
                    dto.CriticalLearnedSamplesCount,
                    dto.CriticalTotalAttemptsToLearn);

                return player;
            }
            catch
            {
                return null;
            }
        }

        // ----- DTO -----

        private class NetworkDto
        {
            public int Version { get; set; } = 2;   // подняли версию
            public int InputSize { get; set; }
            public string Mode { get; set; } = "DiscountedWithShaping";
            public LayerDto[] Layers { get; set; } = Array.Empty<LayerDto>();

            // ----- Накопленная статистика обучения -----
            public long TotalGamesPlayed { get; set; }
            public long TotalXWins { get; set; }
            public long TotalOWins { get; set; }
            public long TotalDraws { get; set; }
            public long TotalMissedWins { get; set; }
            public long TotalMoves { get; set; }
            public int EpisodesTrained { get; set; }
            public double LastAverageLoss { get; set; }
            public double BestWinRate { get; set; }
            public double[] RecentLossHistory { get; set; } = Array.Empty<double>();

            // ----- Буфер критичных семплов (новое) -----
            public CriticalSampleDto[] CriticalSamples { get; set; } = Array.Empty<CriticalSampleDto>();
            public int CriticalLearnedSamplesCount { get; set; }
            public long CriticalTotalAttemptsToLearn { get; set; }
        }

        private class LayerDto
        {
            public int InputSize { get; set; }
            public int OutputSize { get; set; }
            public string Activation { get; set; } = "ReLU";
            public double[][] W { get; set; } = Array.Empty<double[]>();
            public double[] B { get; set; } = Array.Empty<double>();
        }

        /// <summary>
        /// Один критичный семпл буфера — состояние уже размножено во всех 4 симметриях,
        /// сохраняется как есть, без пересчёта при загрузке.
        /// </summary>
        private class CriticalSampleDto
        {
            public double[][] States { get; set; } = Array.Empty<double[]>();
            public double Target { get; set; }
            public double Weight { get; set; }
            public int ConsecutiveGoodChecks { get; set; }
            public int AttemptsSinceAdded { get; set; }
            public long Key { get; set; }
        }

        // ----- Преобразования -----

        private static NetworkDto ToDto(NeuralNetwork net)
        {
            var layers = new LayerDto[net.LayerCount];
            for (int i = 0; i < net.LayerCount; i++)
            {
                var layer = net.GetLayer(i);
                layers[i] = new LayerDto
                {
                    InputSize = layer.InputSize,
                    OutputSize = layer.OutputSize,
                    Activation = layer.Act.ToString(),
                    W = layer.W,
                    B = layer.B
                };
            }

            return new NetworkDto
            {
                Version = 2,
                InputSize = net.InputSize,
                Layers = layers
            };
        }

        private static NeuralNetwork FromDto(NetworkDto dto)
        {
            var rng = new Random(0);
            var layers = new Layer[dto.Layers.Length];

            for (int i = 0; i < dto.Layers.Length; i++)
            {
                var l = dto.Layers[i];
                var activation = Enum.Parse<ActivationType>(l.Activation);

                var layer = new Layer(l.InputSize, l.OutputSize, activation, rng);

                for (int r = 0; r < l.OutputSize; r++)
                {
                    Array.Copy(l.W[r], layer.W[r], l.InputSize);
                }
                Array.Copy(l.B, layer.B, l.OutputSize);

                layers[i] = layer;
            }

            return new NeuralNetwork(layers);
        }
    }
}