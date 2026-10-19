namespace Sideways.Core
{
    public enum RideSessionState
    {
        Driving,
        Ended
    }

    public enum RideEndReason
    {
        Flipped
    }

    public interface IRideScoreProvider
    {
        int CurrentScore { get; }
    }
}
