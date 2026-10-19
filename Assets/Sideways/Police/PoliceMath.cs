using UnityEngine;

namespace Sideways.Police
{
    public static class PoliceMath
    {
        public static bool IsWithinVision(Vector3 origin, Vector3 forward, Vector3 target, float range, float angle)
        {
            var toTarget = Vector3.ProjectOnPlane(target - origin, Vector3.up);
            if (toTarget.sqrMagnitude > range * range || toTarget.sqrMagnitude < 0.0001f) return false;
            var flatForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            return Vector3.Angle(flatForward, toTarget.normalized) <= angle * 0.5f;
        }
    }
}
