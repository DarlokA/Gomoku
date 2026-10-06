using System;
using System.Threading;
using GomokuGame.AI;
using GomokuGame.Models;

namespace GomokuGame.AI
{
    public class GameResult
    {
        public CellState Winner { get; set; }   // X, O, или Empty (ничья)
        public int Moves { get; set; }
        public double AverageLoss { get; set; }
        public int MissedWins { get; set; }
    }
    /// <summary>
    /// Играть без исследования (для турниров).
    /// </summary>
    public class TrainingStats
    {
        public int GamesPlayed { get; set; }
        public int XWins { get; set; }
        public int OWins { get; set; }
        public int Draws { get; set; }
        public double LastAverageLoss { get; set; }
        public double Epsilon { get; set; }
        public int totalMoves { get; set; }
        public int MissedWins { get; set; }
        public int ForcedCorrectionCount { get; set; }
        public int TotalSamplesCount { get; set; }
        public int CriticalBufferSize { get; set; }
        public int CriticalSamplesLearnedCount { get; set; }
        public double AverageAttemptsToLearn { get; set; }

        public double XWinRate => GamesPlayed == 0 ? 0 : (double)XWins / GamesPlayed;
        public double OWinRate => GamesPlayed == 0 ? 0 : (double)OWins / GamesPlayed;
        public double DrawRate => GamesPlayed == 0 ? 0 : (double)Draws / GamesPlayed;
        public double AverageMovies => GamesPlayed == 0 ? 0 : (double)totalMoves / GamesPlayed;
        public double MissedWinRate => GamesPlayed == 0 ? 0 : (double)MissedWins / GamesPlayed;
    }

    public class MatchStats
    {
        public int Games { get; set; }
        public int XWins { get; set; }
        public int OWins { get; set; }
        public int Draws { get; set; }
        public int totalMoves { get; set; }

        public double XWinRate => Games == 0 ? 0 : (double)XWins / Games;
        public double OWinRate => Games == 0 ? 0 : (double)OWins / Games;
        public double DrawRate => Games == 0 ? 0 : (double)Draws / Games;
        public double AverageMovies => Games == 0 ? 0 : (double)totalMoves / Games;

        public override string ToString() =>
            $"X: {XWins,5} ({XWinRate:P1})  " +
            $"O: {OWins,5} ({OWinRate:P1})  " +
            $"D: {Draws,5} ({DrawRate:P1})" +
            $"Total movies: {totalMoves,5} avg: {AverageMovies:P1}";
    }

    public static class GameSession
    {

        /// <summary>
        /// Единый Random для аугментации ориентаций.
        /// Управляется через SetSeed() для воспроизводимости.
        /// </summary>
        private static Random _orientationRng = new Random(42);

