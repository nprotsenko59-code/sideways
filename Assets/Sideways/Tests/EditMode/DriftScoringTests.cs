using NUnit.Framework;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Tests
{
    public sealed class DriftScoringTests
    {
        private ScoringProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<ScoringProfile>();
            profile.maximumScoringDeltaTime = 0.1f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void EligibleDriftEarnsPointsAndBuildsMultiplier()
        {
            var model = new DriftScoringModel(profile);
            var telemetry = EligibleTelemetry();

            for (var i = 0; i < 21; i++) model.Tick(telemetry, 0.1f);

            Assert.That(model.Snapshot.TotalScore, Is.GreaterThan(0));
            Assert.That(model.Snapshot.ActiveComboScore, Is.GreaterThan(0));
            Assert.That(model.Snapshot.Multiplier, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(model.Snapshot.Scoring, Is.True);
        }

        [Test]
        public void ComboBanksAfterGraceWindow()
        {
            var model = new DriftScoringModel(profile);
            for (var i = 0; i < 5; i++) model.Tick(EligibleTelemetry(), 0.1f);
            var activeScore = model.Snapshot.ActiveComboScore;

            for (var i = 0; i < 16; i++) model.Tick(IneligibleTelemetry(), 0.1f);

            Assert.That(activeScore, Is.GreaterThan(0));
            Assert.That(model.Snapshot.ActiveComboScore, Is.Zero);
            Assert.That(model.Snapshot.BankedScore, Is.EqualTo(activeScore).Within(1));
        }

        [Test]
        public void ComboCancellationNeverDeductsBankedScore()
        {
            var model = new DriftScoringModel(profile);
            for (var i = 0; i < 5; i++) model.Tick(EligibleTelemetry(), 0.1f);
            Assert.That(model.BankCombo(), Is.True);
            var banked = model.Snapshot.BankedScore;
            for (var i = 0; i < 5; i++) model.Tick(EligibleTelemetry(), 0.1f);

            Assert.That(model.CancelCombo(), Is.True);

            Assert.That(model.Snapshot.BankedScore, Is.EqualTo(banked));
            Assert.That(model.Snapshot.TotalScore, Is.EqualTo(banked));
        }

        [Test]
        public void OnlySevereCollisionCancelsActiveCombo()
        {
            var model = new DriftScoringModel(profile);
            for (var i = 0; i < 5; i++) model.Tick(EligibleTelemetry(), 0.1f);
            var active = model.Snapshot.ActiveComboScore;

            Assert.That(model.RegisterImpact(profile.severeCollisionSpeed - 0.1f), Is.False);
            Assert.That(model.Snapshot.ActiveComboScore, Is.EqualTo(active));
            Assert.That(model.RegisterImpact(profile.severeCollisionSpeed), Is.True);
            Assert.That(model.Snapshot.ActiveComboScore, Is.Zero);
        }

        [TestCase(3f, 30f, 0f, 30f)]
        [TestCase(15f, 3f, 0f, 30f)]
        [TestCase(15f, 85f, 0f, 30f)]
        [TestCase(15f, 30f, 70f, 30f)]
        [TestCase(15f, 30f, 0f, 250f)]
        public void ExploitThresholdsRejectInvalidDrifts(float speed, float slip, float tilt, float yawRate)
        {
            var model = new DriftScoringModel(profile);
            var telemetry = new VehicleTelemetry(speed, 1f, Vector3.forward * speed, slip, true, true, true, tilt, yawRate);
            model.Tick(telemetry, 0.1f);
            Assert.That(model.Snapshot.TotalScore, Is.Zero);
        }

        [Test]
        public void ScoringProfileValidatesOrderedThresholds()
        {
            profile.minimumSlipAngle = 40f;
            profile.idealSlipAngle = 20f;
            Assert.That(profile.IsValid(out var error), Is.False);
            Assert.That(error, Does.Contain("minimum < ideal < maximum"));
        }

        private static VehicleTelemetry EligibleTelemetry() =>
            new(15f, 1f, Vector3.forward * 15f, 30f, true, true, true, 0f, 30f);

        private static VehicleTelemetry IneligibleTelemetry() =>
            new(15f, 0f, Vector3.forward * 15f, 0f, true, false, false, 0f, 0f);
    }
}
