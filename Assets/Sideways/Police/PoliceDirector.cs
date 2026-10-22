using System;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Police
{
    public sealed class PoliceDirector : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private VehicleController playerVehicle;
        [SerializeField] private VehicleRecovery playerRecovery;
        [SerializeField] private PoliceVehicleController policeVehicle;
        [SerializeField] private PoliceVisionSensor vision;
        [SerializeField] private RecklessDrivingTracker recklessDriving;
        [SerializeField] private PoliceProfile profile;

        private PolicePursuitModel model;
        private bool active = true;
        private int lastSearchWaypointIndex = -1;

        public event Action<PoliceStateTransition> StateChanged;
        public event Action<PoliceStatusSnapshot> StatusUpdated;
        public event Action PlayerBusted;
        public event Action PlayerEscaped;

        public PoliceState State => model?.State ?? PoliceState.Patrol;

        private void Awake() => EnsureModel();

        private void OnEnable() => GameplayEvents.RideEnded += HandleRideEnded;

        private void OnDisable() => GameplayEvents.RideEnded -= HandleRideEnded;

        private void Update()
        {
            if (!active || player == null || policeVehicle == null || profile == null) return;
            EnsureModel();
            if (model == null) return;

            var hasVisual = vision != null && vision.HasVisual;
            var speed = playerVehicle != null ? playerVehicle.Telemetry.Speed : 0f;
            var velocity = playerVehicle != null ? playerVehicle.Telemetry.GroundVelocity : Vector3.zero;
            var distance = Vector3.Distance(policeVehicle.transform.position, player.position);
            var result = model.Tick(hasVisual, recklessDriving != null && recklessDriving.IsReckless, player.position, velocity, speed, distance, Time.deltaTime);

            if (result.StateChanged)
            {
                policeVehicle.SetState(result.State);
                lastSearchWaypointIndex = -1;
                StateChanged?.Invoke(new PoliceStateTransition(result.PreviousState, result.State));
                if (result.State == PoliceState.Escaped) PlayerEscaped?.Invoke();
            }

            if (result.Busted)
            {
                if (playerRecovery == null || !playerRecovery.ResetToRoad()) GameplayEvents.RaiseComboCancellation();
                PlayerBusted?.Invoke();
            }

            if (model.State == PoliceState.Pursuit && model.HasLastSeen)
            {
                var target = model.EstimatedPlayerPosition;
                if (hasVisual) target += velocity * profile.interceptLeadTime;
                policeVehicle.SetDestination(target);
            }
            else if (model.State == PoliceState.Search && model.HasLastSeen)
            {
                var searchIndex = model.SearchWaypointIndex;
                if (searchIndex != lastSearchWaypointIndex)
                {
                    lastSearchWaypointIndex = searchIndex;
                    policeVehicle.SetSearchDestination(model.EstimatedPlayerPosition, searchIndex);
                }
            }

            StatusUpdated?.Invoke(new PoliceStatusSnapshot(model.State, hasVisual, model.HasLastSeen, model.LastSeenPosition, model.SearchRemaining));
        }

        public void Configure(
            Transform playerTransform,
            VehicleController vehicle,
            VehicleRecovery recovery,
            PoliceVehicleController navigator,
            PoliceVisionSensor sensor,
            RecklessDrivingTracker tracker,
            PoliceProfile policeProfile)
        {
            player = playerTransform;
            playerVehicle = vehicle;
            playerRecovery = recovery;
            policeVehicle = navigator;
            vision = sensor;
            recklessDriving = tracker;
            profile = policeProfile;
            model = profile != null ? new PolicePursuitModel(profile) : null;
            policeVehicle?.SetState(PoliceState.Patrol);
        }

        private void EnsureModel()
        {
            if (model == null && profile != null) model = new PolicePursuitModel(profile);
        }

        private void HandleRideEnded(RideEndReason reason)
        {
            active = false;
            if (policeVehicle != null) policeVehicle.enabled = false;
        }
    }
}