        /// <summary>
        /// Установить seed для воспроизводимости.
        /// Вызывать перед началом обучения.
        /// </summary>
        public static void SetSeed(int seed)
        {
            _orientationRng = new Random(seed);
        }
        /// <summary>
        /// Сыграть одну партию AI vs AI с обучением.
        /// </summary>
        public static GameResult PlayOneGame(
            AiPlayer ai,
            int boardSize,
            int winLength)
        {
            ai.ResetHistory();

            var board = new GameBoard(boardSize, winLength);
            var current = CellState.X; // X всегда ходит первым
            int moves = 0;
            int missedWinsCount = 0;

            while (true)
            {
                var move = ai.ChooseMove(board, current);
                if (move == null) break;

                var (row, col) = move.Value;
                var bestCandidate = ai.LastBestCandidate;
                var mostCritical = ai.LastMostCritical;

                // === ε-greedy: случайный ход без сбора семпла ===
                if (bestCandidate == null)
                {
                    board[row, col] = current;
                    moves++;

                    var winLineRandom = board.GetWinningLine(row, col, current);
                    if (winLineRandom != null)
                    {
                        double lossRandom = ai.LearnFromGame(current, winLength);
                        return new GameResult { Winner = current, Moves = moves, AverageLoss = lossRandom };
                    }

                    if (board.IsFull())
                    {
                        double lossRandom = ai.LearnFromGame(CellState.Empty, winLength);
                        return new GameResult { Winner = CellState.Empty, Moves = moves, AverageLoss = lossRandom };
                    }

                    current = current == CellState.X ? CellState.O : CellState.X;
                    continue;
                }

                // === Логика выбора семпла ===
                bool missedWin = MoveCriticality.MissedWin(board, bestCandidate.Row, bestCandidate.Col, current, winLength);
                if (missedWin) missedWinsCount++;
                bool threatWasVisible = mostCritical != null && mostCritical.Criticality >= 0.9;
                bool playedCorrectly = bestCandidate.Criticality >= 0.9;

                MoveCandidate? sampleToRecord;
                double weight;
                bool isForcedCorrection;

                if (missedWin)
                {
                    sampleToRecord = mostCritical;
                    weight = 5.0;
                    isForcedCorrection = true;
                }
                else if (threatWasVisible && !playedCorrectly)
                {
                    sampleToRecord = mostCritical;
                    isForcedCorrection = true;

                    // Гонка: AI не просто пропустил защиту, а ВМЕСТО блока разогнал свою
                    // линию до того же уровня критичности (need=1) — то есть выбрал ход,
                    // который выглядит как "я тоже скоро выиграю", хотя следующий ход
                    // достанется противнику, и тот выигрывает первым. Это строго худшая
                    // ошибка, чем обычный пропуск защиты без видимой "своей" альтернативы.
                    board[bestCandidate.Row, bestCandidate.Col] = current;
                    double ownPotentialAfterChosen =
                        board.GetLinePotential(bestCandidate.Row, bestCandidate.Col, current, winLength);
                    board[bestCandidate.Row, bestCandidate.Col] = CellState.Empty;

                    bool isRaceMistake = ownPotentialAfterChosen >= 5.0;
                    weight = isRaceMistake ? 8.0 : 5.0;
                }
                else if (playedCorrectly)
                {
                    sampleToRecord = bestCandidate;
                    weight = 3.0;
                    isForcedCorrection = false;
                }
                else
                {
                    sampleToRecord = bestCandidate;
                    weight = 1.0;
                    isForcedCorrection = false;
                }

                // === Симулируем ход sampleToRecord для потенциалов (до реального хода) ===
                if (sampleToRecord != null)
                {
                    int sr = sampleToRecord.Row;
                    int sc = sampleToRecord.Col;

                    board[sr, sc] = current;
                    int sampleLineLen = board.GetLineLengthAt(sr, sc, current);
                    double sampleMyPotential = board.GetLinePotential(sr, sc, current, winLength);
                    var opponent = current == CellState.X ? CellState.O : CellState.X;
                    double sampleOppPotential = board.GetMaxPotential(opponent, winLength);
                    board[sr, sc] = CellState.Empty;

                    // Для максимально критичных семплов (пропуск победы / видимая угроза) —
                    // размножаем в 4 симметриях для постоянного буфера
                    // correctionMode == Off: weight = 0.0.В эту ветку не попадем.
                    double[][]? allOrientationStates = null;
                    if (weight >= 5.0)
                    {
                        allOrientationStates = new double[4][];
                        for (int ori = 0; ori < 4; ori++)
                            allOrientationStates[ori] = StateEncoder.Encode(board, sr, sc, current, ori);
                    }

                    ai.RecordMoveWithWeight(
                        sampleToRecord.State,
                        sr, sc,
                        current,
                        sampleLineLen,
                        sampleMyPotential,
                        sampleOppPotential,
                        weight,
                        criticality: sampleToRecord.Criticality,
                        isForcedCorrection: isForcedCorrection,
                        allOrientationStates);
                }

                // === Реальный ход ===
                board[row, col] = current;
                moves++;

                var winLine = board.GetWinningLine(row, col, current);
                if (winLine != null)
                {
                    double loss = ai.LearnFromGame(current, winLength);
                    return new GameResult { Winner = current, Moves = moves, AverageLoss = loss, MissedWins = missedWinsCount };
                }

                if (board.IsFull())
                {
                    double loss = ai.LearnFromGame(CellState.Empty, winLength);
                    return new GameResult { Winner = CellState.Empty, Moves = moves, AverageLoss = loss, MissedWins = missedWinsCount };
                }

                current = current == CellState.X ? CellState.O : CellState.X;
            }

            // Сюда не должны попасть, но на всякий случай
            double finalLoss = ai.LearnFromGame(CellState.Empty, winLength);
            return new GameResult
            {
                Winner = CellState.Empty,
                Moves = moves,
                AverageLoss = finalLoss,
                MissedWins = missedWinsCount
            };
        }

