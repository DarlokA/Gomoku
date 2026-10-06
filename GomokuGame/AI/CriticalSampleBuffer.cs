using System;
using System.Collections.Generic;

namespace GomokuGame.AI
{
    /// <summary>
    /// Постоянный буфер редких критичных позиций (пропущенная или не до конца
    /// закрытая победная угроза соперника). Не чистится после каждой партии —
    /// семпл живёт, пока сеть не показала стабильно низкую ошибку на нём
    /// RequiredStreak проверок подряд, после чего считается выученным и удаляется.
    /// </summary>
    public class CriticalSampleBuffer
    {
        /// <summary>Порог ошибки, ниже которого проверка считается успешной.</summary>
        public double GoodErrorThreshold { get; set; } = 0.01;

        /// <summary>Сколько успешных проверок подряд нужно, чтобы считать семпл выученным.</summary>
        public int RequiredStreak { get; set; } = 3;

        /// <summary>Жёсткий потолок размера буфера — старые записи вытесняются при переполнении.</summary>
        public int MaxSize { get; set; } = 2000;

        private readonly Dictionary<long, CriticalSample> _byKey = new();
        private readonly List<long> _order = new();
        private readonly Random _rng = new();

        public int Count => _byKey.Count;

        /// <summary>Текущий размер буфера критичных семплов.</summary>
        public int BufferSize => _byKey.Count;

        /// <summary>Сколько семплов всего признано выученными с начала обучения.</summary>
        public int LearnedSamplesCount { get; private set; }

        /// <summary>Суммарное число попыток (проверок), потраченных на уже выученные семплы.</summary>
        public long TotalAttemptsToLearn { get; private set; }

        /// <summary>Среднее число попыток до признания семпла выученным.</summary>
        public double AverageAttemptsToLearn =>
            LearnedSamplesCount == 0 ? 0 : (double)TotalAttemptsToLearn / LearnedSamplesCount;

        /// <summary>
        /// Снимок всех текущих семплов буфера — для сохранения в файл сети.
        /// </summary>
        public List<CriticalSample> Snapshot() => new List<CriticalSample>(_byKey.Values);

        /// <summary>
        /// Восстанавливает буфер из снимка, прочитанного из файла сети.
        /// Порядок вытеснения при переполнении (какой семпл устареет первым) после
        /// restore восстанавливается не точно — это не критично, влияет только
        /// на редкий случай, когда буфер уже на старте окажется на пределе MaxSize.
        /// </summary>
        public void RestoreState(IEnumerable<CriticalSample> samples, int learnedCount, long totalAttemptsToLearn)
        {
            _byKey.Clear();
            _order.Clear();
            foreach (var sample in samples)
            {
                _byKey[sample.Key] = sample;
                _order.Add(sample.Key);
            }
            LearnedSamplesCount = learnedCount;
            TotalAttemptsToLearn = totalAttemptsToLearn;
        }

        /// <summary>
        /// Стабильный ключ канонической (orientation 0) позиции — бинарный паттерн
        /// состояния (каналы по факту 0.0/1.0), без зависимости от порядка float-шумов.
        /// </summary>
        public static long ComputeKey(double[] canonicalState)
        {
            unchecked
            {
                long hash = 17;
                for (int i = 0; i < canonicalState.Length; i++)
                {
                    int bit = canonicalState[i] > 0.5 ? 1 : 0;
                    hash = hash * 31 + bit;
                }
                return hash;
            }
        }

        /// <summary>
        /// Добавляет новый критичный семпл или обновляет целевое значение уже
        /// существующего (по ключу) и сбрасывает его счётчик успешных проверок —
        /// раз позиция снова встретилась как критичная, сеть её ещё не закрепила.
        /// </summary>
        public void AddOrUpdate(double[][] states, double target, double weight, long key)
        {
            if (_byKey.TryGetValue(key, out var existing))
            {
                existing.Target = target;
                existing.Weight = Math.Max(existing.Weight, weight);
                existing.ConsecutiveGoodChecks = 0;
                existing.AttemptsSinceAdded = 0;
                return;
            }

            if (_byKey.Count >= MaxSize)
            {
                int evictAt = 0;
                while (evictAt < _order.Count && !_byKey.ContainsKey(_order[evictAt]))
                    evictAt++;
                if (evictAt < _order.Count)
                {
                    _byKey.Remove(_order[evictAt]);
                    _order.RemoveAt(evictAt);
                }
            }

            _byKey[key] = new CriticalSample(states, target, weight, key);
            _order.Add(key);
        }

        /// <summary>
        /// Подмешивает в обучение случайную выборку из буфера (до sampleCount записей),
        /// тренирует на всех 4 симметриях каждой записи, обновляет счётчик успешных
        /// проверок по средней ошибке и удаляет записи, которые сеть выучила устойчиво.
        /// </summary>
        public void TrainMix(NeuralNetwork network, double baseLearningRate, int sampleCount)
        {
            if (_byKey.Count == 0 || sampleCount <= 0) return;

            var keys = new List<long>(_byKey.Keys);
            int take = Math.Min(sampleCount, keys.Count);

            for (int i = 0; i < take; i++)
            {
                int j = i + _rng.Next(keys.Count - i);
                (keys[i], keys[j]) = (keys[j], keys[i]);
            }

            var toRemove = new List<long>();

            for (int i = 0; i < take; i++)
            {
                if (!_byKey.TryGetValue(keys[i], out var sample)) continue;
                
                sample.AttemptsSinceAdded++;

                double effectiveLr = baseLearningRate * sample.Weight;
                double totalError = 0;
                for (int o = 0; o < sample.States.Length; o++)
                {
                    totalError += network.TrainOnExample(
                        sample.States[o],
                        new[] { sample.Target },
                        effectiveLr);
                }
                double avgError = totalError / sample.States.Length;

                if (avgError < GoodErrorThreshold)
                {
                    sample.ConsecutiveGoodChecks++;
                    if (sample.ConsecutiveGoodChecks >= RequiredStreak)
                    {
                        toRemove.Add(sample.Key);
                        LearnedSamplesCount++;
                        TotalAttemptsToLearn += sample.AttemptsSinceAdded;
                    }  
                }
                else
                {
                    sample.ConsecutiveGoodChecks = 0;
                }
            }

            foreach (var key in toRemove)
            {
                _byKey.Remove(key);
                _order.Remove(key);
            }
        }
    }
}