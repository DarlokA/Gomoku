using GomokuGame.Models;
using GomokuGame.Services;
using System;
using System.Collections.Generic;

namespace GomokuGame.AI
{
    public class AiPlayer
    {

        public enum LearningMode
        {
            /// <summary>Вариант A: reward = длина линии после хода, без исхода партии.</summary>
            LineLengthOnly,

            /// <summary>Вариант B: reward = дисконт от победы. Ходы победителя — 0.5 + 0.5·γ^dist, остальные — 0.5.</summary>
            DiscountedOutcome,

            /// <summary>Вариант C: дисконт + shaping за длину линии.</summary>
            DiscountedWithShaping,
            
            /// <summary>Вариант D: дисконт + shaping за потенциальную длину линии .</summary>
            AttackDefenseShaping

        }

        public enum CorrectionMode
        {
            Full,   // как сейчас: override + запись в буфер + TrainMix
            Once,   // override остаётся, но в буфер не кладём
            Off,    // override полностью отключён
            All     // override + запись в буфер + ПОЛНЫЙ прогон всего буфера каждую партию
        }


        public NeuralNetwork Network { get; set; }
        private readonly Random _rng;

        /// <summary>
        /// Последний выбранный ход (лучший по оценке сети) — из последнего ChooseMove.
        /// Используется в GameSession для логики обучения.
        /// </summary>
        public MoveCandidate? LastBestCandidate { get; private set; }

        /// <summary>
        /// Самый критичный семпл из последнего ChooseMove.
        /// Используется в GameSession для логики обучения.
        /// </summary>
        public MoveCandidate? LastMostCritical { get; private set; }

        /// <summary>
        /// Постоянный буфер редких критичных позиций — живёт вместе с AiPlayer
        /// на протяжении всего обучения, не очищается между партиями.
        /// </summary>
        public CriticalSampleBuffer CriticalBuffer { get; } = new CriticalSampleBuffer();

        private readonly List<MoveRecord> _gameHistory = new();
        private int _winLength = 5;

        // Гиперпараметры
        public double LearningRate { get; set; } = 0.01;
        public double Epsilon { get; set; } = 0.3;
        public double EpsilonMin { get; set; } = 0.02;
        public double EpsilonDecay { get; set; } = 0.995;

        public double AttackWeight { get; set; } = 0.4;    // α
        public double DefenseWeight { get; set; } = 0.6;   // β
        
        // Метаданные обучения (для диагностики)
        public int EpisodesTrained { get; set; }
        public double LastAverageLoss { get; set; }
        public double BestWinRate { get; set; }
        public bool UseRotatingEvaluation { get; set; } = false;

        // ← НОВОЕ: накопленная статистика по ВСЕМ сессиям обучения этой сети.
        // Нужна, чтобы при продолжении "бесконечного" обучения после перезапуска
        // статистика не стартовала каждый раз с нуля — она хранится в самом файле сети.
        public long TotalGamesPlayed { get; set; }
        public long TotalXWins { get; set; }
        public long TotalOWins { get; set; }
        public long TotalDraws { get; set; }
        public long TotalMissedWins { get; set; }
        public long TotalMoves { get; set; }

        /// <summary>
        /// ← НОВОЕ: скользящее окно последних значений loss — для детектора плато.
        /// Хранится ограниченным (см. MaxRecentLossHistory), чтобы файл сети не рос
        /// бесконечно при многочасовом/многодневном обучении.
        /// </summary>
        public List<double> RecentLossHistory { get; set; } = new();

        private const int MaxRecentLossHistory = 200; // ← НОВОЕ

        // Путь к файлу весов (для автосохранения)
        public string? NetworkPath { get; set; }

        public AiPlayer(NeuralNetwork network, Random? rng = null)
        {
            Network = network;
            _rng = rng ?? new Random();
        }

        /// <summary>
        /// ← НОВОЕ: добавить значение loss в скользящее окно, обрезая его до MaxRecentLossHistory.
        /// </summary>
        public void AppendLossHistory(double loss)
        {
            RecentLossHistory.Add(loss);
            while (RecentLossHistory.Count > MaxRecentLossHistory)
                RecentLossHistory.RemoveAt(0);
        }

        // ----- Создание / загрузка -----