        /// <summary>
        /// Прогнать N партий с обучением. Периодически дёргает onProgress.
        /// </summary>
        public static TrainingStats TrainMany(
            AiPlayer ai,
            int games,
            int boardSize,
            int winLength,
            Action<TrainingStats>? onProgress = null,
            CancellationToken ct = default,
            int progressEvery = 100)
        {
            var stats = new TrainingStats();

            for (int i = 0; i < games; i++)
            {
                if (ct.IsCancellationRequested) break;

                GameResult result = PlayOneGame(ai, boardSize, winLength);

                stats.GamesPlayed++;
                stats.totalMoves += result.Moves;
                stats.MissedWins += result.MissedWins;
                stats.CriticalBufferSize = ai.CriticalBuffer.BufferSize;
                stats.CriticalSamplesLearnedCount = ai.CriticalBuffer.LearnedSamplesCount;
                stats.AverageAttemptsToLearn = ai.CriticalBuffer.AverageAttemptsToLearn;
                switch (result.Winner)
                {
                    case CellState.X: stats.XWins++; break;
                    case CellState.O: stats.OWins++; break;
                    default: stats.Draws++; break;
                }
                stats.LastAverageLoss = result.AverageLoss;
                stats.Epsilon = ai.Epsilon;

                if ((i + 1) % progressEvery == 0 || i == games - 1)
                {
                    onProgress?.Invoke(stats);
                }
            }

            return stats;
        }


        /// <summary>
        /// Матч между двумя игроками с подробным логом каждого хода.
        /// Без обучения. После каждого хода пишет координаты, длину линии
        /// и число открытых концов — чтобы ловить открытые/полуоткрытые угрозы.
        /// </summary>
        public static GameResult PlayMatchWithLog(
            AiPlayer aiX,
            AiPlayer aiO,
            int boardSize,
            int winLength,
            System.IO.TextWriter log)
        {
            var board = new GameBoard(boardSize, winLength);
            var current = CellState.X;
            int moves = 0;

            log.WriteLine($"=== Match with log: {boardSize}x{boardSize}, win {winLength} ===");
            log.WriteLine();

            while (true)
            {
                var ai = current == CellState.X ? aiX : aiO;
                var move = ai.ChooseMove(board, current);
                if (move == null) break;

                var (row, col) = move.Value;
                board[row, col] = current;
                moves++;

                var (len, openEnds, _, _) = board.GetMaxLineWithOpenEnds(row, col, current);

                log.WriteLine($"Move {moves,3}: {current} -> ({row},{col})  line={len} openEnds={openEnds}");
                log.WriteLine(board.ToAsciiString());
                log.WriteLine();

                var winLine = board.GetWinningLine(row, col, current);
                if (winLine != null)
                {
                    log.WriteLine($"=== WINNER: {current}, moves: {moves} ===");
                    log.WriteLine(board.ToAsciiString(winLine));
                    return new GameResult { Winner = current, Moves = moves };
                }

                if (board.IsFull())
                {
                    log.WriteLine("=== DRAW ===");
                    return new GameResult { Winner = CellState.Empty, Moves = moves };
                }

                current = current == CellState.X ? CellState.O : CellState.X;
            }

            log.WriteLine("=== DRAW (no moves) ===");
            return new GameResult { Winner = CellState.Empty, Moves = moves };
        }

        /// <summary>
        /// Матч между двумя игроками. Без обучения.
        /// aiX играет за X, aiO — за O.
        /// </summary>
        public static GameResult PlayMatch(
            AiPlayer aiX,
            AiPlayer aiO,
            int boardSize,
            int winLength)
        {
            var board = new GameBoard(boardSize, winLength);
            var current = CellState.X;
            int moves = 0;

            while (true)
            {
                var ai = current == CellState.X ? aiX : aiO;
                var move = ai.ChooseMove(board, current);
                if (move == null) break;

                var (row, col) = move.Value;
                board[row, col] = current;
                moves++;

                var winLine = board.GetWinningLine(row, col, current);
                if (winLine != null)
                {
                    return new GameResult { Winner = current, Moves = moves };
                }

                if (board.IsFull())
                {
                    return new GameResult { Winner = CellState.Empty, Moves = moves };
                }

                current = current == CellState.X ? CellState.O : CellState.X;
            }

            return new GameResult { Winner = CellState.Empty, Moves = moves };
        }

        public static MatchStats PlayMatchSeries(
            AiPlayer aiX,
            AiPlayer aiO,
            int games,
            int boardSize,
            int winLength)
        {
            var stats = new MatchStats { Games = games };

            for (int i = 0; i < games; i++)
            {
                var result = PlayMatch(aiX, aiO, boardSize, winLength);
                stats.totalMoves += result.Moves;
                switch (result.Winner)
                {
                    case CellState.X: stats.XWins++; break;
                    case CellState.O: stats.OWins++; break;
                    default: stats.Draws++; break;
                }
            }

            return stats;
        }

