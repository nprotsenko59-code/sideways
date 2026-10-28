using UnityEngine;

namespace Sideways.Vehicle
{
    public static class VehicleMath
    {
        public static Vector3 GroundPlaneVelocity(Vector3 velocity, Vector3 groundNormal) =>
            Vector3.ProjectOnPlane(velocity, groundNormal);

        public static float SlipAngleDegrees(Vector3 forward, Vector3 groundVelocity, Vector3 groundNormal)
        {
            var planarForward = Vector3.ProjectOnPlane(forward, groundNormal).normalized;
            var planarVelocity = Vector3.ProjectOnPlane(groundVelocity, groundNormal);
            if (planarForward.sqrMagnitude < 0.0001f || planarVelocity.sqrMagnitude < 0.01f)
            {
                return 0f;
            }

            return Vector3.SignedAngle(planarForward, planarVelocity.normalized, groundNormal);
        }

        public static float MoveSteering(float current, float target, float response, float deltaTime) =>
            Mathf.MoveTowards(current, Mathf.Clamp(target, -1f, 1f), Mathf.Max(0f, response) * Mathf.Max(0f, deltaTime));

        public static float HoldDriftStrength(float heldDuration, float fullStrengthDuration) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Max(0f, heldDuration) / Mathf.Max(0.01f, fullStrengthDuration)));

        public static bool IsCountersteering(float steeringInput, float yawRateDegrees, bool drifting, float minimumYawRate)
        {
            if (!drifting || Mathf.Abs(yawRateDegrees) <= Mathf.Max(0f, minimumYawRate)) return false;
            return Mathf.Abs(steeringInput) > 0.01f && Mathf.Sign(steeringInput) != Mathf.Sign(yawRateDegrees);
        }

        public static float SteeringDriftTarget(float steeringInput, float driftStrength, float speed, float driftMinimumSpeed)
        {
            var speedBlend = Mathf.InverseLerp(Mathf.Max(0f, driftMinimumSpeed) * 0.55f, Mathf.Max(0.01f, driftMinimumSpeed), Mathf.Max(0f, speed));
            return Mathf.Clamp(steeringInput, -1f, 1f) * Mathf.Clamp01(driftStrength) * speedBlend;
        }

        public static float MoveSignedDriftControl(float current, float target, float engageRate, float releaseRate, float deltaTime)
        {
            current = Mathf.Clamp(current, -1f, 1f);
            target = Mathf.Clamp(target, -1f, 1f);
            var reversing = current * target < 0f;
            var growing = Mathf.Abs(target) > Mathf.Abs(current);
            var rate = reversing || !growing ? Mathf.Max(0f, releaseRate) : Mathf.Max(0f, engageRate);
            return Mathf.MoveTowards(current, target, rate * Mathf.Max(0f, deltaTime));
        }

        public static Vector3 ClampForwardToVelocity(Vector3 forward, Vector3 velocity, Vector3 groundNormal, float maximumAngle)
        {
            var planarForward = Vector3.ProjectOnPlane(forward, groundNormal).normalized;
            var planarVelocity = Vector3.ProjectOnPlane(velocity, groundNormal);
            if (planarForward.sqrMagnitude < 0.0001f || planarVelocity.sqrMagnitude < 0.01f) return planarForward;
            var velocityDirection = planarVelocity.normalized;
            var slipAngle = Vector3.SignedAngle(planarForward, velocityDirection, groundNormal);
            var limit = Mathf.Clamp(Mathf.Abs(maximumAngle), 0f, 89f);
            if (Mathf.Abs(slipAngle) <= limit) return planarForward;
            return (Quaternion.AngleAxis(-Mathf.Sign(slipAngle) * limit, groundNormal) * velocityDirection).normalized;
        }

        public static float SteeringAngleDegrees(float input, float speed, float lowSpeedAngle, float highSpeedAngle, float fullReductionSpeed)
        {
            var speedRatio = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.01f, fullReductionSpeed));
            var maximumAngle = Mathf.Lerp(Mathf.Max(0f, lowSpeedAngle), Mathf.Max(0f, highSpeedAngle), speedRatio * speedRatio);
            return Mathf.Clamp(input, -1f, 1f) * maximumAngle;
        }

        public static float CountersteerAssistAngle(
            float steeringInput,
            float slipAngle,
            float yawRateDegrees,
            float minimumYawRate,
            float assistStrength,
            float recoveryAmount,
            float maximumAssistAngle,
            float driftControlBlend,
            bool drifting)
        {
            if (!drifting || Mathf.Abs(slipAngle) < 0.01f) return 0f;
            // Assist only after the driver points into the slide. Applying it
            // during an ordinary corner opposes the requested turn and creates
            // a false steering stop at speed.
            var deliberateCountersteer = IsCountersteering(steeringInput, yawRateDegrees, drifting, minimumYawRate) ? 1f : 0f;
            var requested = Mathf.Sign(steeringInput) * Mathf.Abs(slipAngle) * Mathf.Max(0f, assistStrength) * Mathf.Clamp01(recoveryAmount) *
                            (1f - Mathf.Clamp01(driftControlBlend)) * deliberateCountersteer;
            return Mathf.Clamp(requested, -Mathf.Max(0f, maximumAssistAngle), Mathf.Max(0f, maximumAssistAngle));
        }

        public static float AxleSlipAngleDegrees(Vector3 wheelForward, Vector3 velocity, Vector3 groundNormal)
        {
            var planarForward = Vector3.ProjectOnPlane(wheelForward, groundNormal).normalized;
            var planarVelocity = Vector3.ProjectOnPlane(velocity, groundNormal);
            if (planarForward.sqrMagnitude < 0.0001f || planarVelocity.sqrMagnitude < 0.01f) return 0f;
            return Vector3.SignedAngle(planarForward, planarVelocity.normalized, groundNormal);
        }

        public static float TireGripAcceleration(float slipAngleDegrees, float peakGrip, float peakSlipAngle, float slidingSlipAngle, float slidingGripRetention)
        {
            var slip = Mathf.Abs(slipAngleDegrees);
            var safePeakAngle = Mathf.Max(0.01f, peakSlipAngle);
            var safeSlidingAngle = Mathf.Max(safePeakAngle + 0.01f, slidingSlipAngle);
            var safePeakGrip = Mathf.Max(0f, peakGrip);
            if (slip <= safePeakAngle)
            {
                return safePeakGrip * Mathf.Sin(Mathf.Clamp01(slip / safePeakAngle) * Mathf.PI * 0.5f);
            }

            var falloff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(safePeakAngle, safeSlidingAngle, slip));
            return safePeakGrip * Mathf.Lerp(1f, Mathf.Clamp(slidingGripRetention, 0f, 1f), falloff);
        }

        public static float RemainingGrip(float totalGrip, float usedGrip)
        {
            var total = Mathf.Max(0f, totalGrip);
            var used = Mathf.Clamp(Mathf.Abs(usedGrip), 0f, total);
            return Mathf.Sqrt(Mathf.Max(0f, total * total - used * used));
        }

        public static float FrontLoadFraction(float rearAxleOffset, float wheelbase, float longitudinalAcceleration, float centerOfMassHeight, float maximumTransfer)
        {
            var safeWheelbase = Mathf.Max(0.01f, wheelbase);
            var staticFront = Mathf.Clamp01(rearAxleOffset / safeWheelbase);
            var gravity = Mathf.Max(0.01f, Physics.gravity.magnitude);
            var transfer = longitudinalAcceleration * Mathf.Max(0f, centerOfMassHeight) / (safeWheelbase * gravity);
            return Mathf.Clamp(staticFront - Mathf.Clamp(transfer, -Mathf.Abs(maximumTransfer), Mathf.Abs(maximumTransfer)), 0.1f, 0.9f);
        }

        public static float MoveBlend(float current, bool active, float engageRate, float releaseRate, float deltaTime) =>
            Mathf.MoveTowards(Mathf.Clamp01(current), active ? 1f : 0f, Mathf.Max(0f, active ? engageRate : releaseRate) * Mathf.Max(0f, deltaTime));

        public static bool UpdateDriftState(bool current, bool grounded, float speed, float slipAngle, float minimumSpeed, float enterAngle, float exitAngle)
        {
            if (!grounded || speed < minimumSpeed) return false;
            var absoluteSlip = Mathf.Abs(slipAngle);
            return current ? absoluteSlip > exitAngle : absoluteSlip >= enterAngle;
        }
    }
}
