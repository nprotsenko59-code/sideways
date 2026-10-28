using UnityEngine;

namespace Sideways.Vehicle
{
    public readonly struct VehicleTelemetry
    {
        public VehicleTelemetry(float speed, float steering, Vector3 groundVelocity, float slipAngle, bool grounded, bool drifting, bool brake, float angleFromUpright, float yawRateDegrees = 0f)
            : this(speed, steering, groundVelocity, slipAngle, grounded, drifting, brake, angleFromUpright, yawRateDegrees, 0f, 0f, 0f, 0f, 0f, 0f, 0f)
        {
        }

        public VehicleTelemetry(
            float speed,
            float steering,
            Vector3 groundVelocity,
            float slipAngle,
            bool grounded,
            bool drifting,
            bool brake,
            float angleFromUpright,
            float yawRateDegrees,
            float steeringAngleDegrees,
            float frontSlipAngle,
            float rearSlipAngle,
            float frontGripUtilization,
            float rearGripUtilization,
            float driftIntensity,
            float controlDriftStrength)
        {
            Speed = speed;
            Steering = steering;
            GroundVelocity = groundVelocity;
            SlipAngle = slipAngle;
            Grounded = grounded;
            Drifting = drifting;
            Brake = brake;
            AngleFromUpright = angleFromUpright;
            YawRateDegrees = yawRateDegrees;
            SteeringAngleDegrees = steeringAngleDegrees;
            FrontSlipAngle = frontSlipAngle;
            RearSlipAngle = rearSlipAngle;
            FrontGripUtilization = frontGripUtilization;
            RearGripUtilization = rearGripUtilization;
            DriftIntensity = driftIntensity;
            ControlDriftStrength = controlDriftStrength;
        }

        public float Speed { get; }
        public float Steering { get; }
        public Vector3 GroundVelocity { get; }
        public float SlipAngle { get; }
        public bool Grounded { get; }
        public bool Drifting { get; }
        public bool Brake { get; }
        public float AngleFromUpright { get; }
        public float YawRateDegrees { get; }
        public float SteeringAngleDegrees { get; }
        public float FrontSlipAngle { get; }
        public float RearSlipAngle { get; }
        public float FrontGripUtilization { get; }
        public float RearGripUtilization { get; }
        public float DriftIntensity { get; }
        public float ControlDriftStrength { get; }
    }
}
