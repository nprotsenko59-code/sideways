using UnityEngine;

namespace Sideways.Configuration
{
    [CreateAssetMenu(menuName = "Sideways/Vehicle Handling Profile", fileName = "VehicleHandlingProfile")]
    public sealed class VehicleHandlingProfile : ScriptableObject
    {
        [Header("Speed")]
        [Min(0f)] public float acceleration = 16f;
        [Min(1f)] public float maxSpeed = 28f;
        [Min(0f)] public float overspeedBraking = 8f;
        [Min(0f)] public float downforce = 12f;

        [Header("Geometry")]
        [Min(0.5f)] public float wheelbase = 2.6f;
        [Min(0.1f)] public float frontAxleOffset = 1.25f;
        [Min(0.1f)] public float rearAxleOffset = 1.35f;
        [Min(0.05f)] public float centerOfMassHeight = 0.55f;

        [Header("Steering")]
        [Range(1f, 60f)] public float lowSpeedSteeringAngle = 38f;
        [Range(1f, 45f)] public float highSpeedSteeringAngle = 20f;
        [Min(1f)] public float fullSteeringReductionSpeed = 32f;
        [Min(0.1f)] public float inputResponse = 4.5f;
        [Range(0.1f, 1f)] public float initialSteeringStrength = 0.38f;
        [Min(0.1f)] public float fullDriftHoldDuration = 1.1f;
        [Range(0f, 0.5f)] public float countersteerAssist = 0.18f;
        [Range(0f, 15f)] public float maximumCountersteerAssistAngle = 5f;
        [Range(0f, 45f)] public float countersteerMinimumYawRate = 4f;

        [Header("Axle grip")]
        [Min(0.1f)] public float frontPeakGrip = 12f;
        [Min(0.1f)] public float rearPeakGrip = 8.8f;
        [Min(0.1f)] public float rearLongitudinalGrip = 18.5f;
        [Range(0.5f, 45f)] public float frontPeakSlipAngle = 9f;
        [Range(0.5f, 45f)] public float rearPeakSlipAngle = 8f;
        [Range(1f, 90f)] public float slidingSlipAngle = 35f;
        [Range(0.1f, 1f)] public float slidingGripRetention = 0.68f;
        [Min(0.1f)] public float lowSpeedGripFadeSpeed = 2f;

        [Header("Load transfer")]
        [Range(0f, 0.4f)] public float maximumLongitudinalLoadTransfer = 0.18f;
        [Range(0f, 0.4f)] public float lateralLoadSensitivity = 0.12f;
        [Min(0.1f)] public float accelerationSmoothing = 7f;

        [Header("Drift detection")]
        [Min(0f)] public float driftMinimumSpeed = 8f;
        [Range(0f, 90f)] public float driftEnterAngle = 12f;
        [Range(0f, 90f)] public float driftExitAngle = 6f;
        [Range(1f, 90f)] public float fullDriftIntensityAngle = 32f;

        [Header("Hold-to-drift steering")]
        [Range(0.05f, 1f)] public float steeringDriftRearGripMultiplier = 0.42f;
        [Min(0f)] public float steeringDriftYawAcceleration = 2.4f;
        [Min(0.1f)] public float steeringDriftEngageRate = 3.5f;
        [Min(0.1f)] public float steeringDriftReleaseRate = 5f;

        [Header("Service brake")]
        [Min(0f)] public float brakeDeceleration = 18f;
        [Min(0.1f)] public float brakeEngageRate = 9f;
        [Min(0.1f)] public float brakeReleaseRate = 12f;

        [Header("Stability assistance")]
        [Range(30f, 360f)] public float maximumYawRate = 175f;
        [Min(0f)] public float yawLimitStrength = 5f;
        [Min(0f)] public float recoveryYawDamping = 0.55f;
        [Range(20f, 89f)] public float antiSpinSoftAngle = 55f;
        [Range(30f, 89f)] public float antiSpinHardAngle = 78f;
        [Min(0f)] public float antiSpinYawAcceleration = 14f;
        [Min(0f)] public float antiSpinMinimumSpeed = 4f;

        [Header("Drift feedback")]
        [Min(0f)] public float effectMinimumSpeed = 6f;
        [Range(0f, 90f)] public float skidSlipAngle = 8f;
        [Range(0f, 90f)] public float smokeSlipAngle = 12f;
        [Range(0f, 20f)] public float maximumBodyRoll = 5f;
        [Range(0f, 10f)] public float maximumBodyPitch = 2.5f;
        [Min(0.1f)] public float visualResponse = 7f;

