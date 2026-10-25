namespace GomokuGame.AI.Training
{
    internal interface ITrainingTarget
    {
        double Compute(
            MoveRecord move,
            int index,
            TrainingContext context);
    }
}