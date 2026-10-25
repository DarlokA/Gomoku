using System;

namespace GomokuGame.AI.Training.Targets
{
    /// <summary>
    /// E: локальная оценка постановки своего камня в центр окна.
    ///
    /// Приоритеты:
    /// 1. Собственная немедленная победа.
    /// 2. Устранение немедленной угрозы противника.
    /// 3. Купирование угрозы, которой осталось два камня до победы.
    /// 4. Непобедное развитие своей линии.
    ///
    /// Исход партии и положение примера в истории
    /// на целевое значение не влияют.
    /// </summary>
    internal sealed class LocalTacticalTarget : ITrainingTarget
    {
        public double Compute(
            MoveRecord move,
            int index,
            TrainingContext context)
        {
            throw new NotImplementedException(
                "Расчёт целевого сигнала алгоритма E ещё не реализован.");
        }
    }
}