        public static MatchStats PlayMatchSeriesParallel(
                AiPlayer aiX,
                AiPlayer aiO,
                int games,
                int boardSize,
                int winLength,
                int threadCount)
        {
            int gamesPerThread = games / threadCount;
            int remainder = games % threadCount;

            var results = new (int x, int o, int d, int m)[threadCount];

            Parallel.For(0, threadCount, t =>
            {
                int myGames = gamesPerThread + (t < remainder ? 1 : 0);

                // Копия сетей для этого потока
                var localX = aiX.Clone(seed: t * 31 + 1);
                var localO = aiO.Clone(seed: t * 31 + 1001);

                int x = 0, o = 0, d = 0, m = 0;
                for (int i = 0; i < myGames; i++)
                {
                    var result = PlayMatch(localX, localO, boardSize, winLength);
                    m += result.Moves;
                    switch (result.Winner)
                    {
                        case CellState.X: x++; break;
                        case CellState.O: o++; break;
                        default: d++; break;
                    }
                }
                results[t] = (x, o, d, m);
            });

            int totalX = 0, totalO = 0, totalD = 0, totalM = 0;
            foreach (var r in results)
            {
                totalX += r.x;
                totalO += r.o;
                totalD += r.d;
                totalM += r.m;
            }

            return new MatchStats
            {
                Games = games,
                XWins = totalX,
                OWins = totalO,
                Draws = totalD,
                totalMoves = totalM,
            };
        }

        /// <summary>
        /// Партия: обучаемый игрок против замороженного.
        /// learnerPlays — за кого играет learner (X или O).
        /// Обучается только learner. Ходы frozenOpponent в историю не пишутся.
        /// </summary>
        public static GameResult PlayOneGameAgainstFixed(
            AiPlayer learner,
            AiPlayer frozenOpponent,
            CellState learnerPlays,
            int boardSize,
            int winLength)
        {
            learner.ResetHistory();

            var board = new GameBoard(boardSize, winLength);
            var current = CellState.X;
            int moves = 0;

            while (true)
            {
                bool isLearner = current == learnerPlays;
                var ai = isLearner ? learner : frozenOpponent;

                var move = ai.ChooseMove(board, current);
                if (move == null) break;

                var (row, col) = move.Value;

                double[]? stateBefore = null;
                if (isLearner)
                {
                    int orientation = _orientationRng.Next(4);
                    stateBefore = StateEncoder.Encode(board, row, col, current, orientation);
                }
                

                board[row, col] = current;
                moves++;

                if (isLearner)
                {
                    int lineLen = board.GetLineLengthAt(row, col, current);
                    double myPotential = board.GetLinePotential(row, col, current, winLength);
                    var opponent = current == CellState.X ? CellState.O : CellState.X;
                    double oppPotential = board.GetMaxPotential(opponent, winLength);

                    learner.RecordMove(stateBefore!, row, col, current,
                                       lineLen, myPotential, oppPotential);
                }

                var winLine = board.GetWinningLine(row, col, current);
                if (winLine != null)
                {
                    double loss = learner.LearnFromGame(current, winLength);
                    return new GameResult
                    {
                        Winner = current,
                        Moves = moves,
                        AverageLoss = loss
                    };
                }

                if (board.IsFull())
                {
                    double loss = learner.LearnFromGame(CellState.Empty, winLength);
                    return new GameResult
                    {
                        Winner = CellState.Empty,
                        Moves = moves,
                        AverageLoss = loss
                    };
                }

                current = current == CellState.X ? CellState.O : CellState.X;
            }

            double finalLoss = learner.LearnFromGame(CellState.Empty, winLength);
            return new GameResult
            {
                Winner = CellState.Empty,
                Moves = moves,
                AverageLoss = finalLoss
            };
        }

