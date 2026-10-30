using System;

namespace GomokuGame.Trainer.MCP.Models
{
    /// <summary>
    /// Состояние процесса обучения Gomoku сети.
    /// </summary>
    public sealed class TrainingState
    {
        /// <summary>Имя обучаемой сети (A/B/C/D/E)</summary>
        public string? NetworkName { get; set; }

        /// <summary>Всего сыгранных партий</summary>
        public long TotalGamesPlayed { get; set; }

        /// <summary>Текущий batch (если обучение запущено)</summary>
        public int? CurrentBatch { get; set; }

        /// <summary>Общее количество батчей</summary>
        public int? TotalBatches { get; set; }

        /// <summary>Потери последнего батча (loss)</summary>
        public double? LastLoss { get; set; }

        /// <summary>Winrate последней партии (в процентах, 0..100)</summary>
        public double? LastWinRate { get; set; }

        /// <summary>Прогресс обучения в процентах</summary>
        public double ProgressPercent =>
            TotalBatches.HasValue && CurrentBatch.HasValue
                ? (double)CurrentBatch.Value / TotalBatches.Value * 100.0
                : 0.0;

        /// <summary>Статус процесса</summary>
        public TrainingStatus Status { get; set; } = TrainingStatus.Idle;

        /// <summary>Путь к логам обучения (если есть)</summary>
        public string? LogPath { get; set; }

        /// <summary>Путь к обученной сети (если завершено)</summary>
        public string? SavedNetworkPath { get; set; }

        /// <summary>Начальное количество партий для warmup</summary>
        public int WarmupGames { get; set; } = 500;

        /// <summary>Seed случайных чисел</summary>
        public int Seed { get; set; } = 42;

        /// <summary>Learning rate</summary>
        public double LearningRate { get; set; } = 0.01;

        /// <summary>Еpsilon (exploration probability)</summary>
        public double Epsilon { get; set; } = 0.3;

        /// <summary>Decay coefficient</summary>
        public double Decay { get; set; } = 0.995;

        /// <summary>Имя сохранённой сети (если завершено)</summary>
        public string? SaveName { get; set; }

        /// <summary>Количество прошедшего времени обучения</summary>
        public TimeSpan TotalTimeElapsed => default(TimeSpan); // заполняется позже

        /// <summary>Последний выбранный режим обучения (LineLengthOnly/DiscountedOutcome и т.д.)</summary>
        public string? LearningMode { get; set; }

        /// <summary>Количество Missed Wins в среднем</summary>
        public double AvgMissedWins { get; set; }

        /// <summary>Лучший winrate за все сессии обучения</summary>
        public double BestWinRate { get; set; }
    }

    /// <summary>
    /// Статус процесса обучения.
    /// </summary>
    public enum TrainingStatus
    {
        Idle,       ///< Ждёт команды
        Running,    ///< Обучение запущено
        Stopped,    ///< Остановлено пользователем
        Finished,   ///< Завершилось успешно
        Error       ///< Ошибка
    }
}