        public static AiPlayer CreateFresh(int seed = 0)
        {
            var rng = seed == 0 ? new Random() : new Random(seed);
            var net = new NeuralNetwork(
                new Layer(StateEncoder.InputSize, 128, ActivationType.ReLU, rng),
                new Layer(128, 64, ActivationType.ReLU, rng),
                new Layer(64, 1, ActivationType.Sigmoid, rng));

            return new AiPlayer(net, rng);
        }

        /// <summary>
        /// Создаёт глубокую копию игрока с независимой сетью.
        /// Используется для параллельных матчей.
        /// </summary>
        public AiPlayer Clone(int seed)
        {
            var rng = new Random(seed);

            var layers = new Layer[Network.LayerCount];
            for (int i = 0; i < Network.LayerCount; i++)
            {
                var src = Network.GetLayer(i);
                var dst = new Layer(src.InputSize, src.OutputSize, src.Act, rng);

                for (int r = 0; r < src.OutputSize; r++)
                    Array.Copy(src.W[r], dst.W[r], src.InputSize);

                Array.Copy(src.B, dst.B, src.OutputSize);
                layers[i] = dst;
            }

            var newNet = new NeuralNetwork(layers);
            var copy = new AiPlayer(newNet, rng)
            {
                LearningRate = LearningRate,
                Epsilon = Epsilon,
                EpsilonMin = EpsilonMin,
                EpsilonDecay = EpsilonDecay,
                Mode = Mode,
                AttackWeight = AttackWeight,
                DefenseWeight = DefenseWeight,
                UseRotatingEvaluation = UseRotatingEvaluation,   // ← добавить
                NetworkPath = NetworkPath
            };

            return copy;
        }

        /// <summary>
        /// Загружает сеть из файла; если файла нет — создаёт новую.
        /// </summary>
        public static AiPlayer LoadOrCreate(string path, int seed = 0)
        {
            var player = NetworkSerializer.Load(path);
            if (player == null)
            {
                player = CreateFresh(seed);
            }
            player.NetworkPath = path;
            return player;
        }