        /// <summary>
        /// League-обучение:
        /// - Первый чанк (warmup) — обычный self-play.
        /// - Дальше — поочерёдно: learner за X против снимка, learner за O против снимка.
        /// - Снимок обновляется каждые snapshotEvery партий.
        /// </summary>
        public static TrainingStats TrainManyLeague(
            AiPlayer learner,
            int games,
            int boardSize,
            int winLength,
            int warmupGames = 500,
            int snapshotEvery = 100,
            Action<TrainingStats>? onProgress = null,
            CancellationToken ct = default,
            int progressEvery = 500)
        {
            var stats = new TrainingStats();

            // ===== Чанк 1: warmup (обычный self-play) =====
            if (warmupGames > 0)
            {
                int warmup = Math.Min(warmupGames, games);
                var warmupStats = TrainMany(learner, warmup, boardSize, winLength,
                                            progressEvery: warmup);

                AccumulateStats(stats, warmupStats);
                onProgress?.Invoke(stats);
            }

            int remaining = games - Math.Min(warmupGames, games);
            if (remaining <= 0) return stats;

            // Снимок после warmup
            var snapshot = learner.Clone(seed: 987654);
            snapshot.Epsilon = 0.1;

            int played = 0;
            int halfChunk = snapshotEvery / 2;

            while (played < remaining && !ct.IsCancellationRequested)
            {
                // Прогон A: learner за X против снимка
                int gamesThisRun = Math.Min(halfChunk, remaining - played);
                for (int i = 0; i < gamesThisRun; i++)
                {
                    var result = PlayOneGameAgainstFixed(
                        learner, snapshot, CellState.X, boardSize, winLength);

                    stats.GamesPlayed++;
                    switch (result.Winner)
                    {
                        case CellState.X: stats.XWins++; break;
                        case CellState.O: stats.OWins++; break;
                        default: stats.Draws++; break;
                    }
                    stats.LastAverageLoss = result.AverageLoss;
                    stats.Epsilon = learner.Epsilon;
                    stats.totalMoves += result.Moves;
                    stats.CriticalBufferSize = learner.CriticalBuffer.BufferSize;
                    stats.CriticalSamplesLearnedCount = learner.CriticalBuffer.LearnedSamplesCount;
                    stats.AverageAttemptsToLearn = learner.CriticalBuffer.AverageAttemptsToLearn;
                }
                played += gamesThisRun;

                // Прогон B: learner за O против снимка
                gamesThisRun = Math.Min(halfChunk, remaining - played);
                for (int i = 0; i < gamesThisRun; i++)
                {
                    var result = PlayOneGameAgainstFixed(
                        learner, snapshot, CellState.O, boardSize, winLength);

                    stats.GamesPlayed++;
                    switch (result.Winner)
                    {
                        case CellState.X: stats.XWins++; break;
                        case CellState.O: stats.OWins++; break;
                        default: stats.Draws++; break;
                    }
                    stats.LastAverageLoss = result.AverageLoss;
                    stats.Epsilon = learner.Epsilon;
                    stats.totalMoves += result.Moves;
                    stats.CriticalBufferSize = learner.CriticalBuffer.BufferSize;
                    stats.CriticalSamplesLearnedCount = learner.CriticalBuffer.LearnedSamplesCount;
                    stats.AverageAttemptsToLearn = learner.CriticalBuffer.AverageAttemptsToLearn;
                }
                played += gamesThisRun;

                // Обновляем снимок
                snapshot = learner.Clone(seed: 987654);
                snapshot.Epsilon = 0.1;

                if (played % progressEvery == 0 || played >= remaining)
                    onProgress?.Invoke(stats);
            }

            return stats;
        }

        private static void AccumulateStats(TrainingStats target, TrainingStats source)
        {
            target.GamesPlayed += source.GamesPlayed;
            target.XWins += source.XWins;
            target.OWins += source.OWins;
            target.Draws += source.Draws;
            target.LastAverageLoss = source.LastAverageLoss;
            target.Epsilon = source.Epsilon;
            target.totalMoves += source.totalMoves;
            target.MissedWins += source.MissedWins;
        }



