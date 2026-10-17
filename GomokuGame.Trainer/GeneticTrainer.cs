using GomokuGame.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ============ GeneticTrainer.cs ============
namespace GomokuGame.Trainer
{
    public class GeneticTrainer
    {
        private int _generationCounter = 0;
        private int _cycleCounter = 0;

        private readonly int _population;
        private readonly int _eliteCount;
        private readonly double _mutationSigma;
        private readonly int _gamesPerPair;
        private readonly int _boardSize;
        private readonly int _winLength;
        private readonly int _chunkGames;
        private readonly string _saveName;

        private Individual[] _individuals = null!;

        public GeneticTrainer(int population, int eliteCount, double mutationSigma,
            int gamesPerPair, int boardSize, int winLength, int chunkGames, string saveName)
        {
            _population = population;
            _eliteCount = eliteCount;
            _mutationSigma = mutationSigma;
            _gamesPerPair = gamesPerPair;
            _boardSize = boardSize;
            _winLength = winLength;
            _chunkGames = chunkGames;
            _saveName = saveName;
        }

        public void InitializePopulation(string? existingNetworkPath)
        {
            _individuals = new Individual[_population];

            AiPlayer seedAi = existingNetworkPath != null && File.Exists(existingNetworkPath)
                ? AiPlayer.LoadOrCreate(existingNetworkPath)
                : AiPlayer.CreateFresh();

            for (int i = 0; i < _population; i++)
            {
                AiPlayer ai = i < _eliteCount
                    ? seedAi.Clone(/*cloneSeed:*/1000 + i)                 // чистый клон, без шума
                    : MutateFrom(seedAi, _mutationSigma, /*cloneSeed:*/ 1000 + i); // мутант

                _individuals[i] = new Individual { Id = i, Ai = ai };
            }
        }

        public void RunChunkCycle()
        {
            TrainPhase();
            TournamentPhase();
            var scores = _individuals.Select(p => p.Score).ToList();
            double avgScore = scores.Average();
            double worstScore = scores.Min();
            double bestScoreCheck = scores.Max(); // для сверки с best, должно совпасть с лучшей особью
            double mean = avgScore;
            double scoreStdDev = Math.Sqrt(scores.Sum(s => Math.Pow(s - mean, 2)) / scores.Count);

            var best = _individuals.OrderByDescending(p => p.Score).First();

            var patternValues = PatternProbe.EvaluateAll(best.Ai, _boardSize, _winLength);
            int gamesFrom = _cycleCounter * _chunkGames;
            int gamesTo = (_cycleCounter + 1) * _chunkGames;
            PatternProbe.LogToConsole(gamesFrom, gamesTo, patternValues);
            PatternProbe.AppendCsv(
                saveName: _saveName,
                cycle: _cycleCounter,
                mutationSigma: _mutationSigma,
                gamesFrom: gamesFrom,
                gamesTo: gamesTo,
                bestScore: best.Score,
                avgScore: avgScore,
                worstScore: worstScore,
                scoreStdDev: scoreStdDev,
                bestWins: best.Wins,
                bestDraws: best.Draws,
                bestLosses: best.Losses,
                lastLoss: best.LastLoss,
                epsilon: best.Epsilon,
                criticalBufferSize: best.CriticalBufferSize,
                criticalSamplesLearnedCount: best.CriticalSamplesLearnedCount,
                averageAttemptsToLearn: best.AverageAttemptsToLearn,
                patternValues: patternValues
            );

            SelectionAndBreedingPhase();
            SaveBestToDisk(best);
            _cycleCounter++;
        }

        // ---------- Фаза 1: обучение (конкурирует за потоки) ----------

