using UnityEngine;

namespace Sideways.Vehicle
{
    public readonly struct VehicleTelemetry
    {
        public VehicleTelemetry(float speed, float steering, Vector3 groundVelocity, float slipAngle, bool grounded, bool drifting, bool handbrake, float angleFromUpright)
        {
            Speed = speed;
            Steering = steering;
            GroundVelocity = groundVelocity;
            SlipAngle = slipAngle;
            Grounded = grounded;
            Drifting = drifting;
            Handbrake = handbrake;
            AngleFromUpright = angleFromUpright;
        }

        public float Speed { get; }
        public float Steering { get; }
        public Vector3 GroundVelocity { get; }
        public float SlipAngle { get; }
        public bool Grounded { get; }
        public bool Drifting { get; }
        public bool Handbrake { get; }
        public float AngleFromUpright { get; }
    }
}
