using System;
using System.Collections.Generic;
using GomokuGame.AI.Training.Targets;
using GomokuGame.Models;

namespace GomokuGame.AI.Training
{
    public sealed partial class GradeMain : IBoardNetworkTrainer
    {
        public int InputSize => StateEncoder.InputSize;

        public double[] Encode(
            GameBoard board,
            int row,
            int col,
            CellState player,
            int orientation = 0)
        {
            return StateEncoder.Encode(
                board,
                row,
                col,
                player,
                orientation);
        }

        public double LearnFromGame(
            AiPlayer player,
            List<MoveRecord> history,
            CellState winner,
            int winLength)
        {
            if (history.Count == 0)
                return 0;

            int count = history.Count;
            double totalLoss = 0;

            // Индекс последнего хода победителя (если есть победитель)
            int lastWinnerIdx = -1;

            if (winner != CellState.Empty)
            {
                for (int i = history.Count - 1; i >= 0; i--)
                {
                    if (history[i].Player == winner)
                    {
                        lastWinnerIdx = i;
                        break;
                    }
                }
            }

            int criticalCountThisGame = 0;
            bool reverseOrder = (player.EpisodesTrained % 2) == 1;

            for (int i = 0; i < history.Count; i++)
            {
                var move = history[i];

                var context = new TrainingContext(
                    winner,
                    lastWinnerIdx,
                    winLength,
                    player.Discount,
                    player.AttackWeight,
                    player.DefenseWeight);

                double target = ComputeTarget(
                    player.Mode, move, i, context);

                double effectiveLr =
                    player.LearningRate * move.CriticalityWeight;

                if (move.AllOrientationStates != null)
                {
                    // Гарантированный x4: та же target на всех 4 поворотах одной позиции.
                    // Чередуем порядок обхода по чётности партии, чтобы одна и та же
                    // ориентация не оказывалась систематически "последней" на общих весах.
                    double sumLoss = 0;
                    int n = move.AllOrientationStates.Length;

                    for (int k = 0; k < n; k++)
                    {
                        int orientation = reverseOrder
                            ? n - 1 - k
                            : k;

                        sumLoss += player.Network.TrainOnExample(
                            move.AllOrientationStates[orientation],
                            new[] { target },
                            effectiveLr);
                    }

                    totalLoss += sumLoss / n;
                }
                else
                {
                    // Страховка на случай сэмплов без ориентаций (например, из других путей обучения).
                    totalLoss += player.Network.TrainOnExample(
                        move.State,
                        new[] { target },
                        effectiveLr);
                }

                // В постоянный буфер — только по явному флагу критичности,
                // а не по тому, посчитаны ли 4 ориентации (теперь они есть у всех).
                if (move.IsCritical
                    && (player.Correction == AiPlayer.CorrectionMode.Full
                        || player.Correction == AiPlayer.CorrectionMode.All))
                {
                    criticalCountThisGame++;

                    long key = CriticalSampleBuffer.ComputeKey(
                        move.AllOrientationStates![0]);

                    player.CriticalBuffer.AddOrUpdate(
                        move.AllOrientationStates,
                        target,
                        move.CriticalityWeight,
                        key);
                }
            }

            // Full/Once/Off — дозированная подмесь, как раньше.
            // All — принудительный проход по ВСЕМУ текущему буферу каждую партию.
            int mixCount =
                player.Correction == AiPlayer.CorrectionMode.All
                    ? player.CriticalBuffer.BufferSize
                    : criticalCountThisGame * 2;

            player.CriticalBuffer.TrainMix(
                player.Network,
                player.LearningRate,
                mixCount,
                reverseOrder);

            // Сохраняем прежний порядок завершающих действий.
            history.Clear();
            player.LastAverageLoss = totalLoss / count;
            player.EpisodesTrained++;
            player.DecayEpsilon();

            return player.LastAverageLoss;
        }

        private double ComputeTarget(
            AiPlayer.LearningMode mode,
            MoveRecord move,
            int index,
            TrainingContext context)
        {
            // Форсированная коррекция: это не реально сыгранный ход, а показанный сети
            // задним числом правильный ответ на прямую угрозу (своя победа или блок чужой).
            // Его цель — объективная критичность момента, а не то, как закончилась вся партия:
            // иначе дисконт по исходу и shaping по чужому потенциалу могут обнулить или даже
            // развернуть сигнал ровно там, где он важнее всего.
            if (move.IsForcedCorrection)
                return Math.Clamp(move.Criticality, 0.0, 1.0);

            var calculator = TrainingTargetFactory.Get(mode);

            return calculator?.Compute(move, index, context) ?? 0.5;
        }
    }
}