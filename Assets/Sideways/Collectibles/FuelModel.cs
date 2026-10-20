using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Collectibles
{
    public sealed class FuelModel
    {
        private readonly FuelProfile profile;

        public FuelModel(FuelProfile fuelProfile)
        {
            profile = fuelProfile;
            CurrentFuel = profile != null ? Mathf.Clamp(profile.startingFuel, 0f, profile.maximumFuel) : 0f;
        }

        public float CurrentFuel { get; private set; }
        public float MaximumFuel => profile != null ? profile.maximumFuel : 0f;
        public bool IsEmpty => CurrentFuel <= 0f;

        public float Tick(float speed, float deltaTime, bool consuming)
        {
            if (profile == null || !consuming || speed < profile.minimumConsumptionSpeed || deltaTime <= 0f || IsEmpty) return 0f;
            var used = (profile.baseConsumptionPerSecond + Mathf.Max(0f, speed) * profile.speedConsumptionPerMeter) * deltaTime;
            var before = CurrentFuel;
            CurrentFuel = Mathf.Max(0f, CurrentFuel - used);
            return before - CurrentFuel;
        }

        public float Refuel(float amount)
        {
            if (profile == null || amount <= 0f || CurrentFuel >= profile.maximumFuel) return 0f;
            var before = CurrentFuel;
            CurrentFuel = Mathf.Min(profile.maximumFuel, CurrentFuel + amount);
            return CurrentFuel - before;
        }
    }
}
