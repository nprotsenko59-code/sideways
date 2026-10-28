using UnityEngine;

namespace Sideways.Configuration
{
    [CreateAssetMenu(menuName = "Sideways/Stunt Profile", fileName = "StuntProfile")]
    public sealed class StuntProfile : ScriptableObject
    {
        [Min(0f)] public float minimumLaunchSpeed = 8f;
        [Min(0f)] public float minimumLandingSpeed = 5f;
        [Min(0.05f)] public float minimumAirtime = 0.35f;
        [Min(0.1f)] public float maximumCreditedAirtime = 2.5f;
        [Range(0f, 90f)] public float maximumLandingTilt = 35f;
        [Min(0f)] public float maximumDownwardLandingSpeed = 12f;
        [Min(0)] public int cleanLandingPoints = 100;
        [Min(0f)] public float pointsPerAirborneSecond = 100f;
        [Min(0.1f)] public float launchWindow = 0.75f;
        [Min(0f)] public float rearmDelay = 0.5f;

        public bool IsValid(out string error)
        {
            if (minimumLaunchSpeed <= 0f || minimumLandingSpeed < 0f)
            {
                error = "Stunt speed thresholds are invalid.";
                return false;
            }
            if (minimumAirtime <= 0f || maximumCreditedAirtime < minimumAirtime || launchWindow <= 0f)
            {
                error = "Stunt timing thresholds are invalid.";
                return false;
            }
            if (maximumLandingTilt <= 0f || maximumDownwardLandingSpeed <= 0f)
            {
                error = "Clean landing limits must be greater than zero.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
