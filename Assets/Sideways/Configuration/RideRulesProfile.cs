using UnityEngine;

namespace Sideways.Configuration
{
    [CreateAssetMenu(menuName = "Sideways/Ride Rules Profile", fileName = "RideRulesProfile")]
    public sealed class RideRulesProfile : ScriptableObject
    {
        [Range(90f, 180f)] public float flipAngle = 100f;
        [Min(0.1f)] public float flipDuration = 2f;
        [Min(0.1f)] public float safePoseInterval = 0.5f;
        [Range(0f, 90f)] public float safePoseMaximumTilt = 25f;
        [Min(0f)] public float safePoseClearance = 0.3f;
        [Min(0f)] public float resetVerticalOffset = 0.65f;
    }
}
