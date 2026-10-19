using UnityEngine;

namespace Sideways.Configuration
{
    [CreateAssetMenu(menuName = "Sideways/Scoring Profile", fileName = "ScoringProfile")]
    public sealed class ScoringProfile : ScriptableObject
    {
        [Header("Drift eligibility")]
        [Min(0f)] public float minimumSpeed = 6f;
        [Range(0f, 90f)] public float minimumSlipAngle = 8f;
        [Range(0f, 90f)] public float idealSlipAngle = 35f;
        [Range(0f, 180f)] public float maximumSlipAngle = 70f;
        [Range(0f, 180f)] public float maximumVehicleTilt = 45f;
        [Min(0f)] public float maximumYawRate = 180f;

        [Header("Points")]
        [Min(0f)] public float basePointsPerSecond = 100f;
        [Range(0f, 1f)] public float minimumAngleFactor = 0.4f;
        [Min(0f)] public float fullSpeedBonusAt = 25f;
        [Min(1f)] public float maximumSpeedMultiplier = 1.75f;
        [Range(0.01f, 0.25f)] public float maximumScoringDeltaTime = 0.1f;

        [Header("Combo")]
        [Min(0f)] public float comboGraceDuration = 1.5f;
        [Min(0.1f)] public float multiplierStepSeconds = 2f;
        [Min(0f)] public float multiplierStep = 0.25f;
        [Min(1f)] public float maximumMultiplier = 3f;

        [Header("Collision")]
        [Min(0f)] public float severeCollisionSpeed = 7f;
        [Min(0f)] public float collisionScoringLockout = 0.4f;

        public bool IsValid(out string error)
        {
            if (minimumSlipAngle >= idealSlipAngle || idealSlipAngle >= maximumSlipAngle)
            {
                error = "Slip angles must be ordered minimum < ideal < maximum.";
                return false;
            }

            if (fullSpeedBonusAt <= minimumSpeed)
            {
                error = "Full speed bonus threshold must exceed minimum scoring speed.";
                return false;
            }

            if (maximumMultiplier < 1f)
            {
                error = "Maximum multiplier must be at least one.";
                return false;
            }

            if (multiplierStepSeconds <= 0f || maximumScoringDeltaTime <= 0f)
            {
                error = "Scoring time steps must be greater than zero.";
                return false;
            }

            if (maximumVehicleTilt <= 0f || maximumYawRate <= 0f)
            {
                error = "Anti-exploit orientation limits must be greater than zero.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
