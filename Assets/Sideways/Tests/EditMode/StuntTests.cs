using NUnit.Framework;
using Sideways.Configuration;
using Sideways.Stunts;
using UnityEngine;

namespace Sideways.Tests
{
    public sealed class StuntTests
    {
        private StuntProfile profile;

        [SetUp]
        public void SetUp() => profile = ScriptableObject.CreateInstance<StuntProfile>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(profile);

        [Test]
        public void CleanRampJumpAwardsCappedAirtimeAndLandingPoints()
        {
            var model = new AirborneStuntModel(profile);
            Assert.That(model.Arm(12f), Is.True);
            var began = model.Tick(false, 12f, 0f, 5f, 0.1f);
            Assert.That(began.Began, Is.True);
            for (var i = 0; i < 30; i++) model.Tick(false, 11f, 10f, -4f, 0.1f);
            var landed = model.Tick(true, 9f, 20f, -4f, 0.02f);

            Assert.That(landed.CleanLanding, Is.True);
            Assert.That(landed.Airtime, Is.GreaterThan(profile.maximumCreditedAirtime));
            Assert.That(landed.Bonus, Is.EqualTo(350));
            Assert.That(landed.ComboHold, Is.False);
        }

        [Test]
        public void SlowLaunchAndUnsafeLandingDoNotAwardPoints()
        {
            var model = new AirborneStuntModel(profile);
            Assert.That(model.Arm(profile.minimumLaunchSpeed - 0.1f), Is.False);
            Assert.That(model.Arm(profile.minimumLaunchSpeed), Is.True);
            model.Tick(false, 10f, 0f, 4f, 0.1f);
            for (var i = 0; i < 5; i++) model.Tick(false, 10f, 0f, -14f, 0.1f);
            var landed = model.Tick(true, 10f, 10f, -14f, 0.02f);

            Assert.That(landed.CleanLanding, Is.False);
            Assert.That(landed.Bonus, Is.Zero);
            Assert.That(model.Arm(20f), Is.False, "Rearming is blocked during the landing cooldown.");
        }

        [Test]
        public void RampArmExpiresIfVehicleNeverLeavesGround()
        {
            var model = new AirborneStuntModel(profile);
            Assert.That(model.Arm(10f), Is.True);
            for (var i = 0; i < 9; i++) model.Tick(true, 10f, 0f, 0f, 0.1f);
            var update = model.Tick(false, 10f, 0f, 3f, 0.1f);
            Assert.That(update.Began, Is.False);
        }
    }
}
