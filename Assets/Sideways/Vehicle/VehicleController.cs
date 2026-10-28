using System;
using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleController : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private MonoBehaviour inputSourceBehaviour;
        [SerializeField] private VehicleHandlingProfile profile;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private Vector3 centerOfMass = new(0f, -0.35f, 0f);

        private IVehicleInputSource inputSource;
        private bool drivingEnabled = true;
        private bool propulsionEnabled = true;
        private bool drifting;
        private Vector3 groundNormal = Vector3.up;
        private VehicleTelemetry telemetry;
        private Vector3 previousPlanarVelocity;
        private float smoothedLongitudinalAcceleration;
        private float smoothedLateralAcceleration;
        private float brakeBlend;
        private float steeringDriftControl;
        private float steeringDriftBlend;
        private float steeringAngle;
        private float frontSlipAngle;
        private float rearSlipAngle;
        private float frontGripUtilization;
        private float rearGripUtilization;
        private bool accelerationInitialized;

        public event Action<VehicleTelemetry> TelemetryUpdated;

        public VehicleTelemetry Telemetry => telemetry;
        public bool DrivingEnabled => drivingEnabled;
        public bool PropulsionEnabled => propulsionEnabled;
        public Rigidbody Body => body;
        public VehicleHandlingProfile HandlingProfile => profile;
        public float SteeringDriftControl => steeringDriftControl;

        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
            inputSource = inputSourceBehaviour as IVehicleInputSource;
            if (body != null) body.centerOfMass = centerOfMass;
        }

        private void FixedUpdate()
        {
            if (body == null || profile == null) return;

            var grounded = TryGetGround(out groundNormal);
            var steering = drivingEnabled && inputSource != null ? inputSource.Steering : 0f;
            var driftStrength = drivingEnabled && inputSource != null ? inputSource.DriftStrength : 0f;
            var brake = drivingEnabled && inputSource != null && inputSource.Brake;

            if (drivingEnabled && grounded)
            {
                ApplyDriveForces(steering, driftStrength, brake, Time.fixedDeltaTime);
            }
            else
            {
                ResetDynamicState(brake, Time.fixedDeltaTime);
            }

            UpdateTelemetry(steering, brake, grounded);
        }

        public void Configure(Rigidbody rigidbody, IVehicleInputSource source, MonoBehaviour sourceBehaviour, VehicleHandlingProfile handling, LayerMask roadMask, Vector3 massCenter)
        {
            body = rigidbody;
            inputSource = source;
            inputSourceBehaviour = sourceBehaviour;
            profile = handling;
            groundMask = roadMask;
            centerOfMass = massCenter;
            if (body != null) body.centerOfMass = centerOfMass;
        }

        public void SetDrivingEnabled(bool enabled) => drivingEnabled = enabled;

        public void SetPropulsionEnabled(bool enabled) => propulsionEnabled = enabled;

        private void ApplyDriveForces(float steering, float driftStrength, bool brake, float deltaTime)
        {
            var forward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            var right = Vector3.Cross(groundNormal, forward).normalized;
            var planarVelocity = VehicleMath.GroundPlaneVelocity(body.linearVelocity, groundNormal);
            var forwardSpeed = Vector3.Dot(planarVelocity, forward);
            var speed = planarVelocity.magnitude;
            UpdateAcceleration(planarVelocity, forward, right, deltaTime);
            brakeBlend = VehicleMath.MoveBlend(brakeBlend, brake, profile.brakeEngageRate, profile.brakeReleaseRate, deltaTime);
            var chassisSlip = VehicleMath.SlipAngleDegrees(forward, planarVelocity, groundNormal);
            var yawSpeed = Vector3.Dot(body.angularVelocity, groundNormal);
            var yawRateDegrees = yawSpeed * Mathf.Rad2Deg;
            var driftTarget = VehicleMath.SteeringDriftTarget(steering, driftStrength, speed, profile.driftMinimumSpeed);
            steeringDriftControl = VehicleMath.MoveSignedDriftControl(
                steeringDriftControl,
                driftTarget,
                profile.steeringDriftEngageRate,
                profile.steeringDriftReleaseRate,
                deltaTime);
            steeringDriftBlend = Mathf.Abs(steeringDriftControl);

            var baseSteeringAngle = VehicleMath.SteeringAngleDegrees(
                steering,
                speed,
                profile.lowSpeedSteeringAngle,
                profile.highSpeedSteeringAngle,
                profile.fullSteeringReductionSpeed);
            var recoveryAmount = Mathf.InverseLerp(profile.driftExitAngle, profile.fullDriftIntensityAngle, Mathf.Abs(chassisSlip));
            var countersteer = VehicleMath.CountersteerAssistAngle(
                steering,
                chassisSlip,
                yawRateDegrees,
                profile.countersteerMinimumYawRate,
                profile.countersteerAssist,
                recoveryAmount,
                profile.maximumCountersteerAssistAngle,
                steeringDriftBlend,
                drifting);
            steeringAngle = baseSteeringAngle + countersteer;

            var frontPosition = body.worldCenterOfMass + forward * profile.frontAxleOffset;
            var rearPosition = body.worldCenterOfMass - forward * profile.rearAxleOffset;
            var frontWheelForward = Quaternion.AngleAxis(steeringAngle, groundNormal) * forward;
            var frontWheelRight = Vector3.Cross(groundNormal, frontWheelForward).normalized;
            var frontVelocity = VehicleMath.GroundPlaneVelocity(body.GetPointVelocity(frontPosition), groundNormal);
            var rearVelocity = VehicleMath.GroundPlaneVelocity(body.GetPointVelocity(rearPosition), groundNormal);
            frontSlipAngle = VehicleMath.AxleSlipAngleDegrees(frontWheelForward, frontVelocity, groundNormal);
            rearSlipAngle = VehicleMath.AxleSlipAngleDegrees(forward, rearVelocity, groundNormal);

            var frontLoad = VehicleMath.FrontLoadFraction(
                profile.rearAxleOffset,
                profile.wheelbase,
                smoothedLongitudinalAcceleration,
                profile.centerOfMassHeight,
                profile.maximumLongitudinalLoadTransfer);
            var rearLoad = 1f - frontLoad;
            var gravity = Mathf.Max(0.01f, Physics.gravity.magnitude);
            var lateralLoadScale = 1f - Mathf.Clamp01(Mathf.Abs(smoothedLateralAcceleration) / gravity) * profile.lateralLoadSensitivity;
            var frontCapacity = profile.frontPeakGrip * frontLoad * lateralLoadScale;
            var rearCapacity = profile.rearPeakGrip * rearLoad * lateralLoadScale * Mathf.Lerp(1f, profile.steeringDriftRearGripMultiplier, steeringDriftBlend);

            var frontGrip = VehicleMath.TireGripAcceleration(
                frontSlipAngle,
                frontCapacity,
                profile.frontPeakSlipAngle,
                profile.slidingSlipAngle,
                profile.slidingGripRetention);
            var rearGrip = VehicleMath.TireGripAcceleration(
                rearSlipAngle,
                rearCapacity,
                profile.rearPeakSlipAngle,
                profile.slidingSlipAngle,
                profile.slidingGripRetention);
            var frontSpeedFade = Mathf.InverseLerp(0.25f, profile.lowSpeedGripFadeSpeed, Mathf.Abs(Vector3.Dot(frontVelocity, frontWheelForward)));
            var rearSpeedFade = Mathf.InverseLerp(0.25f, profile.lowSpeedGripFadeSpeed, Mathf.Abs(Vector3.Dot(rearVelocity, forward)));
            frontGrip *= frontSpeedFade;
            rearGrip *= rearSpeedFade;

            var frontLateralDirection = Mathf.Sign(Vector3.Dot(frontVelocity, frontWheelRight));
            var rearLateralDirection = Mathf.Sign(Vector3.Dot(rearVelocity, right));
            body.AddForceAtPosition(-frontWheelRight * frontLateralDirection * frontGrip, frontPosition, ForceMode.Acceleration);
            body.AddForceAtPosition(-right * rearLateralDirection * rearGrip, rearPosition, ForceMode.Acceleration);
            frontGripUtilization = frontCapacity > 0.001f ? Mathf.Clamp01(frontGrip / frontCapacity) : 0f;
            rearGripUtilization = rearCapacity > 0.001f ? Mathf.Clamp01(rearGrip / rearCapacity) : 0f;

            var speedRatio = Mathf.Clamp01(Mathf.Max(0f, forwardSpeed) / profile.maxSpeed);
            var driveFalloff = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, speedRatio));
            var driveDemand = propulsionEnabled
                ? profile.acceleration * (1f - driveFalloff) * (1f - brakeBlend)
                : 0f;
            if (forwardSpeed > profile.maxSpeed)
            {
                driveDemand -= Mathf.Min(forwardSpeed - profile.maxSpeed, profile.overspeedBraking);
            }

            var rearLongitudinalCapacity = profile.rearLongitudinalGrip * lateralLoadScale * Mathf.Lerp(1f, profile.steeringDriftRearGripMultiplier, steeringDriftBlend);
            var remainingRearGrip = VehicleMath.RemainingGrip(rearLongitudinalCapacity, rearGrip);
            var longitudinalDemand = Mathf.Clamp(driveDemand, -remainingRearGrip, remainingRearGrip);
            body.AddForceAtPosition(forward * longitudinalDemand, rearPosition, ForceMode.Acceleration);

            if (brakeBlend > 0f && forwardSpeed > 0.05f)
            {
                var stoppingAcceleration = Mathf.Min(profile.brakeDeceleration * brakeBlend, forwardSpeed / Mathf.Max(0.001f, deltaTime));
                body.AddForce(-forward * stoppingAcceleration, ForceMode.Acceleration);
            }

            body.AddTorque(groundNormal * steeringDriftControl * profile.steeringDriftYawAcceleration, ForceMode.Acceleration);

            var yawLimit = profile.maximumYawRate * Mathf.Deg2Rad;
            var yawExcess = Mathf.Max(0f, Mathf.Abs(yawSpeed) - yawLimit);
            if (yawExcess > 0f)
            {
                body.AddTorque(-groundNormal * Mathf.Sign(yawSpeed) * yawExcess * profile.yawLimitStrength, ForceMode.Acceleration);
            }

            var recoveryDamping = recoveryAmount * (1f - steeringDriftBlend) * profile.recoveryYawDamping;
            body.AddTorque(-groundNormal * yawSpeed * recoveryDamping, ForceMode.Acceleration);
            ApplyAntiSpin(planarVelocity, chassisSlip, speed, yawSpeed);
            body.AddForce(-groundNormal * profile.downforce * speedRatio, ForceMode.Acceleration);
        }

        private void ApplyAntiSpin(Vector3 planarVelocity, float chassisSlip, float speed, float yawSpeed)
        {
            if (speed < profile.antiSpinMinimumSpeed) return;
            var absoluteSlip = Mathf.Abs(chassisSlip);
            if (absoluteSlip <= profile.antiSpinSoftAngle) return;

            var correctionStrength = Mathf.InverseLerp(profile.antiSpinSoftAngle, profile.antiSpinHardAngle, absoluteSlip);
            var correctionDirection = Mathf.Sign(chassisSlip);
            body.AddTorque(groundNormal * correctionDirection * correctionStrength * profile.antiSpinYawAcceleration, ForceMode.Acceleration);

            if (yawSpeed * correctionDirection < 0f)
            {
                body.angularVelocity -= groundNormal * yawSpeed;
            }

            if (absoluteSlip <= profile.antiSpinHardAngle) return;
            var clampedForward = VehicleMath.ClampForwardToVelocity(transform.forward, planarVelocity, groundNormal, profile.antiSpinHardAngle);
            body.rotation = Quaternion.LookRotation(clampedForward, groundNormal);
        }

        private void UpdateAcceleration(Vector3 planarVelocity, Vector3 forward, Vector3 right, float deltaTime)
        {
            if (!accelerationInitialized || deltaTime <= 0f)
            {
                previousPlanarVelocity = planarVelocity;
                accelerationInitialized = true;
                return;
            }

            var acceleration = (planarVelocity - previousPlanarVelocity) / deltaTime;
            previousPlanarVelocity = planarVelocity;
            var blend = 1f - Mathf.Exp(-profile.accelerationSmoothing * deltaTime);
            smoothedLongitudinalAcceleration = Mathf.Lerp(smoothedLongitudinalAcceleration, Vector3.Dot(acceleration, forward), blend);
            smoothedLateralAcceleration = Mathf.Lerp(smoothedLateralAcceleration, Vector3.Dot(acceleration, right), blend);
        }

        private void ResetDynamicState(bool brake, float deltaTime)
        {
            brakeBlend = VehicleMath.MoveBlend(brakeBlend, brake, profile.brakeEngageRate, profile.brakeReleaseRate, deltaTime);
            steeringDriftControl = Mathf.MoveTowards(steeringDriftControl, 0f, profile.steeringDriftReleaseRate * deltaTime);
            steeringDriftBlend = Mathf.Abs(steeringDriftControl);
            accelerationInitialized = false;
            smoothedLongitudinalAcceleration = 0f;
            smoothedLateralAcceleration = 0f;
            frontGripUtilization = 0f;
            rearGripUtilization = 0f;
            frontSlipAngle = 0f;
            rearSlipAngle = 0f;
        }

        private bool TryGetGround(out Vector3 normal)
        {
            var origin = transform.position + transform.up * profile.groundProbeHeight;
            if (Physics.SphereCast(origin, profile.groundProbeRadius, -transform.up, out var hit, profile.groundProbeLength, groundMask, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return true;
            }

            normal = Vector3.up;
            return false;
        }

        private void UpdateTelemetry(float steering, bool brake, bool grounded)
        {
            var planarVelocity = VehicleMath.GroundPlaneVelocity(body.linearVelocity, grounded ? groundNormal : Vector3.up);
            var slip = VehicleMath.SlipAngleDegrees(transform.forward, planarVelocity, grounded ? groundNormal : Vector3.up);
            var driftSlip = Mathf.Max(Mathf.Abs(slip), Mathf.Abs(rearSlipAngle));
            drifting = VehicleMath.UpdateDriftState(
                drifting,
                grounded,
                planarVelocity.magnitude,
                driftSlip,
                profile.driftMinimumSpeed,
                profile.driftEnterAngle,
                profile.driftExitAngle);
            var speedIntensity = Mathf.InverseLerp(profile.effectMinimumSpeed, profile.driftMinimumSpeed + 2f, planarVelocity.magnitude);
            var angleIntensity = Mathf.InverseLerp(profile.driftExitAngle, profile.fullDriftIntensityAngle, driftSlip);
            var driftIntensity = grounded ? speedIntensity * angleIntensity : 0f;

            telemetry = new VehicleTelemetry(
                planarVelocity.magnitude,
                steering,
                planarVelocity,
                slip,
                grounded,
                drifting,
                brake,
                Vector3.Angle(transform.up, Vector3.up),
                Vector3.Dot(body.angularVelocity, grounded ? groundNormal : Vector3.up) * Mathf.Rad2Deg,
                steeringAngle,
                frontSlipAngle,
                rearSlipAngle,
                frontGripUtilization,
                rearGripUtilization,
                Mathf.Clamp01(driftIntensity),
                steeringDriftBlend);
            TelemetryUpdated?.Invoke(telemetry);
        }
    }
}
