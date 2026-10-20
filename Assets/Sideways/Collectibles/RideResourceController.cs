using System;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Collectibles
{
    public sealed class RideResourceController : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicle;
        [SerializeField] private DriftScoreController scoring;
        [SerializeField] private FuelProfile profile;

        private FuelModel fuel;
        private int coins;

        public event Action<RideResourceSnapshot> ResourcesUpdated;
        public event Action<CollectiblePickup> PickupCollected;

        public RideResourceSnapshot Snapshot => new(fuel?.CurrentFuel ?? 0f, fuel?.MaximumFuel ?? 0f, coins);

        private void Awake() => EnsureModel();

        private void Update()
        {
            EnsureModel();
            if (fuel == null || vehicle == null) return;
            var wasEmpty = fuel.IsEmpty;
            var used = fuel.Tick(vehicle.Telemetry.Speed, Time.deltaTime, vehicle.DrivingEnabled);
            vehicle.SetPropulsionEnabled(!fuel.IsEmpty);
            if (used > 0f || wasEmpty != fuel.IsEmpty) ResourcesUpdated?.Invoke(Snapshot);
        }

        public void Configure(VehicleController vehicleController, DriftScoreController scoreController, FuelProfile fuelProfile)
        {
            vehicle = vehicleController;
            scoring = scoreController;
            profile = fuelProfile;
            fuel = profile != null ? new FuelModel(profile) : null;
            coins = 0;
            vehicle?.SetPropulsionEnabled(fuel == null || !fuel.IsEmpty);
            ResourcesUpdated?.Invoke(Snapshot);
        }

        public bool TryCollect(CollectibleKind kind, float amount = 0f)
        {
            EnsureModel();
            if (fuel == null || profile == null) return false;

            float collectedAmount;
            switch (kind)
            {
                case CollectibleKind.Coin:
                    coins++;
                    collectedAmount = amount > 0f ? amount : profile.coinPoints;
                    scoring?.AwardBonus(Mathf.RoundToInt(collectedAmount), "Coin");
                    break;
                case CollectibleKind.FuelCanister:
                    collectedAmount = fuel.Refuel(amount > 0f ? amount : profile.canisterFuel);
                    if (collectedAmount <= 0f) return false;
                    vehicle?.SetPropulsionEnabled(true);
                    break;
                default:
                    return false;
            }

            var snapshot = Snapshot;
            ResourcesUpdated?.Invoke(snapshot);
            PickupCollected?.Invoke(new CollectiblePickup(kind, collectedAmount, snapshot));
            return true;
        }

        private void EnsureModel()
        {
            if (fuel == null && profile != null) fuel = new FuelModel(profile);
        }
    }
}
