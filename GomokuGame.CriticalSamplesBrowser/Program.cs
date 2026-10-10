using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GomokuGame.AI;
using GomokuGame.Services;

namespace GomokuGame.CriticalSamplesBrowser
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Использование: GomokuGame.CriticalSamplesBrowser <путь_к_сети.json> [путь_к_csv]");
                Environment.Exit(1);
                return;
            }

            string networkPath = args[0];

            var player = NetworkSerializer.Load(networkPath);
            if (player == null)
            {
                Console.WriteLine($"Не удалось загрузить сеть из файла: {networkPath}");
                Environment.Exit(1);
                return;
            }

            var samples = player.CriticalBuffer.Snapshot();
            double goodErrorThreshold = player.CriticalBuffer.GoodErrorThreshold;

            string networkName = Path.GetFileNameWithoutExtension(networkPath);
            string outputPath = args.Length > 1
                ? args[1]
                : $"critical_samples_{networkName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            var rows = new List<SampleRow>(samples.Count);

            foreach (var sample in samples)
            {
                var outputs = new double[sample.States.Length];
                var errors = new double[sample.States.Length];

                for (int o = 0; o < sample.States.Length; o++)
                {
                    // Только прямой проход — веса сети не меняются, обучение не выполняется.
                    double[] output = player.Network.Forward(sample.States[o]);
                    outputs[o] = output[0];
                    errors[o] = Math.Abs(output[0] - sample.Target);
                }

                double avgError = errors.Average();

                rows.Add(new SampleRow
                {
                    Key = sample.Key,
                    Target = sample.Target,
                    Weight = sample.Weight,
                    ConsecutiveGoodChecks = sample.ConsecutiveGoodChecks,
                    AttemptsSinceAdded = sample.AttemptsSinceAdded,
                    Outputs = outputs,
                    AvgError = avgError
                });
            }

            var sorted = rows.OrderByDescending(r => r.AvgError).ToList();

            WriteCsv(outputPath, sorted);

            int notLearnedYet = sorted.Count(r => r.AvgError >= goodErrorThreshold);

            Console.WriteLine($"Сеть: {networkPath}");
            Console.WriteLine($"Семплов в буфере: {sorted.Count}");
            Console.WriteLine($"Порог хорошей ошибки (GoodErrorThreshold): {goodErrorThreshold:0.####}");
            Console.WriteLine($"Семплов с ошибкой выше порога: {notLearnedYet} из {sorted.Count}");
            Console.WriteLine($"Лог записан: {outputPath}");
        }

        private static void WriteCsv(string path, List<SampleRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Index;Key;Target;Weight;ConsecutiveGoodChecks;AttemptsSinceAdded;Output0;Output1;Output2;Output3;AvgError");

            var ci = CultureInfo.InvariantCulture;

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                string o0 = r.Outputs.Length > 0 ? r.Outputs[0].ToString("0.######", ci) : "";
                string o1 = r.Outputs.Length > 1 ? r.Outputs[1].ToString("0.######", ci) : "";
                string o2 = r.Outputs.Length > 2 ? r.Outputs[2].ToString("0.######", ci) : "";
                string o3 = r.Outputs.Length > 3 ? r.Outputs[3].ToString("0.######", ci) : "";

                sb.AppendLine(string.Join(";", new[]
                {
                    i.ToString(ci),
                    r.Key.ToString(ci),
                    r.Target.ToString("0.######", ci),
                    r.Weight.ToString("0.######", ci),
                    r.ConsecutiveGoodChecks.ToString(ci),
                    r.AttemptsSinceAdded.ToString(ci),
                    o0, o1, o2, o3,
                    r.AvgError.ToString("0.######", ci)
                }));
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private class SampleRow
        {
            public long Key { get; set; }
            public double Target { get; set; }
            public double Weight { get; set; }
            public int ConsecutiveGoodChecks { get; set; }
            public int AttemptsSinceAdded { get; set; }
            public double[] Outputs { get; set; } = Array.Empty<double>();
            public double AvgError { get; set; }
        }
    }
}