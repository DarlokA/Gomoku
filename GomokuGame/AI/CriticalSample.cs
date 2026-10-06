using System;

namespace GomokuGame.AI
{
    /// <summary>
    /// Один критичный семпл обучения: состояние уже размножено во всех 4 симметриях
    /// (StateEncoder: гориз/верт/диаг\/диаг/), одна и та же целевая оценка для всех 4.
    /// Семпл живёт в буфере, пока сеть не покажет стабильно низкую ошибку на нём
    /// несколько проверок подряд.
    /// </summary>
    public class CriticalSample
    {
        public double[][] States { get; }   // [0..3] — 4 симметрии одного хода
        public double Target { get; set; }
        public double Weight { get; set; }
        public int ConsecutiveGoodChecks { get; set; }
        public long Key { get; }
        /// <summary>Сколько раз семпл выбирался на проверку с момента добавления в буфер.</summary>
        public int AttemptsSinceAdded { get; set; }

        public CriticalSample(double[][] states, double target, double weight, long key)
        {
            States = states;
            Target = target;
            Weight = weight;
            Key = key;
        }
    }
}