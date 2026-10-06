using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GomokuGame.Models;

namespace GomokuGame.AI
{
    /// <summary>
    /// Прогоняет фиксированные эталонные паттерны через сеть и логирует выход.
    /// Не обучает. Только forward. Используется в Trainer после каждого чанка.
    /// </summary>
    public static class PatternProbe
    {
        // ===== Описание паттерна =====

        public class Pattern
        {
            public string Name { get; }
            /// <summary>Смещения (dRow, dCol) и цвет фишки относительно центра (7,7).</summary>
            public (int dRow, int dCol, CellState Player)[] Cells { get; }

            public Pattern(string name, params (int, int, CellState)[] cells)
            {
                Name = name;
                Cells = cells;
            }
        }

        // ===== Список 23 паттернов =====

        private static readonly Pattern[] AllPatterns = BuildPatterns();

        private static Pattern[] BuildPatterns()
        {
            var X = CellState.X;
            var O = CellState.O;

            return new[]
            {
                // --- ЗАЩИТА: чужие линии слева / вверх / по диагоналям ---
                // "OppHorizontalLeftxN" = N чужих фишек СЛЕВА от центра (центр = целевая клетка, справа от линии).
                new Pattern("OppHorizontalLeftx3",
                    (-1, 0, O), (-1, -1, O), (-1, -2, O)),
                new Pattern("OppHorizontalLeftx4",
                    (-1, 0, O), (-1, -1, O), (-1, -2, O), (-1, -3, O)),

                new Pattern("OppVerticalUpx3",
                    (0, -1, O), (-1, -1, O), (-2, -1, O)),
                new Pattern("OppVerticalUpx4",
                    (0, -1, O), (-1, -1, O), (-2, -1, O), (-3, -1, O)),

                new Pattern("OppDiagonal1x3",
                    (-1, 0, O), (-2, -1, O), (-3, -2, O)),
                new Pattern("OppDiagonal1x4",
                    (-1, 0, O), (-2, -1, O), (-3, -2, O), (-4, -3, O)),

                new Pattern("OppDiagonal2x3",
                    (-1, 0, O), (-2, 1, O), (-3, 2, O)),
                new Pattern("OppDiagonal2x4",
                    (-1, 0, O), (-2, 1, O), (-3, 2, O), (-4, 3, O)),

                // --- АТАКА: свои линии ---
                new Pattern("OwnHorizontalLeftx3",
                    (-1, 0, X), (-1, -1, X), (-1, -2, X)),
                new Pattern("OwnHorizontalLeftx4",
                    (-1, 0, X), (-1, -1, X), (-1, -2, X), (-1, -3, X)),

                new Pattern("OwnVerticalUpx3",
                    (0, -1, X), (-1, -1, X), (-2, -1, X)),
                new Pattern("OwnVerticalUpx4",
                    (0, -1, X), (-1, -1, X), (-2, -1, X), (-3, -1, X)),

                new Pattern("OwnDiagonal1x3",
                    (-1, 0, X), (-2, -1, X), (-3, -2, X)),
                new Pattern("OwnDiagonal1x4",
                    (-1, 0, X), (-2, -1, X), (-3, -2, X), (-4, -3, X)),

                new Pattern("OwnDiagonal2x3",
                    (-1, 0, X), (-2, 1, X), (-3, 2, X)),
                new Pattern("OwnDiagonal2x4",
                    (-1, 0, X), (-2, 1, X), (-3, 2, X), (-4, 3, X)),

                // --- КОНФЛИКТ ---
                // своя 4 по горизонтали + чужая 4 по вертикали
                new Pattern("Conflict_Own4H_Opp4V",
                    (-1, -1, X), (-1, 0, X), (-1, 1, X), (-1, 2, X),   // своя 4 в строке -1
                    (0, 1, O), (-1, 1, O), (-2, 1, O), (-3, 1, O)),    // чужая 4 в столбце 1

                // своя 3 по горизонтали + чужая 4 по вертикали
                new Pattern("Conflict_Own3H_Opp4V",
                    (-1, 0, X), (-1, 1, X), (-1, 2, X),
                    (0, 1, O), (-1, 1, O), (-2, 1, O), (-3, 1, O)),

                // своя 4 по горизонтали + чужая 3 по вертикали
                new Pattern("Conflict_Own4H_Opp3V",
                    (-1, -1, X), (-1, 0, X), (-1, 1, X), (-1, 2, X),
                    (0, 1, O), (-1, 1, O), (-2, 1, O)),

                // "Гонка": своя 3 и чужая 3 на одной строке, между ними одна пустая клетка (центр).
                // X X X . O O O  -> целевая клетка — пробел. Ход X выиграет атаку и проиграет защиту.
                new Pattern("Conflict_RaceSameLine",
                    (0, -3, X), (0, -2, X), (0, -1, X),
                    (0, 1, O), (0, 2, O), (0, 3, O)),

                // --- КОНТРОЛЬ ---
                new Pattern("Empty"),

                new Pattern("SingleOwn",
                    (-1, 0, X)),

                new Pattern("SingleOpp",
                    (-1, 0, O)),
            };
        }

