using Sideways.Configuration;

namespace Sideways.Challenges
{
    public enum ChallengeState
    {
        Idle,
        Active,
        Succeeded,
        Failed,
        Cancelled
    }

    public readonly struct ChallengeSnapshot
    {
        public ChallengeSnapshot(ChallengeState state, string challengeId, string displayName, ChallengeType type, int progress, int target, float timeRemaining, int reward)
        {
            State = state;
            ChallengeId = challengeId;
            DisplayName = displayName;
            Type = type;
            Progress = progress;
            Target = target;
            TimeRemaining = timeRemaining;
            Reward = reward;
        }

        public ChallengeState State { get; }
        public string ChallengeId { get; }
        public string DisplayName { get; }
        public ChallengeType Type { get; }
        public int Progress { get; }
        public int Target { get; }
        public float TimeRemaining { get; }
        public int Reward { get; }
    }

    public readonly struct ChallengeReward
    {
        public ChallengeReward(string challengeId, int amount)
        {
            ChallengeId = challengeId;
            Amount = amount;
        }

        public string ChallengeId { get; }
        public int Amount { get; }
    }
}
