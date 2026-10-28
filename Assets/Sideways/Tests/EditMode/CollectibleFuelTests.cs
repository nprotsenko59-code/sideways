using NUnit.Framework;
using Sideways.Collectibles;
using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Tests
{
    public sealed class CollectibleFuelTests
    {
        [Test]
        public void FuelConsumesOnlyWhileMovingAndClampsAtEmpty()
        {
            var profile = CreateProfile();
            profile.startingFuel = 10f;
            profile.baseConsumptionPerSecond = 1f;
            profile.speedConsumptionPerMeter = 0.1f;
            var model = new FuelModel(profile);

            Assert.That(model.Tick(0f, 5f, true), Is.Zero);
            Assert.That(model.Tick(10f, 2f, true), Is.EqualTo(4f).Within(0.001f));
            Assert.That(model.CurrentFuel, Is.EqualTo(6f).Within(0.001f));
            model.Tick(100f, 10f, true);
            Assert.That(model.CurrentFuel, Is.Zero);
            Assert.That(model.IsEmpty, Is.True);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void RefuelClampsAtTankCapacity()
        {
            var profile = CreateProfile();
            profile.startingFuel = 70f;
            var model = new FuelModel(profile);

            Assert.That(model.Refuel(35f), Is.EqualTo(30f).Within(0.001f));
            Assert.That(model.CurrentFuel, Is.EqualTo(100f));
            Assert.That(model.Refuel(10f), Is.Zero);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ResourceControllerCountsCoinsAndRejectsFuelAtFullTank()
        {
            var profile = CreateProfile();
            var gameObject = new GameObject("Resources");
            var controller = gameObject.AddComponent<RideResourceController>();
            controller.Configure(null, null, profile);

            Assert.That(controller.TryCollect(CollectibleKind.Coin), Is.True);
            Assert.That(controller.Snapshot.Coins, Is.EqualTo(1));
            Assert.That(controller.TryCollect(CollectibleKind.FuelCanister), Is.True);
            Assert.That(controller.Snapshot.Fuel, Is.EqualTo(100f));
            Assert.That(controller.TryCollect(CollectibleKind.FuelCanister), Is.False);
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void RoadResetRestoresEmergencyFuelOnlyWhenTankIsEmpty()
        {
            var profile = CreateProfile();
            profile.startingFuel = 0f;
            profile.emergencyResetFuel = 15f;
            var gameObject = new GameObject("Resources");
            var controller = gameObject.AddComponent<RideResourceController>();
            controller.Configure(null, null, profile);

            Assert.That(controller.Snapshot.IsEmpty, Is.True);
            Assert.That(controller.RestoreEmergencyFuel(), Is.True);
            Assert.That(controller.Snapshot.Fuel, Is.EqualTo(15f));
            Assert.That(controller.RestoreEmergencyFuel(), Is.False, "Reset fuel is a rescue, not a repeatable refill exploit.");

            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void FuelProfileDefaultsAreValid()
        {
            var profile = CreateProfile();
            Assert.That(profile.IsValid(out var error), Is.True, error);
            Object.DestroyImmediate(profile);
        }

        private static FuelProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<FuelProfile>();
            profile.maximumFuel = 100f;
            profile.startingFuel = 70f;
            profile.baseConsumptionPerSecond = 0.22f;
            profile.speedConsumptionPerMeter = 0.008f;
            profile.minimumConsumptionSpeed = 0.5f;
            profile.emergencyResetFuel = 15f;
            profile.coinPoints = 100;
            profile.canisterFuel = 35f;
            profile.coinRespawnDelay = 20f;
            profile.canisterRespawnDelay = 35f;
            return profile;
        }
    }
}