        private void TrainPhase()
        {
            Parallel.ForEach(_individuals,
                new ParallelOptions { MaxDegreeOfParallelism = _population },
                individual =>
                {
                    var stats = GameSession.TrainMany(individual.Ai, _chunkGames, _boardSize, _winLength, progressEvery: _chunkGames);
                    individual.LastLoss = stats.LastAverageLoss;
                    individual.Epsilon = stats.Epsilon;
                    individual.CriticalBufferSize = stats.CriticalBufferSize;
                    individual.CriticalSamplesLearnedCount = stats.CriticalSamplesLearnedCount;
                    individual.AverageAttemptsToLearn = stats.AverageAttemptsToLearn;
                });
        }

        // ---------- Фаза 2: турнир all-vs-all, каждый за X и за O ----------

        private void TournamentPhase()
        {
            foreach (var ind in _individuals)
                ind.ResetTournamentStats();

            var tasks = new List<MatchTask>();
            for (int i = 0; i < _population; i++)
            {
                for (int j = 0; j < _population; j++)
                {
                    if (i == j) continue;
                    tasks.Add(new MatchTask(i, j)); // i играет X, j играет O — и наоборот будет отдельной парой
                }
            }

            Parallel.ForEach(tasks,
                new ParallelOptions { MaxDegreeOfParallelism = _population },
                task =>
                {
                    var aiX = _individuals[task.XIndex].Ai;
                    var aiO = _individuals[task.OIndex].Ai;

                    var stats = GameSession.PlayMatchSeriesParallel(
                        aiX, aiO, _gamesPerPair, _boardSize, _winLength, threadCount: 1);
                    // threadCount: 1 — партии этой пары не плодят собственные потоки,
                    // параллелизм уже обеспечен верхним Parallel.ForEach по задачам

                    double xScore = stats.XWins * 1.0 + stats.Draws * 0.5;
                    double oScore = stats.OWins * 1.0 + stats.Draws * 0.5;

                    lock (_individuals[task.XIndex])
                    {
                        _individuals[task.XIndex].Score += xScore;
                        _individuals[task.XIndex].Wins += stats.XWins;
                        _individuals[task.XIndex].Draws += stats.Draws;
                        _individuals[task.XIndex].Losses += stats.OWins;
                    }
                    lock (_individuals[task.OIndex])
                    {
                        _individuals[task.OIndex].Score += oScore;
                        _individuals[task.OIndex].Wins += stats.OWins;
                        _individuals[task.OIndex].Draws += stats.Draws;
                        _individuals[task.OIndex].Losses += stats.XWins;
                    }
                });
        }

        // ---------- Фаза 3: отбор + разведение по кругу ----------

        private void SelectionAndBreedingPhase()
        {
            var ranked = _individuals.OrderByDescending(i => i.Score).ToArray();
            var elite = ranked.Take(_eliteCount).ToArray();
            var rest = ranked.Skip(_eliteCount).ToArray();

            for (int i = 0; i < rest.Length; i++)
            {
                var parent = elite[i % elite.Length]; // равномерно по кругу
                rest[i].Ai = MutateFrom(parent.Ai, _mutationSigma, cloneSeed: 2000 + i + _generationCounter * 100);
                rest[i].ResetTournamentStats();
            }

            _individuals = elite.Concat(rest).ToArray();
            _generationCounter++;
        }

        private static AiPlayer MutateFrom(AiPlayer parent, double sigma, int cloneSeed)
        {
            var clone = parent.Clone(cloneSeed);
            var rng = new Random(); // отдельный rng для самого шума, не для весов внутри Layer

            for (int li = 0; li < clone.Network.LayerCount; li++)
            {
                var layer = clone.Network.GetLayer(li);
                for (int r = 0; r < layer.W.Length; r++)
                {
                    for (int c = 0; c < layer.W[r].Length; c++)
                    {
                        layer.W[r][c] += NextGaussian(rng) * sigma;
                    }
                }
                // layer.B намеренно не трогаем — мутируем только веса
            }

            return clone;
        }

        private static double NextGaussian(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }



        public Individual GetBest() => _individuals.OrderByDescending(i => i.Score).First();



        private void SaveBestToDisk(Individual best)
        {
            best.Ai.NetworkPath = Program.GetSavePath(_saveName);
            best.Ai.Save();
        }
    }
}
