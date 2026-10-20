using UnityEngine;

namespace Sideways.Traffic
{
    public readonly struct NearMissEvent
    {
        public NearMissEvent(string trafficVehicleId, float closestDistance, float relativeSpeed, int points, Vector3 position)
        {
            TrafficVehicleId = trafficVehicleId;
            ClosestDistance = closestDistance;
            RelativeSpeed = relativeSpeed;
            Points = points;
            Position = position;
        }

        public string TrafficVehicleId { get; }
        public float ClosestDistance { get; }
        public float RelativeSpeed { get; }
        public int Points { get; }
        public Vector3 Position { get; }
    }

    public static class TrafficEvents
    {
        public static event System.Action<NearMissEvent> NearMiss;

        internal static void RaiseNearMiss(NearMissEvent nearMiss) => NearMiss?.Invoke(nearMiss);

        public static void ClearAllListeners() => NearMiss = null;
    }
}
