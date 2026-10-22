using UnityEngine;

namespace Sideways.Configuration
{
    [CreateAssetMenu(menuName = "Sideways/Traffic Profile", fileName = "TrafficProfile")]
    public sealed class TrafficProfile : ScriptableObject
    {
        [Header("Population")]
        [Range(0, 12)] public int maximumVehicles = 4;
        [Min(0f)] public float minimumSpawnDistance = 65f;
        [Min(1f)] public float maximumSpawnDistance = 170f;
        [Min(0f)] public float forwardSpawnExclusionDistance = 110f;
        [Range(1f, 179f)] public float forwardSpawnExclusionAngle = 50f;
        [Min(1f)] public float despawnDistance = 230f;
        [Range(1, 32)] public int spawnAttempts = 16;
        [Min(1f)] public float minimumVehicleSpacing = 12f;

        [Header("Driving")]
        [Min(1f)] public float minimumSpeed = 8f;
        [Min(1f)] public float maximumSpeed = 14f;
        [Min(1f)] public float turnRate = 100f;
        [Min(0f)] public float laneOffset = 2.7f;
        [Min(0.5f)] public float waypointTolerance = 2.5f;
        [Min(1f)] public float obstacleLookAhead = 9f;
        [Min(1f)] public float intersectionYieldDistance = 8f;

        [Header("Damage")]
        [Min(1f)] public float destructionImpactSpeed = 12f;
        [Min(0.5f)] public float wreckRespawnDelay = 8f;

        [Header("Near miss")]
        [Min(1f)] public float nearMissRadius = 3.4f;
        [Min(0f)] public float nearMissRelativeSpeed = 9f;
        [Min(1)] public int nearMissPoints = 250;

        public bool IsValid(out string error)
        {
            if (maximumVehicles < 0 || maximumSpawnDistance <= minimumSpawnDistance || despawnDistance <= maximumSpawnDistance || minimumVehicleSpacing <= 0f)
            {
                error = "Traffic population distances must be ordered spawn minimum < spawn maximum < despawn, with positive vehicle spacing.";
                return false;
            }
            if (minimumSpeed <= 0f || maximumSpeed < minimumSpeed || turnRate <= 0f)
            {
                error = "Traffic speed and steering values are invalid.";
                return false;
            }
            if (intersectionYieldDistance <= 0f || destructionImpactSpeed <= 0f || wreckRespawnDelay <= 0f)
            {
                error = "Traffic yielding and damage values must be positive.";
                return false;
            }
            if (nearMissRadius <= 0f || nearMissRelativeSpeed <= 0f || nearMissPoints <= 0)
            {
                error = "Near-miss radius, relative speed, and points must be positive.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
