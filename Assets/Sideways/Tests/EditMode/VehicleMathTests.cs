using NUnit.Framework;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Tests
{
    public sealed class VehicleMathTests
    {
        [Test]
        public void SteeringMovesTowardTargetWithoutOvershoot()
        {
            Assert.That(VehicleMath.MoveSteering(0f, 1f, 4f, 0.1f), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(VehicleMath.MoveSteering(0.9f, 1f, 4f, 0.1f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SlipAngleUsesGroundPlaneVelocity()
        {
            var velocity = new Vector3(10f, 25f, 10f);
            var slip = VehicleMath.SlipAngleDegrees(Vector3.forward, velocity, Vector3.up);
            Assert.That(slip, Is.EqualTo(45f).Within(0.01f));
        }

        [Test]
        public void HandlingProfileRejectsInvertedDriftThresholds()
        {
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            profile.driftEnterAngle = 5f;
            profile.driftExitAngle = 10f;
            Assert.That(profile.IsValid(out var error), Is.False);
            Assert.That(error, Does.Contain("exit angle"));
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void HandlingProfileRejectsHandbrakeWithMoreGripThanDrift()
        {
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            profile.driftLateralGrip = 2f;
            profile.handbrakeLateralGrip = 3f;
            Assert.That(profile.IsValid(out var error), Is.False);
            Assert.That(error, Does.Contain("Handbrake grip"));
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void FlipTimerResetsAfterTransientRoll()
        {
            var timer = new FlipTimer(100f, 2f);
            Assert.That(timer.Tick(120f, 1.5f), Is.False);
            Assert.That(timer.Tick(20f, 0.1f), Is.False);
            Assert.That(timer.Elapsed, Is.Zero);
            Assert.That(timer.Tick(120f, 2f), Is.True);
        }

        [TestCase(true, 10f, true, true, true)]
        [TestCase(false, 10f, true, true, false)]
        [TestCase(true, 40f, true, true, false)]
        [TestCase(true, 10f, false, true, false)]
        [TestCase(true, 10f, true, false, false)]
        public void SafePoseRequiresGroundRoadUprightAndClearance(bool grounded, float angle, bool roadBelow, bool obstacleFree, bool expected)
        {
            Assert.That(SafePoseRules.IsEligible(grounded, angle, 25f, roadBelow, obstacleFree), Is.EqualTo(expected));
        }
    }
}