        public static IReadOnlyList<Pattern> Patterns => AllPatterns;

        // ===== Раскладка паттерна на доске =====

        /// <summary>
        /// Строит доску с паттерном вокруг центра (center, center).
        /// Целевая клетка — сам центр. Остальные — пустые.
        /// </summary>
        public static GameBoard BuildBoard(Pattern pattern, int boardSize, int winLength)
        {
            var board = new GameBoard(boardSize, winLength);
            int center = boardSize / 2;

            foreach (var (dr, dc, player) in pattern.Cells)
            {
                int r = center + dr;
                int c = center + dc;
                if (r >= 0 && r < boardSize && c >= 0 && c < boardSize)
                    board[r, c] = player;
            }

            return board;
        }

        // ===== Оценка =====

        /// <summary>
        /// Прогон одного паттерна через сеть.
        /// Возвращает выход сети на центральную клетку (me = X).
        /// </summary>
        public static double Evaluate(AiPlayer ai, Pattern pattern, int boardSize, int winLength)
        {
            var board = BuildBoard(pattern, boardSize, winLength);
            int center = boardSize / 2;

            // StateEncoder смотрит 9×9 вокруг (center, center).
            // me = X, orientation = 0 — фиксировано.
            var input = StateEncoder.Encode(board, center, center, CellState.X, 0);
            var output = ai.Network.Forward(input);
            return output[0];
        }

        // ===== Логирование =====

        /// <summary>
        /// Прогоняет все паттерны и возвращает словарь имя -> выход.
        /// </summary>
        public static Dictionary<string, double> EvaluateAll(AiPlayer ai, int boardSize, int winLength)
        {
            var result = new Dictionary<string, double>(AllPatterns.Length);
            foreach (var p in AllPatterns)
                result[p.Name] = Evaluate(ai, p, boardSize, winLength);
            return result;
        }

        /// <summary>
        /// Печатает все паттерны столбиком в консоль.
        /// </summary>
        public static void LogToConsole(int chunkNumber, long done, Dictionary<string, double> values)
        {
            Console.WriteLine();
            Console.WriteLine($"==== Pattern probe (after chunk {chunkNumber}, done {done:N0}) ====");
            foreach (var p in AllPatterns)
                Console.WriteLine($"{p.Name,-26} - {values[p.Name]:F4}");
            Console.WriteLine("==================================================");
            Console.WriteLine();
        }

        /// <summary>
        /// Открывает/создаёт CSV-файл pattern_log_{network}_{startGames}.csv
        /// и дописывает строку со статистикой чанка + всеми паттернами.
        /// </summary>
        public static void AppendCsv(
            string networkName,
            long startGames,
            long done,
            TrainingStats stats,
            double lastLoss,
            double epsilon,
            double timeSeconds,
            double perPartyMs,
            double avgMoves,
            int criticalBufferSize,
            int criticalSamplesLearned,
            double avgAttemptsToLearn,
            Dictionary<string, double> patternValues)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games", "GomokuGame", "pattern_logs");
            Directory.CreateDirectory(dir);

            long safeStart = startGames < 0 ? 0 : startGames;
            string path = Path.Combine(dir, $"pattern_log_{networkName}_{safeStart}.csv");

            bool isNew = !File.Exists(path);

            var sb = new StringBuilder();

            if (isNew)
            {
                // Заголовок
                sb.Append("Done;X;O;Draw;Loss;Eps;Missed;Time;PerPartyTime;AvgMoves;");
                sb.Append("CriticalBufferSize;CriticalSamplesLearned;AvgAttemptsToLearn");
                foreach (var p in AllPatterns)
                {
                    sb.Append(';');
                    sb.Append(p.Name);
                }
                sb.AppendLine();
            }

            // Строка данных
            var ci = CultureInfo.InvariantCulture;
            sb.Append(done.ToString(ci));
            sb.Append(';').Append(stats.XWins.ToString(ci));
            sb.Append(';').Append(stats.OWins.ToString(ci));
            sb.Append(';').Append(stats.Draws.ToString(ci));
            sb.Append(';').Append(lastLoss.ToString("F4", ci));
            sb.Append(';').Append(epsilon.ToString("F3", ci));
            sb.Append(';').Append(stats.MissedWins.ToString(ci));
            sb.Append(';').Append(timeSeconds.ToString("F1", ci));
            sb.Append(';').Append(perPartyMs.ToString("F1", ci));
            sb.Append(';').Append(avgMoves.ToString("F2", ci));
            sb.Append(';').Append(criticalBufferSize.ToString(ci));
            sb.Append(';').Append(criticalSamplesLearned.ToString(ci));
            sb.Append(';').Append(avgAttemptsToLearn.ToString("F2", ci));

            foreach (var p in AllPatterns)
            {
                sb.Append(';');
                sb.Append(patternValues[p.Name].ToString("F4", ci));
            }
            sb.AppendLine();

            File.AppendAllText(path, sb.ToString());
        }
    }
}