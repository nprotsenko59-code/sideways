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
    }
}
