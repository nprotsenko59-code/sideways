using UnityEngine;

namespace Sideways.Configuration
{
    [CreateAssetMenu(menuName = "Sideways/Police Profile", fileName = "PoliceProfile")]
    public sealed class PoliceProfile : ScriptableObject
    {
        [Header("Detection")]
        [Min(1f)] public float visionRange = 95f;
        [Range(1f, 179f)] public float visionAngle = 120f;
        [Min(0.02f)] public float scanInterval = 0.1f;
        [Min(0f)] public float driftTriggerDuration = 0.7f;
        [Min(0f)] public float driftTriggerMinimumSpeed = 9f;
        [Min(0f)] public float severeCollisionSpeed = 7f;
        [Min(0f)] public float recklessMemory = 3.5f;

        [Header("Pursuit lifecycle")]
        [Min(0f)] public float lostSightDelay = 1.25f;
        [Range(10f, 20f)] public float searchDuration = 15f;
        [Min(0f)] public float escapedDisplayDuration = 2f;
        [Min(0f)] public float minimumEscapeSpeed = 2f;

        [Header("Navigation")]
        [Min(1f)] public float patrolSpeed = 11f;
        [Min(1f)] public float pursuitSpeed = 19f;
        [Min(1f)] public float searchSpeed = 13f;
        [Min(0.1f)] public float acceleration = 10f;
        [Min(1f)] public float turnRate = 130f;
        [Min(0.5f)] public float waypointTolerance = 3.5f;
        [Min(0.1f)] public float routeReplanInterval = 0.8f;
        [Min(0f)] public float interceptLeadTime = 1.1f;

        [Header("Busted")]
        [Min(0.5f)] public float bustedDistance = 4.5f;
        [Min(0f)] public float bustedMaximumPlayerSpeed = 6f;
        [Min(0.1f)] public float bustedHoldDuration = 1.25f;

        public bool IsValid(out string error)
        {
            if (visionRange <= 0f || visionAngle <= 0f || visionAngle >= 180f || scanInterval <= 0f)
            {
                error = "Police vision requires a positive range and scan interval, and an angle between 0 and 180 degrees.";
                return false;
            }

            if (searchDuration < 10f || searchDuration > 20f || lostSightDelay < 0f || minimumEscapeSpeed < 0f)
            {
                error = "Search duration must be 10–20 seconds and pursuit timing values cannot be negative.";
                return false;
            }

            if (patrolSpeed <= 0f || pursuitSpeed < patrolSpeed || searchSpeed <= 0f || acceleration <= 0f || turnRate <= 0f)
            {
                error = "Police navigation speeds and response values are invalid.";
                return false;
            }

            if (bustedDistance <= 0f || bustedHoldDuration <= 0f || bustedMaximumPlayerSpeed < 0f)
            {
                error = "Busted-condition values are invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
