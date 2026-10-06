using System;
using System.IO;
using System.Text;
using GomokuGame.Models;

namespace GomokuGame.AI
{
    /// <summary>
    /// Логгер партии в текстовый файл.
    /// Пишет каждый ход с псевдографикой доски.
    /// </summary>
    public class GameLogger
    {
        private readonly StringBuilder _sb = new();
        private readonly string _path;
        private int _moveNumber = 0;

        public GameLogger(string path)
        {
            _path = path;
            _sb.AppendLine($"=== Game started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        }

        public void LogHeader(int boardSize, int winLength,
                              string xPlayer, string oPlayer)
        {
            _sb.AppendLine($"Board: {boardSize}x{boardSize}, Win: {winLength}");
            _sb.AppendLine($"X: {xPlayer}");
            _sb.AppendLine($"O: {oPlayer}");
            _sb.AppendLine();
        }

        public void LogMove(GameBoard board, CellState player, int row, int col)
        {
            _moveNumber++;

            var opponent = player == CellState.X ? CellState.O : CellState.X;
            int lineLen = board.GetLineLengthAt(row, col, player);
            double myPotential = board.GetLinePotential(row, col, player, board.WinLength);
            double oppPotential = board.GetMaxPotential(opponent, board.WinLength);

            _sb.AppendLine($"Move {_moveNumber}: {player} -> ({row}, {col})   [line={lineLen}, myPotential={myPotential:F2}, oppPotential={oppPotential:F2}]");
            _sb.AppendLine(board.ToAsciiString());
            _sb.AppendLine();
        }

        public void LogResult(CellState winner, int moves)
        {
            string w = winner == CellState.Empty ? "Draw" : winner.ToString();
            _sb.AppendLine($"=== Result: {w} wins, moves: {moves} ===");
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(_path, _sb.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GameLogger.Save failed: {ex.Message}");
            }
        }
    }
}