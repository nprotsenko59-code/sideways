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

    public readonly struct TrafficVehicleDestroyedEvent
    {
        public TrafficVehicleDestroyedEvent(string vehicleId, float impactSpeed, Vector3 position)
        {
            VehicleId = vehicleId;
            ImpactSpeed = impactSpeed;
            Position = position;
        }

        public string VehicleId { get; }
        public float ImpactSpeed { get; }
        public Vector3 Position { get; }
    }

    public static class TrafficEvents
    {
        public static event System.Action<NearMissEvent> NearMiss;
        public static event System.Action<TrafficVehicleDestroyedEvent> VehicleDestroyed;

        internal static void RaiseNearMiss(NearMissEvent nearMiss) => NearMiss?.Invoke(nearMiss);
        internal static void RaiseVehicleDestroyed(TrafficVehicleDestroyedEvent destroyed) => VehicleDestroyed?.Invoke(destroyed);

        public static void ClearAllListeners()
        {
            NearMiss = null;
            VehicleDestroyed = null;
        }
    }
}
