using UnityEngine;

namespace Sideways.Configuration
{
    [CreateAssetMenu(menuName = "Sideways/Vehicle Handling Profile", fileName = "VehicleHandlingProfile")]
    public sealed class VehicleHandlingProfile : ScriptableObject
    {
        [Header("Speed")]
        [Min(0f)] public float acceleration = 11f;
        [Min(1f)] public float maxSpeed = 28f;
        [Min(0f)] public float overspeedBraking = 8f;
        [Min(0f)] public float downforce = 12f;

        [Header("Steering")]
        [Min(0f)] public float steeringAcceleration = 7.5f;
        [Min(0f)] public float yawDamping = 3.5f;
        [Range(0f, 1f)] public float minimumSteeringAtMaxSpeed = 0.35f;
        [Min(0.1f)] public float inputResponse = 4.5f;

        [Header("Grip and drift")]
        [Min(0f)] public float normalLateralGrip = 8f;
        [Min(0f)] public float driftLateralGrip = 2.1f;
        [Min(0f)] public float driftMinimumSpeed = 8f;
        [Range(0f, 90f)] public float driftEnterAngle = 12f;
        [Range(0f, 90f)] public float driftExitAngle = 6f;
        [Min(0f)] public float driftInputThreshold = 0.55f;

        [Header("Handbrake drift")]
        [Min(0f)] public float handbrakeLateralGrip = 0.32f;
        [Min(1f)] public float handbrakeYawMultiplier = 2.75f;
        [Min(0f)] public float handbrakeDeceleration = 2.2f;

        [Header("Grounding")]
        [Min(0.1f)] public float groundProbeLength = 1.1f;
        [Min(0.01f)] public float groundProbeRadius = 0.18f;
        [Min(0f)] public float groundProbeHeight = 0.35f;

        public bool IsValid(out string error)
        {
            if (maxSpeed <= 0f) { error = "Max speed must be positive."; return false; }
            if (driftExitAngle > driftEnterAngle) { error = "Drift exit angle must not exceed enter angle."; return false; }
            if (handbrakeLateralGrip > driftLateralGrip) { error = "Handbrake grip must not exceed drift grip."; return false; }
            if (groundProbeLength <= groundProbeHeight) { error = "Ground probe length must exceed its height offset."; return false; }
            error = string.Empty;
            return true;
        }
    }
}
