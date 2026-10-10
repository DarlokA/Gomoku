using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                Console.WriteLine("Использование:");
                Console.WriteLine("  <путь.json> [analyze] [N]  — лог буфера, с анализом топ-N по spread при analyze");
                Console.WriteLine("  <путь.csv> [N]             — анализ готового лога, топ-N по spread (по умолчанию 20)");
                return;
            }


            string path = args[0];
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string? extra = args.Length > 1 ? args[1].ToLowerInvariant() : null;

            if (ext == ".json" && extra == "board")
            {
                string keysArg = args.Length > 2 ? args[2] : "";
                var keys = keysArg.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => long.Parse(s.Trim(), CultureInfo.InvariantCulture))
                                   .ToList();
                if (keys.Count == 0)
                {
                    Console.WriteLine("Укажи ключи через запятую: <путь.json> board <key1,key2,...>");
                    return;
                }
                PrintBoards(path, keys);
                return;
            }


            int topN = 20;
            bool doAnalyze = false;

            if (ext == ".csv")
            {
                // <path.csv> [N]
                if (extra != null && int.TryParse(extra, out int nCsv)) topN = nCsv;
            }
            else if (ext == ".json")
            {
                // <path.json> [analyze] [N]
                doAnalyze = extra == "analyze";
                string? extra2 = args.Length > 2 ? args[2] : null;
                if (extra2 != null && int.TryParse(extra2, out int nJson)) topN = nJson;
            }
            else
            {
                Console.WriteLine($"Неизвестное расширение файла: {ext}. Ожидается .json или .csv");
                return;
            }

            if (ext == ".csv")
            {
                var rows = LoadRows(path);
                PrintSpreadAnalysis(rows);
                PrintTopSpread(rows, topN);
            }
            else // .json
            {
                string csvPath = Path.ChangeExtension(path, null) + "_log.csv";
                RunBufferDump(path, csvPath);

                if (doAnalyze)
                {
                    var rows = LoadRows(csvPath);
                    PrintSpreadAnalysis(rows);
                    PrintTopSpread(rows, topN);
                }
            }
        }

        private static void RunBufferDump(string networkPath, string csvPath)
        {
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
            string outputPath = csvPath;
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

            Console.WriteLine($"Сеть: {networkName}");
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

        
        static List<SampleRow> LoadRows(string path)
        {
            var rows = new List<SampleRow>();
            var lines = File.ReadAllLines(path);
            for (int i = 1; i < lines.Length; i++) // строка 0 — заголовок
            {
                var p = lines[i].Split(';');
                if (p.Length < 11) continue;
                rows.Add(new SampleRow
                {
                    Key = long.Parse(p[1], CultureInfo.InvariantCulture),
                    Target = double.Parse(p[2], CultureInfo.InvariantCulture),
                    Weight = double.Parse(p[3], CultureInfo.InvariantCulture),
                    ConsecutiveGoodChecks = int.Parse(p[4], CultureInfo.InvariantCulture),
                    AttemptsSinceAdded = int.Parse(p[5], CultureInfo.InvariantCulture),
                    Outputs = new[] {
                double.Parse(p[6], CultureInfo.InvariantCulture),
                double.Parse(p[7], CultureInfo.InvariantCulture),
                double.Parse(p[8], CultureInfo.InvariantCulture),
                double.Parse(p[9], CultureInfo.InvariantCulture)
            },
                    AvgError = double.Parse(p[10], CultureInfo.InvariantCulture)
                });
            }
            return rows;
        }

        static double Median(double[] values)
        {
            var s = values.OrderBy(v => v).ToArray();
            int n = s.Length;
            return n % 2 == 0 ? (s[n / 2 - 1] + s[n / 2]) / 2.0 : s[n / 2];
        }

        static double Correlation(double[] x, double[] y)
        {
            double mx = x.Average(), my = y.Average();
            double sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double dx = x[i] - mx, dy = y[i] - my;
                sxy += dx * dy; sxx += dx * dx; syy += dy * dy;
            }
            return sxy / Math.Sqrt(sxx * syy);
        }

        static void PrintTopSpread(List<SampleRow> rows, int topN)
        {
            var top = rows.OrderByDescending(r => r.Spread).Take(topN).ToList();
            Console.WriteLine($"\nТоп-{topN} семплов по spread:");
            Console.WriteLine("Key\t\tSpread\tAvgError\tWeight\tOutputs");
            foreach (var r in top)
            {
                Console.WriteLine($"{r.Key}\t{r.Spread:F4}\t{r.AvgError:F4}\t{r.Weight:F0}\t[{string.Join(", ", r.Outputs.Select(o => o.ToString("F3")))}]");
            }
        }

        static void PrintSpreadAnalysis(List<SampleRow> rows)
        {
            var spreads = rows.Select(r => r.Spread).ToArray();
            var errors = rows.Select(r => r.AvgError).ToArray();

            Console.WriteLine($"Всего семплов: {rows.Count}");
            Console.WriteLine($"Spread: среднее = {spreads.Average():F4}, медиана = {Median(spreads):F4}");
            Console.WriteLine($"Корреляция spread ↔ AvgError: {Correlation(spreads, errors):F4}");

            var weak = rows.Where(r => r.Weight <= 1).ToList();
            var strong = rows.Where(r => r.Weight >= 5).ToList();
            Console.WriteLine($"Spread, Weight=1  (n={weak.Count}): среднее = {(weak.Count > 0 ? weak.Average(r => r.Spread) : 0):F4}");
            Console.WriteLine($"Spread, Weight>=5 (n={strong.Count}): среднее = {(strong.Count > 0 ? strong.Average(r => r.Spread) : 0):F4}");

            var highSpread = rows.Where(r => r.Spread > 0.2).ToList();
            Console.WriteLine($"Доля семплов со spread > 0.2: {(double)highSpread.Count / rows.Count:P1} (n={highSpread.Count})");
            if (highSpread.Count > 0)
                Console.WriteLine($"   их средняя AvgError: {highSpread.Average(r => r.AvgError):F4}");          

        }

        private static void PrintBoards(string networkPath, IReadOnlyCollection<long> keys)
        {
            var player = NetworkSerializer.Load(networkPath);
            if (player == null)
            {
                Console.WriteLine($"Не удалось загрузить сеть из файла: {networkPath}");
                Environment.Exit(1);
                return;
            }

            var samples = player.CriticalBuffer.Snapshot();
            string[] orientationNames = { "горизонталь (0)", "вертикаль (1)", "диагональ \\ (2)", "диагональ / (3)" };

            foreach (var key in keys)
            {
                var sample = samples.FirstOrDefault(s => s.Key == key);
                if (sample == null)
                {
                    Console.WriteLine($"Семпл с ключом {key} не найден в буфере.");
                    continue;
                }

                Console.WriteLine($"=== Key {key}  Target={sample.Target:0.##}  Weight={sample.Weight:0.##} ===");

                for (int o = 0; o < sample.States.Length; o++)
                {
                    double[] output = player.Network.Forward(sample.States[o]);
                    Console.WriteLine($"-- {orientationNames[o]}, выход сети = {output[0]:F3} --");
                    Console.WriteLine(DecodeStateToAscii(sample.States[o]));
                }
            }
        }

        private static string DecodeStateToAscii(double[] state)
        {
            const int size = 9;    // StateEncoder.WindowSize
            const int stride = 81; // size * size

            var sb = new StringBuilder();
            sb.Append("    ");
            for (int c = 0; c < size; c++) sb.Append(c).Append(' ');
            sb.AppendLine();
            sb.Append("   +");
            for (int c = 0; c < size; c++) sb.Append("--");
            sb.AppendLine("+");

            for (int r = 0; r < size; r++)
            {
                sb.Append(r).Append(" |");
                for (int c = 0; c < size; c++)
                {
                    int i = r * size + c;
                    double mine = state[i];
                    double theirs = state[stride + i];

                    char ch;
                    if (r == 4 && c == 4)
                        ch = '@'; // центр окна: симулированный ход, не реальная фишка
                    else if (mine >= 0.5)
                        ch = 'X';
                    else if (theirs >= 0.5)
                        ch = 'O';
                    else
                        ch = '.'; // пусто либо край реальной доски — тут не различить

                    sb.Append(' ').Append(ch);
                }
                sb.AppendLine(" |");
            }

            sb.Append("   +");
            for (int c = 0; c < size; c++) sb.Append("--");
            sb.AppendLine("+");
            return sb.ToString();
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
            public double Spread => Outputs.Max() - Outputs.Min();
        }
    }
}