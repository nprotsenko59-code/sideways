using Sideways.Configuration;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Police
{
    public sealed class RecklessDrivingTracker : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicle;
        [SerializeField] private VehicleCollisionSensor collisionSensor;
        [SerializeField] private PoliceProfile profile;

        private float driftElapsed;
        private float recklessRemaining;
        private bool subscribed;

        public bool IsReckless => recklessRemaining > 0f;

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            recklessRemaining = Mathf.Max(0f, recklessRemaining - Time.deltaTime);
        }

        public void Configure(VehicleController vehicleController, VehicleCollisionSensor sensor, PoliceProfile policeProfile)
        {
            Unsubscribe();
            vehicle = vehicleController;
            collisionSensor = sensor;
            profile = policeProfile;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            if (vehicle != null) vehicle.TelemetryUpdated += HandleTelemetry;
            if (collisionSensor != null) collisionSensor.ImpactReported += HandleImpact;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (vehicle != null) vehicle.TelemetryUpdated -= HandleTelemetry;
            if (collisionSensor != null) collisionSensor.ImpactReported -= HandleImpact;
            subscribed = false;
        }

        private void HandleTelemetry(VehicleTelemetry telemetry)
        {
            if (profile == null) return;
            if (telemetry.Drifting && telemetry.Speed >= profile.driftTriggerMinimumSpeed)
            {
                driftElapsed += Time.fixedDeltaTime;
                if (driftElapsed >= profile.driftTriggerDuration) recklessRemaining = profile.recklessMemory;
            }
            else
            {
                driftElapsed = 0f;
            }
        }

        private void HandleImpact(VehicleImpact impact)
        {
            if (profile != null && impact.RelativeSpeed >= profile.severeCollisionSpeed)
            {
                recklessRemaining = profile.recklessMemory;
            }
        }
    }
}
