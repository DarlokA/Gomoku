using GomokuGame.AI;
using GomokuGame.Models;
using System;
using System.Globalization;

namespace VisualizerDemo
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Gomoku Network Visualizer — Что видит сеть по ходу игры ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Параметры
            int boardSize = 9;
            int winLength = 5;
            int gamesPerChunk = 10;   // после каждого чанка покажем вероятности
            double lr = 0.001;
            double epsilon = 0.3;
            int seed = 42;
            string modeStr = "C";     // DiscountedWithShaping

            // Создаём две случайные сети (epsilon=1.0 для ε-greedy)
            var aiX = AiPlayer.CreateFresh(seed: 1);
            aiX.Epsilon = 1.0;
            var aiO = AiPlayer.CreateFresh(seed: 2);
            aiO.Epsilon = 1.0;

            Console.WriteLine($"Созданы сети X (seed={1}) и O (seed={2}), Epsilon=1.0");
            Console.WriteLine();

            // Сыграем матч
            PlayMatch(aiX, aiO, boardSize, winLength);
        }

        private static void PlayMatch(AiPlayer aiX, AiPlayer aiO, int boardSize, int winLength)
        {
            var board = new GameBoard(boardSize, winLength);
            CellState current = CellState.X;
            int moves = 0;

            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Матч: X vs O ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            while (true)
            {
                var ai = current == CellState.X ? aiX : aiO;
                var move = ai.ChooseMove(board, current);
                if (move == null) break;

                var (row, col) = move.Value;
                board[row, col] = current;
                moves++;

                // Получаем вероятность для центра доски (4,4)
                int centerRow = boardSize / 2;
                int centerCol = boardSize / 2;

                double prob = ai.EvaluateMove(board, centerRow, centerCol, current);

                // Цветовая кодировка (ANSI escape codes)
                string colorX = "\033[92m";   // зелёный — свои
                string colorO = "\033[91m";   // красный — чужие
                string colorBorder = "\033[94m"; // синий — пусто/граница
                string reset = "\033[0m";

                Console.WriteLine($"╔══════════════════════════════════════════════════════════╗");
                var playerSymbol = current == CellState.X ? "X" : "O";
                var playerColor = current == CellState.X ? colorX : colorO;
                Console.WriteLine($"║  Ход {moves,2}: {playerColor}{playerSymbol}{reset} → ({row},{col}) ║");

                // Показываем вероятность для центра доски
                double oppProb = prob > 0.5 ? (1 - prob) : prob;
                Console.WriteLine($"║  Центр (4,4): P(X) = {prob:P3}, P(O) = {oppProb:P3} ║");

                // Показываем доску с вероятностями для всех клеток
                ShowBoardWithProbabilities(board, aiX, boardSize, winLength);

                var winLine = board.GetWinningLine(row, col, current);
                if (winLine != null)
                {
                    Console.WriteLine($"╚══════════════════════════════════════════════════════════╝");
                    Console.WriteLine($"║  ПОБЕДА: {playerColor}{playerSymbol}{reset}, ходы: {moves} ║");
                    Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
                    break;
                }

                if (board.IsFull())
                {
                    Console.WriteLine($"╚══════════════════════════════════════════════════════════╝");
                    Console.WriteLine("║  НИЧЬЯ — доска заполнена ║");
                    Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
                    break;
                }

                current = current == CellState.X ? CellState.O : CellState.X;
            }
        }

        private static void ShowBoardWithProbabilities(GameBoard board, AiPlayer aiX, int boardSize, int winLength)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║  Вероятности для всех клеток (центр = {boardSize/2},{boardSize/2}) ║");

            // Используем ANSI цвета для консоли
            string colorX = "\033[92m";   // зелёный — свои
            string colorO = "\033[91m";   // красный — чужие
            string colorBorder = "\033[94m"; // синий — пусто/граница
            string reset = "\033[0m";

            for (int row = 0; row < boardSize; row++)
            {
                Console.Write("╠═══════");
                for (int col = 0; col < boardSize; col++)
                {
                    int cell = row * boardSize + col;

                    // Получаем вероятность для этой клетки
                    double prob = aiX.EvaluateMove(board, row, col, CellState.X);

                    string cellColor;
                    if (prob > 0.5)
                        cellColor = colorX + "X" + reset;
                    else
                        cellColor = colorBorder + "+" + reset;

                    Console.Write($" {cellColor}{(int)(board[row, col] == CellState.Empty ? 0 : board[row, col])}{reset}");
                }
                Console.WriteLine("═╩═══════");
            }

            Console.WriteLine();
        }
    }
}