        /// <summary>
        /// Партия: обучаемый игрок (learner) против бота.
        /// learnerPlays — за кого играет learner (X или O).
        /// Обучается только learner. Ходы бота в историю learner не пишутся.
        /// </summary>
        public static GameResult PlayOneGameAgainstBot(
            AiPlayer learner,
            RotatingBot bot,
            CellState learnerPlays,
            int boardSize,
            int winLength)
        {
            learner.ResetHistory();

            var board = new GameBoard(boardSize, winLength);
            var current = CellState.X;
            int moves = 0;

            while (true)
            {
                bool isLearner = current == learnerPlays;

                (int Row, int Col)? move;
                if (isLearner)
                    move = learner.ChooseMove(board, current);
                else
                    move = bot.ChooseMove(board, current);

                if (move == null) break;

                var (row, col) = move.Value;

                double[]? stateBefore = null;
                if (isLearner)
                {
                    int orientation = _orientationRng.Next(4);
                    stateBefore = StateEncoder.Encode(board, row, col, current, orientation);   // случайный поворот — для симметричного обучения
                }
                
                board[row, col] = current;
                moves++;

                if (isLearner)
                {
                    int lineLen = board.GetLineLengthAt(row, col, current);
                    double myPotential = board.GetLinePotential(row, col, current, winLength);
                    var opponent = current == CellState.X ? CellState.O : CellState.X;
                    double oppPotential = board.GetMaxPotential(opponent, winLength);

                    learner.RecordMove(stateBefore!, row, col, current,
                                       lineLen, myPotential, oppPotential);
                }

                var winLine = board.GetWinningLine(row, col, current);
                if (winLine != null)
                {
                    double loss = learner.LearnFromGame(current, winLength);
                    return new GameResult
                    {
                        Winner = current,
                        Moves = moves,
                        AverageLoss = loss
                    };
                }

                if (board.IsFull())
                {
                    double loss = learner.LearnFromGame(CellState.Empty, winLength);
                    return new GameResult
                    {
                        Winner = CellState.Empty,
                        Moves = moves,
                        AverageLoss = loss
                    };
                }

                current = current == CellState.X ? CellState.O : CellState.X;
            }

            double finalLoss = learner.LearnFromGame(CellState.Empty, winLength);
            return new GameResult
            {
                Winner = CellState.Empty,
                Moves = moves,
                AverageLoss = finalLoss
            };
        }

        /// <summary>
        /// Обучение против бота с ЧАНКАМИ.
        /// Внутри чанка — одна ориентация бота (фиксированная).
        /// Между чанками — ориентация меняется (0 → 1 → 2 → 3 → 0 → ...).
        /// Половина партий — learner за X, половина — за O.
        /// </summary>
        public static TrainingStats TrainManyAgainstBot(
            AiPlayer learner,
            int games,
            int boardSize,
            int winLength,
            int chunkSize = 500,
            Action<TrainingStats>? onProgress = null)
        {
            var stats = new TrainingStats();
            int chunks = games / chunkSize;

            for (int chunk = 0; chunk < chunks; chunk++)
            {
                int orientation = chunk % 4;   // 0, 1, 2, 3, 0, 1, 2, 3, ...
                var bot = new RotatingBot(learner, orientation);

                for (int i = 0; i < chunkSize; i++)
                {
                    // Чередуем: чётные — learner за X, нечётные — за O
                    CellState learnerPlays = (i % 2 == 0) ? CellState.X : CellState.O;

                    var result = PlayOneGameAgainstBot(
                        learner, bot, learnerPlays, boardSize, winLength);

                    stats.GamesPlayed++;
                    switch (result.Winner)
                    {
                        case CellState.X: stats.XWins++; break;
                        case CellState.O: stats.OWins++; break;
                        default: stats.Draws++; break;
                    }
                    stats.LastAverageLoss = result.AverageLoss;
                    stats.Epsilon = learner.Epsilon;
                    stats.totalMoves += result.Moves;
                    stats.CriticalBufferSize = learner.CriticalBuffer.BufferSize;
                    stats.CriticalSamplesLearnedCount = learner.CriticalBuffer.LearnedSamplesCount;
                    stats.AverageAttemptsToLearn = learner.CriticalBuffer.AverageAttemptsToLearn;
                }

                onProgress?.Invoke(stats);
            }

            return stats;
        }

        /// <summary>
        /// Демо-партия между двумя сетями. Без обучения, без записи истории.
        /// Возвращает финальную доску, победителя, число ходов и выигрышную линию (если есть).
        /// </summary>
        public static (GameBoard Board, CellState Winner, int Moves,
                       IReadOnlyList<(int Row, int Col)>? WinLine)
            PlayDemoGame(
                AiPlayer aiX,
                AiPlayer aiO,
                int boardSize,
                int winLength)
        {
            var board = new GameBoard(boardSize, winLength);
            var current = CellState.X;
            int moves = 0;

            while (true)
            {
                var ai = current == CellState.X ? aiX : aiO;
                var move = ai.ChooseMove(board, current);
                if (move == null) break;

                var (row, col) = move.Value;
                board[row, col] = current;
                moves++;

                var winLine = board.GetWinningLine(row, col, current);
                if (winLine != null)
                    return (board, current, moves, winLine);

                if (board.IsFull())
                    return (board, CellState.Empty, moves, null);

                current = current == CellState.X ? CellState.O : CellState.X;
            }

            return (board, CellState.Empty, moves, null);
        }
    }
}