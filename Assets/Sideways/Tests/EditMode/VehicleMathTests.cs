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
        public void LongerSteeringHoldBuildsMoreDriftStrength()
        {
            var tap = VehicleMath.HoldDriftStrength(0.15f, 1.1f);
            var mediumHold = VehicleMath.HoldDriftStrength(0.55f, 1.1f);
            var longHold = VehicleMath.HoldDriftStrength(1.1f, 1.1f);
            Assert.That(tap, Is.GreaterThanOrEqualTo(0f).And.LessThan(mediumHold));
            Assert.That(mediumHold, Is.LessThan(longHold));
            Assert.That(longHold, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void OppositeSteeringMovesSignedDriftControlThroughNeutral()
        {
            var oppositeTarget = VehicleMath.SteeringDriftTarget(-1f, 1f, 16f, 8f);
            var releasingOldSide = VehicleMath.MoveSignedDriftControl(1f, oppositeTarget, 3.5f, 5f, 0.1f);
            var neutral = VehicleMath.MoveSignedDriftControl(releasingOldSide, oppositeTarget, 3.5f, 5f, 0.1f);
            var buildingNewSide = VehicleMath.MoveSignedDriftControl(neutral, oppositeTarget, 3.5f, 5f, 0.1f);

            Assert.That(oppositeTarget, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(releasingOldSide, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(neutral, Is.Zero.Within(0.001f), "Direction switching must pass through neutral grip.");
            Assert.That(buildingNewSide, Is.EqualTo(-0.35f).Within(0.001f), "The opposite drift must begin building after neutral.");
        }

        [Test]
        public void HeadingClampNeverAllowsBackwardOrientation()
        {
            var backwardVelocity = Quaternion.AngleAxis(170f, Vector3.up) * Vector3.forward * 20f;
            var clampedForward = VehicleMath.ClampForwardToVelocity(Vector3.forward, backwardVelocity, Vector3.up, 78f);
            var remainingAngle = Mathf.Abs(Vector3.SignedAngle(clampedForward, backwardVelocity.normalized, Vector3.up));

            Assert.That(remainingAngle, Is.EqualTo(78f).Within(0.01f));
            Assert.That(Vector3.Dot(clampedForward, backwardVelocity.normalized), Is.GreaterThan(0f), "The car must remain forward-facing relative to travel.");
        }

        [Test]
        public void SteeringAngleReducesProgressivelyWithSpeed()
        {
            var stopped = VehicleMath.SteeringAngleDegrees(1f, 0f, 34f, 11f, 28f);
            var medium = VehicleMath.SteeringAngleDegrees(1f, 14f, 34f, 11f, 28f);
            var fast = VehicleMath.SteeringAngleDegrees(1f, 28f, 34f, 11f, 28f);
            Assert.That(stopped, Is.EqualTo(34f).Within(0.001f));
            Assert.That(medium, Is.LessThan(stopped).And.GreaterThan(fast));
            Assert.That(fast, Is.EqualTo(11f).Within(0.001f));
        }

        [Test]
        public void CountersteerAssistNeverCancelsAnOrdinaryTurn()
        {
            var continuingRightTurn = VehicleMath.CountersteerAssistAngle(1f, 20f, 30f, 4f, 0.18f, 1f, 5f, 0f, true);
            var prematureCountersteer = VehicleMath.CountersteerAssistAngle(-1f, 20f, 30f, 4f, 0.18f, 1f, 5f, 0f, false);
            var deliberateLeftCountersteer = VehicleMath.CountersteerAssistAngle(-1f, 20f, 30f, 4f, 0.18f, 1f, 5f, 0f, true);
            Assert.That(continuingRightTurn, Is.Zero.Within(0.001f));
            Assert.That(prematureCountersteer, Is.Zero.Within(0.001f));
            Assert.That(deliberateLeftCountersteer, Is.EqualTo(-3.6f).Within(0.001f));
        }

        [Test]
        public void SlipAngleUsesGroundPlaneVelocity()
        {
            var velocity = new Vector3(10f, 25f, 10f);
            var slip = VehicleMath.SlipAngleDegrees(Vector3.forward, velocity, Vector3.up);
            Assert.That(slip, Is.EqualTo(45f).Within(0.01f));
        }

        [Test]
        public void TireGripBuildsToPeakThenRetainsSlidingGrip()
        {
            var small = VehicleMath.TireGripAcceleration(3f, 12f, 9f, 35f, 0.68f);
            var peak = VehicleMath.TireGripAcceleration(9f, 12f, 9f, 35f, 0.68f);
            var sliding = VehicleMath.TireGripAcceleration(35f, 12f, 9f, 35f, 0.68f);
            Assert.That(small, Is.GreaterThan(0f).And.LessThan(peak));
            Assert.That(peak, Is.EqualTo(12f).Within(0.001f));
            Assert.That(sliding, Is.EqualTo(12f * 0.68f).Within(0.001f));
            Assert.That(VehicleMath.TireGripAcceleration(-9f, 12f, 9f, 35f, 0.68f), Is.EqualTo(peak).Within(0.001f));
        }

        [Test]
        public void FrictionBudgetLimitsCombinedLongitudinalGrip()
        {
            Assert.That(VehicleMath.RemainingGrip(10f, 0f), Is.EqualTo(10f).Within(0.001f));
            Assert.That(VehicleMath.RemainingGrip(10f, 8f), Is.EqualTo(6f).Within(0.001f));
            Assert.That(VehicleMath.RemainingGrip(10f, 20f), Is.Zero.Within(0.001f));
        }

        [Test]
        public void LongitudinalAccelerationTransfersLoadRearward()
        {
            var staticFront = VehicleMath.FrontLoadFraction(1.35f, 2.6f, 0f, 0.55f, 0.18f);
            var acceleratingFront = VehicleMath.FrontLoadFraction(1.35f, 2.6f, 6f, 0.55f, 0.18f);
            var brakingFront = VehicleMath.FrontLoadFraction(1.35f, 2.6f, -6f, 0.55f, 0.18f);
            Assert.That(acceleratingFront, Is.LessThan(staticFront));
            Assert.That(brakingFront, Is.GreaterThan(staticFront));
        }

        [Test]
        public void BrakeBlendUsesSeparateEngageAndReleaseRates()
        {
            var engaged = VehicleMath.MoveBlend(0f, true, 5f, 2f, 0.1f);
            var released = VehicleMath.MoveBlend(engaged, false, 5f, 2f, 0.1f);
            Assert.That(engaged, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(released, Is.EqualTo(0.3f).Within(0.001f));
        }

        [Test]
        public void DriftStateUsesEntryAndExitHysteresis()
        {
            Assert.That(VehicleMath.UpdateDriftState(false, true, 12f, 11f, 8f, 12f, 6f), Is.False);
            Assert.That(VehicleMath.UpdateDriftState(false, true, 12f, 12f, 8f, 12f, 6f), Is.True);
            Assert.That(VehicleMath.UpdateDriftState(true, true, 12f, 7f, 8f, 12f, 6f), Is.True);
            Assert.That(VehicleMath.UpdateDriftState(true, true, 12f, 6f, 8f, 12f, 6f), Is.False);
            Assert.That(VehicleMath.UpdateDriftState(true, false, 12f, 30f, 8f, 12f, 6f), Is.False);
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
        public void HandlingProfileRejectsInvalidSlidingAndSteeringDriftGrip()
        {
            var profile = ScriptableObject.CreateInstance<VehicleHandlingProfile>();
            profile.slidingSlipAngle = profile.rearPeakSlipAngle;
            Assert.That(profile.IsValid(out var error), Is.False);
            Assert.That(error, Does.Contain("Sliding slip angle"));
            profile.slidingSlipAngle = 35f;
            profile.steeringDriftRearGripMultiplier = 1.1f;
            Assert.That(profile.IsValid(out error), Is.False);
            Assert.That(error, Does.Contain("Steering-drift rear grip"));
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