        /// <summary>
        /// Сохраняет веса в файл (если путь задан).
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(NetworkPath)) return;
            NetworkSerializer.Save(this, NetworkPath);
        }

        // ----- Игра -----

        public double EvaluateMove(GameBoard board, int row, int col, CellState me)
        {
            if (UseRotatingEvaluation)
            {
                double sum = 0;
                for (int orient = 0; orient < 4; orient++)
                {
                    var input = StateEncoder.Encode(board, row, col, me, orient);
                    var output = Network.Forward(input);
                    sum += output[0];
                }
                return sum / 4.0;
            }
            else
            {
                var input = StateEncoder.Encode(board, row, col, me);
                var output = Network.Forward(input);
                return output[0];
            }
        }

        public (int Row, int Col)? ChooseMove(GameBoard board, CellState me)
        {
            var empties = new List<(int Row, int Col)>();
            for (int r = 0; r < board.Size; r++)
                for (int c = 0; c < board.Size; c++)
                    if (board[r, c] == CellState.Empty)
                        empties.Add((r, c));

            if (empties.Count == 0) return null;

            // ε-greedy: случайный ход (без сбора критических семплов)
            if (_rng.NextDouble() < Epsilon)
            {
                LastBestCandidate = null;
                LastMostCritical = null;
                return empties[_rng.Next(empties.Count)];
            }

            int orientation = 0;

            MoveCandidate? best = null;
            double bestValue = double.NegativeInfinity;
            double maxCriticality = 0;
            var tiedMostCritical = new List<MoveCandidate>();

            foreach (var (r, c) in empties)
            {
                var state = StateEncoder.Encode(board, r, c, me, orientation);
                double value = EvaluateMove(board, r, c, me);
                double criticality = MoveCriticality.ComputeCriticalityForMove(board, r, c, me, board.WinLength);

                var candidate = new MoveCandidate
                {
                    Row = r,
                    Col = c,
                    Player = me,
                    State = state,
                    Value = value,
                    Criticality = criticality
                };

                if (value > bestValue)
                {
                    bestValue = value;
                    best = candidate;
                }

                if (criticality > maxCriticality)
                {
                    maxCriticality = criticality;
                    tiedMostCritical.Clear();
                    tiedMostCritical.Add(candidate);
                }
                else if (criticality == maxCriticality && criticality > 0)
                {
                    tiedMostCritical.Add(candidate);
                }
            }

            LastBestCandidate = best;
            LastMostCritical = tiedMostCritical.Count > 0
                ? tiedMostCritical[_rng.Next(tiedMostCritical.Count)]
                : null;
            return best != null ? (best.Row, best.Col) : null;
        }

        /// <summary>
        /// Запись хода в историю с явным весом критичности.
        /// </summary>
        public void RecordMoveWithWeight(
            double[] stateBefore,
            int row, int col,
            CellState player,
            int lineLengthAfter,
            double attackPotential,
            double opponentPotential,
            double criticalityWeight,
            double criticality = 0.0,
            bool isForcedCorrection = false,
            double[][]? allOrientationStates = null,
            bool isCritical = false)
        {
            _gameHistory.Add(new MoveRecord(stateBefore, row, col, player)
            {
                LineLengthAfter = lineLengthAfter,
                AttackPotential = attackPotential,
                OpponentPotential = opponentPotential,
                CriticalityWeight = criticalityWeight,
                Criticality = criticality,
                IsForcedCorrection = isForcedCorrection,
                AllOrientationStates = allOrientationStates,
                IsCritical = isCritical
            });
        }

        public void RecordMove(
                double[] stateBefore,
                int row, int col,
                CellState player,
                int lineLengthAfter,
                double attackPotential,
                double opponentPotential)
        {
            _gameHistory.Add(new MoveRecord(stateBefore, row, col, player)
            {
                LineLengthAfter = lineLengthAfter,
                AttackPotential = attackPotential,
                OpponentPotential = opponentPotential
            });
        }

        public void RecordState(GameBoard board, int row, int col, CellState player)
        {
            var state = StateEncoder.Encode(board, row, col, player);
            _gameHistory.Add(new MoveRecord(state, row, col, player));
        }

        public LearningMode Mode { get; set; } = LearningMode.DiscountedWithShaping;

        public CorrectionMode Correction { get; set; } = CorrectionMode.Full;

        public double Discount { get; set; } = 0.9;

        public double LearnFromGame(CellState winner, int winLength)
        {
            _winLength = winLength;
            if (_gameHistory.Count == 0) return 0;

            int count = _gameHistory.Count;
            double totalLoss = 0;

            // Индекс последнего хода победителя (если есть победитель)
            int lastWinnerIdx = -1;
            if (winner != CellState.Empty)
            {
                for (int i = _gameHistory.Count - 1; i >= 0; i--)
                {
                    if (_gameHistory[i].Player == winner)
                    {
                        lastWinnerIdx = i;
                        break;
                    }
                }
            }

            int criticalCountThisGame = 0;
            bool reverseOrder = (EpisodesTrained % 2) == 1;

            for (int i = 0; i < _gameHistory.Count; i++)
            {
                var move = _gameHistory[i];
                double target = ComputeTarget(move, i, lastWinnerIdx, winner);
                double effectiveLr = LearningRate * move.CriticalityWeight;

                if (move.AllOrientationStates != null)
                {
                    // Гарантированный x4: та же target на всех 4 поворотах одной позиции.
                    // Чередуем порядок обхода по чётности партии, чтобы одна и та же
                    // ориентация не оказывалась систематически "последней" на общих весах.
                    double sumLoss = 0;
                    int n = move.AllOrientationStates.Length;
                    for (int k = 0; k < n; k++)
                    {
                        int ori = reverseOrder ? n - 1 - k : k;
                        sumLoss += Network.TrainOnExample(move.AllOrientationStates[ori], new[] { target }, effectiveLr);
                    }
                    totalLoss += sumLoss / n;
                }
                else
                {
                    // Страховка на случай сэмплов без ориентаций (например, из других путей обучения).
                    totalLoss += Network.TrainOnExample(move.State, new[] { target }, effectiveLr);
                }

                // В постоянный буфер — только по явному флагу критичности,
                // а не по тому, посчитаны ли 4 ориентации (теперь они есть у всех).
                if (move.IsCritical && (Correction == CorrectionMode.Full || Correction == CorrectionMode.All))
                {
                    criticalCountThisGame++;
                    long key = CriticalSampleBuffer.ComputeKey(move.AllOrientationStates![0]);
                    CriticalBuffer.AddOrUpdate(move.AllOrientationStates, target, move.CriticalityWeight, key);
                }
            }

            // Full/Once/Off — дозированная подмесь, как раньше.
            // All — принудительный проход по ВСЕМУ текущему буферу каждую партию.
            int mixCount = Correction == CorrectionMode.All
                            ? CriticalBuffer.BufferSize
                            : criticalCountThisGame * 2;
            CriticalBuffer.TrainMix(Network, LearningRate, mixCount, reverseOrder);

            _gameHistory.Clear();
            LastAverageLoss = totalLoss / count;
            EpisodesTrained++;
            DecayEpsilon();
            return LastAverageLoss;
        }

        private double ComputeTarget(MoveRecord move, int idx, int lastWinnerIdx, CellState winner)
        {
            // Форсированная коррекция: это не реально сыгранный ход, а показанный сети
            // задним числом правильный ответ на прямую угрозу (своя победа или блок чужой).
            // Его цель — объективная критичность момента, а не то, как закончилась вся партия:
            // иначе дисконт по исходу и shaping по чужому потенциалу могут обнулить или даже
            // развернуть сигнал ровно там, где он важнее всего.
            if (move.IsForcedCorrection)
            {
                return Math.Clamp(move.Criticality, 0.0, 1.0);
            }

            switch (Mode)
            {
                case LearningMode.LineLengthOnly:
                    return ComputeLineLengthTarget(move);

                case LearningMode.DiscountedOutcome:
                    return ComputeDiscountedTarget(move, idx, lastWinnerIdx, winner);

                case LearningMode.DiscountedWithShaping:
                    double baseT = ComputeDiscountedTarget(move, idx, lastWinnerIdx, winner);
                    double shaping = ComputeShaping(move);
                    return Math.Clamp(baseT + shaping, 0.0, 1.0);
                case LearningMode.AttackDefenseShaping:
                    return ComputeAttackDefenseTarget(move, idx, lastWinnerIdx, winner);
                default:
                    return 0.5;
            }
        }

        private double ComputeAttackDefenseTarget(
            MoveRecord move, int idx, int lastWinnerIdx, CellState winner)
        {
            // База: дисконтированный исход
            double baseTarget = ComputeDiscountedTarget(move, idx, lastWinnerIdx, winner);

            // Shaping: атака — свой потенциал, защита — потенциал противника
            // winLength — надо знать! Возьмём извне.
            double attack = Math.Min(1.0, (double)move.AttackPotential / _winLength);
            double defense = Math.Min(1.0, (double)move.OpponentPotential / _winLength);

            // Плохо, если потенциал противника ВЫСОКИЙ
            double shaping = AttackWeight * attack - DefenseWeight * defense;

            return Math.Clamp(baseTarget + shaping, 0.0, 1.0);
        }

        /// <summary>
        /// Reward только за длину линии. Победа / поражение — не учитываются.
        /// </summary>
        private static double ComputeLineLengthTarget(MoveRecord move)
        {
            // lineLen = 1 → 0.0; 2 → 0.25; 3 → 0.5; 4 → 0.75; 5 → 1.0
            double t = (move.LineLengthAfter - 1) / 4.0;
            return Math.Clamp(t, 0.0, 1.0);
        }

        /// <summary>
        /// Дисконтированный финальный исход. Ходы победителя: 0.5 + 0.5·γ^dist. Остальные: 0.5.
        /// </summary>
        private double ComputeDiscountedTarget(MoveRecord move, int idx, int lastWinnerIdx, CellState winner)
        {
            if (winner == CellState.Empty) return 0.5;

            if (move.Player == winner && lastWinnerIdx >= 0)
            {
                int dist = lastWinnerIdx - idx;
                return 0.5 + 0.5 * Math.Pow(Discount, dist);
            }
            return 0.5;
        }

        /// <summary>
        /// Shaping: небольшой бонус за длину линии. Максимум +0.1.
        /// </summary>
        private static double ComputeShaping(MoveRecord move)
        {
            // lineLen = 1 → 0; 2 → 0.025; 3 → 0.05; 4 → 0.075; 5 → 0.1
            double t = (move.LineLengthAfter - 1) / 4.0;
            return 0.1 * Math.Clamp(t, 0.0, 1.0);
        }

        public void SetDeterministic()
        {
            Epsilon = 0.0;
        }

        public void DecayEpsilon()
        {
            Epsilon = Math.Max(EpsilonMin, Epsilon * EpsilonDecay);
        }

        public void ResetHistory() => _gameHistory.Clear();
        public int HistoryCount => _gameHistory.Count;
    }
}