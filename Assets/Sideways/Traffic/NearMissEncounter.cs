using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Traffic
{
    public sealed class NearMissEncounter : MonoBehaviour
    {
        [SerializeField] private TrafficVehicle trafficVehicle;
        [SerializeField] private TrafficProfile profile;

        private Rigidbody playerBody;
        private bool encounterActive;
        private bool collisionOccurred;
        private float closestDistance;
        private float maximumRelativeSpeed;

        public void Configure(TrafficVehicle vehicle, TrafficProfile trafficProfile)
        {
            trafficVehicle = vehicle;
            profile = trafficProfile;
        }

        public void NotifyCollision()
        {
            if (encounterActive) collisionOccurred = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (encounterActive || other.gameObject.layer != 6 || trafficVehicle == null || profile == null) return;
            playerBody = other.attachedRigidbody;
            encounterActive = playerBody != null;
            collisionOccurred = false;
            closestDistance = float.PositiveInfinity;
            maximumRelativeSpeed = 0f;
            Sample();
        }

        private void OnTriggerStay(Collider other)
        {
            if (encounterActive && other.attachedRigidbody == playerBody) Sample();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!encounterActive || other.attachedRigidbody != playerBody) return;
            Sample();
            if (!collisionOccurred && maximumRelativeSpeed >= profile.nearMissRelativeSpeed)
            {
                TrafficEvents.RaiseNearMiss(new NearMissEvent(trafficVehicle.name, closestDistance, maximumRelativeSpeed, profile.nearMissPoints, transform.position));
            }
            encounterActive = false;
            playerBody = null;
        }

        private void Sample()
        {
            if (playerBody == null || trafficVehicle == null) return;
            closestDistance = Mathf.Min(closestDistance, Vector3.Distance(playerBody.worldCenterOfMass, trafficVehicle.transform.position));
            maximumRelativeSpeed = Mathf.Max(maximumRelativeSpeed, (playerBody.linearVelocity - trafficVehicle.Velocity).magnitude);
        }
    }
}