        [Header("Grounding")]
        [Min(0.1f)] public float groundProbeLength = 1.1f;
        [Min(0.01f)] public float groundProbeRadius = 0.18f;
        [Min(0f)] public float groundProbeHeight = 0.35f;

        public bool IsValid(out string error)
        {
            if (maxSpeed <= 0f) { error = "Max speed must be positive."; return false; }
            if (acceleration < 0f || overspeedBraking < 0f || downforce < 0f) { error = "Speed forces must not be negative."; return false; }
            if (wheelbase <= 0f || frontAxleOffset <= 0f || rearAxleOffset <= 0f) { error = "Vehicle geometry must be positive."; return false; }
            if (Mathf.Abs(frontAxleOffset + rearAxleOffset - wheelbase) > 0.05f) { error = "Front and rear axle offsets must add up to the wheelbase."; return false; }
            if (highSpeedSteeringAngle > lowSpeedSteeringAngle) { error = "High-speed steering angle must not exceed low-speed steering angle."; return false; }
            if (lowSpeedSteeringAngle <= 0f || highSpeedSteeringAngle <= 0f || fullSteeringReductionSpeed <= 0f || inputResponse <= 0f || fullDriftHoldDuration <= 0f) { error = "Steering angles, speed, response, and hold duration must be positive."; return false; }
            if (initialSteeringStrength <= 0f || initialSteeringStrength > 1f) { error = "Initial steering strength must be within (0, 1]."; return false; }
            if (countersteerAssist < 0f || countersteerAssist > 0.5f || maximumCountersteerAssistAngle < 0f || countersteerMinimumYawRate < 0f) { error = "Countersteer assistance is outside its supported range."; return false; }
            if (frontPeakGrip <= 0f || rearPeakGrip <= 0f || rearLongitudinalGrip <= 0f) { error = "Axle grip values must be positive."; return false; }
            if (frontPeakSlipAngle <= 0f || rearPeakSlipAngle <= 0f || slidingGripRetention <= 0f || slidingGripRetention > 1f || lowSpeedGripFadeSpeed <= 0f) { error = "Tire curve values are outside their supported ranges."; return false; }
            if (slidingSlipAngle <= frontPeakSlipAngle || slidingSlipAngle <= rearPeakSlipAngle) { error = "Sliding slip angle must exceed both axle peak-slip angles."; return false; }
            if (maximumLongitudinalLoadTransfer < 0f || maximumLongitudinalLoadTransfer > 0.4f || lateralLoadSensitivity < 0f || lateralLoadSensitivity > 0.4f || accelerationSmoothing <= 0f) { error = "Load-transfer values are outside their supported ranges."; return false; }
            if (driftMinimumSpeed < 0f || driftEnterAngle < 0f || driftExitAngle < 0f) { error = "Drift thresholds must not be negative."; return false; }
            if (driftExitAngle > driftEnterAngle) { error = "Drift exit angle must not exceed enter angle."; return false; }
            if (fullDriftIntensityAngle <= driftEnterAngle) { error = "Full drift intensity angle must exceed the drift enter angle."; return false; }
            if (steeringDriftRearGripMultiplier <= 0f || steeringDriftRearGripMultiplier > 1f) { error = "Steering-drift rear grip multiplier must be within (0, 1]."; return false; }
            if (steeringDriftYawAcceleration < 0f || steeringDriftEngageRate <= 0f || steeringDriftReleaseRate <= 0f) { error = "Steering-drift response values are invalid."; return false; }
            if (brakeDeceleration < 0f || brakeEngageRate <= 0f || brakeReleaseRate <= 0f) { error = "Brake deceleration and blend rates are invalid."; return false; }
            if (maximumYawRate <= 0f || yawLimitStrength < 0f || recoveryYawDamping < 0f || antiSpinYawAcceleration < 0f || antiSpinMinimumSpeed < 0f) { error = "Stability-assistance values are invalid."; return false; }
            if (antiSpinSoftAngle <= 0f || antiSpinHardAngle <= antiSpinSoftAngle || antiSpinHardAngle >= 90f) { error = "Anti-spin angles must form a valid envelope below 90 degrees."; return false; }
            if (effectMinimumSpeed < 0f || skidSlipAngle < 0f || maximumBodyRoll < 0f || maximumBodyPitch < 0f || visualResponse <= 0f) { error = "Drift-feedback values are invalid."; return false; }
            if (smokeSlipAngle < skidSlipAngle) { error = "Smoke slip angle must not be below the skid slip angle."; return false; }
            if (groundProbeLength <= groundProbeHeight) { error = "Ground probe length must exceed its height offset."; return false; }
            error = string.Empty;
            return true;
        }
    }
}
