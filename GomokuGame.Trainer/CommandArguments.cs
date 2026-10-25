using static GomokuGame.AI.AiPlayer;

namespace GomokuGame.Trainer
{
    internal struct TrainingArguments
    {
        public GameSettings Game { get; set; }
        public string Algorithm { get; set; }
        public string SaveName { get; set; }
        public int Chunk { get; set; }
        public LearningMode Mode { get; set; }

        public GradientTrainingArguments Gradient { get; set; }
        public GeneticTrainingArguments Genetic { get; set; }
    }

    internal struct GradientTrainingArguments
    {
        public int Games { get; set; }
        public double LearningRate { get; set; }
        public double Epsilon { get; set; }

        public bool DecayExplicit { get; set; }
        public double Decay { get; set; }
        public double DecayTargetFraction { get; set; }

        public bool ShowDemo { get; set; }
        public CorrectionMode Correction { get; set; }
        public LeagueTrainingSettings League { get; set; }
    }

    internal struct GeneticTrainingArguments
    {
        public int Population { get; set; }
        public int EliteCount { get; set; }
        public double MutationSigma { get; set; }
        public int GamesPerPair { get; set; }
        public int MaxCycles { get; set; }
    }

    internal struct MatchArguments
    {
        public GameSettings Game { get; set; }
        public int Games { get; set; }
        public string FileX { get; set; }
        public string FileO { get; set; }
    }

    internal struct MatchLogArguments
    {
        public GameSettings Game { get; set; }
        public string FileX { get; set; }
        public string FileO { get; set; }
        public string? LogFile { get; set; }
        public double Epsilon { get; set; }
    }
}