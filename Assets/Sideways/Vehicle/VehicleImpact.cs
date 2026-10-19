using UnityEngine;

namespace Sideways.Vehicle
{
    public readonly struct VehicleImpact
    {
        public VehicleImpact(float relativeSpeed, Vector3 point, Vector3 normal, int otherLayer)
        {
            RelativeSpeed = Mathf.Max(0f, relativeSpeed);
            Point = point;
            Normal = normal;
            OtherLayer = otherLayer;
        }

        public float RelativeSpeed { get; }
        public Vector3 Point { get; }
        public Vector3 Normal { get; }
        public int OtherLayer { get; }
    }
}
