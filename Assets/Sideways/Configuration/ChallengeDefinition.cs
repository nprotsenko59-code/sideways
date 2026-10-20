using UnityEngine;

namespace Sideways.Configuration
{
    public enum ChallengeType
    {
        DestroyRoadSigns,
        EarnDriftScore
    }

    [CreateAssetMenu(menuName = "Sideways/Challenge Definition", fileName = "ChallengeDefinition")]
    public sealed class ChallengeDefinition : ScriptableObject
    {
        public string challengeId = "challenge";
        public string displayName = "Challenge";
        public ChallengeType type;
        [Min(1f)] public float duration = 60f;
        [Min(1)] public int target = 10;
        [Min(0)] public int reward = 1000;

        public void Configure(string id, string title, ChallengeType challengeType, float seconds, int targetValue, int rewardValue)
        {
            challengeId = id;
            displayName = title;
            type = challengeType;
            duration = seconds;
            target = targetValue;
            reward = rewardValue;
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Challenge identity and display name are required.";
                return false;
            }
            if (duration <= 0f || target <= 0 || reward < 0)
            {
                error = "Challenge duration and target must be positive and reward cannot be negative.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
