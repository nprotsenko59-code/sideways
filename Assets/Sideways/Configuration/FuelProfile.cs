using UnityEngine;

namespace Sideways.Configuration
{
    [CreateAssetMenu(menuName = "Sideways/Fuel Profile", fileName = "FuelProfile")]
    public sealed class FuelProfile : ScriptableObject
    {
        [Header("Tank")]
        [Min(1f)] public float maximumFuel = 100f;
        [Min(0f)] public float startingFuel = 70f;
        [Min(0f)] public float baseConsumptionPerSecond = 0.22f;
        [Min(0f)] public float speedConsumptionPerMeter = 0.008f;
        [Min(0f)] public float minimumConsumptionSpeed = 0.5f;

        [Header("Pickups")]
        [Min(1)] public int coinPoints = 100;
        [Min(1f)] public float canisterFuel = 35f;
        [Min(1f)] public float coinRespawnDelay = 20f;
        [Min(1f)] public float canisterRespawnDelay = 35f;

        public bool IsValid(out string error)
        {
            if (maximumFuel <= 0f || startingFuel < 0f || startingFuel > maximumFuel)
            {
                error = "Starting fuel must be between zero and the tank maximum.";
                return false;
            }
            if (baseConsumptionPerSecond < 0f || speedConsumptionPerMeter < 0f || minimumConsumptionSpeed < 0f)
            {
                error = "Fuel consumption values cannot be negative.";
                return false;
            }
            if (coinPoints <= 0 || canisterFuel <= 0f || coinRespawnDelay <= 0f || canisterRespawnDelay <= 0f)
            {
                error = "Pickup values and respawn delays must be positive.";
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
