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
        [SerializeField] private PoliceVehicleController[] policeVehicles;
        [SerializeField] private PoliceVisionSensor[] visions;
        [SerializeField] private RecklessDrivingTracker recklessDriving;
        [SerializeField] private DriftScoreController scoring;
        [SerializeField] private PoliceRoadblockController roadblock;
        [SerializeField] private PoliceProfile profile;

        private PolicePursuitModel model;
        private WantedHeatModel heat;
        private bool active = true;
        private int lastSearchWaypointIndex = -1;

        public event Action<PoliceStateTransition> StateChanged;
        public event Action<PoliceStatusSnapshot> StatusUpdated;
        public event Action PlayerBusted;
        public event Action PlayerEscaped;
        public event Action<int> HeatChanged;

        public PoliceState State => model?.State ?? PoliceState.Patrol;
        public int HeatLevel => heat?.Level ?? 0;
        public int ActiveUnitCount => CountOperationalUnits();
        public bool RoadblockDeployed => roadblock != null && roadblock.IsDeployed;

        private void Awake()
        {
            EnsureUnitArrays();
            EnsureModels();
            ApplyDeployment();
        }

        private void OnEnable() => GameplayEvents.RideEnded += HandleRideEnded;

        private void OnDisable() => GameplayEvents.RideEnded -= HandleRideEnded;

        private void Update()
        {
            if (!active || player == null || profile == null) return;
            EnsureUnitArrays();
            EnsureModels();
            if (model == null || heat == null || policeVehicles == null || policeVehicles.Length == 0) return;

            var hasVisual = HasAnyVisual();
            var speed = playerVehicle != null ? playerVehicle.Telemetry.Speed : 0f;
            var velocity = playerVehicle != null ? playerVehicle.Telemetry.GroundVelocity : Vector3.zero;
            var distance = NearestOperationalDistance();
            var reckless = recklessDriving != null && recklessDriving.IsReckless;
            var result = model.Tick(hasVisual, reckless, player.position, velocity, speed, distance, Time.deltaTime);

            if (result.StateChanged)
            {
                if (result.State == PoliceState.Pursuit && heat.ReportInitialOffense())
                {
                    ApplyDeployment();
                    HeatChanged?.Invoke(heat.Level);
                }

                SetUnitStates(result.State);
                lastSearchWaypointIndex = -1;
                StateChanged?.Invoke(new PoliceStateTransition(result.PreviousState, result.State));

                if (result.State == PoliceState.Escaped)
                {
                    var reward = heat.EscapeReward;
                    if (reward > 0) scoring?.AwardBonus(reward, $"ESCAPED HEAT {heat.Level}");
                    PlayerEscaped?.Invoke();
                    heat.Reset();
                    ApplyDeployment();
                    HeatChanged?.Invoke(0);
                }
            }

            if (model.State == PoliceState.Pursuit && heat.Tick(hasVisual && reckless, Time.deltaTime))
            {
                ApplyDeployment();
                SetUnitStates(model.State);
                HeatChanged?.Invoke(heat.Level);
            }

            if (result.Busted)
            {
                if (playerRecovery == null || !playerRecovery.ResetToRoad()) GameplayEvents.RaiseComboCancellation();
                PlayerBusted?.Invoke();
                heat.Reset();
                ApplyDeployment();
                HeatChanged?.Invoke(0);
            }

            UpdateDestinations(hasVisual, velocity);
            StatusUpdated?.Invoke(new PoliceStatusSnapshot(model.State, hasVisual, model.HasLastSeen, model.LastSeenPosition, model.SearchRemaining, heat.Level, CountOperationalUnits(), RoadblockDeployed));
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
            Configure(playerTransform, vehicle, recovery, new[] { navigator }, new[] { sensor }, tracker, policeProfile, null, null);
        }

        public void Configure(
            Transform playerTransform,
            VehicleController vehicle,
            VehicleRecovery recovery,
            PoliceVehicleController[] navigators,
            PoliceVisionSensor[] sensors,
            RecklessDrivingTracker tracker,
            PoliceProfile policeProfile,
            DriftScoreController scoreController,
            PoliceRoadblockController roadblockController)
        {
            player = playerTransform;
            playerVehicle = vehicle;
            playerRecovery = recovery;
            policeVehicles = navigators;
            visions = sensors;
            policeVehicle = navigators != null && navigators.Length > 0 ? navigators[0] : null;
            vision = sensors != null && sensors.Length > 0 ? sensors[0] : null;
            recklessDriving = tracker;
            profile = policeProfile;
            scoring = scoreController;
            roadblock = roadblockController;
            model = profile != null ? new PolicePursuitModel(profile) : null;
            heat = profile != null ? new WantedHeatModel(profile) : null;
            ApplyDeployment();
            SetUnitStates(PoliceState.Patrol);
        }

        private void EnsureModels()
        {
            if (model == null && profile != null) model = new PolicePursuitModel(profile);
            if (heat == null && profile != null) heat = new WantedHeatModel(profile);
        }

        private void EnsureUnitArrays()
        {
            if ((policeVehicles == null || policeVehicles.Length == 0) && policeVehicle != null) policeVehicles = new[] { policeVehicle };
            if ((visions == null || visions.Length == 0) && vision != null) visions = new[] { vision };
        }

        private bool HasAnyVisual()
        {
            if (visions == null || policeVehicles == null) return false;
            for (var i = 0; i < visions.Length; i++)
            {
                if (visions[i] == null || i >= policeVehicles.Length || policeVehicles[i] == null || !policeVehicles[i].IsOperational) continue;
                if (visions[i].HasVisual) return true;
            }
            return false;
        }

        private float NearestOperationalDistance()
        {
            var nearest = float.MaxValue;
            for (var i = 0; i < policeVehicles.Length; i++)
            {
                var unit = policeVehicles[i];
                if (unit == null || !unit.IsOperational) continue;
                nearest = Mathf.Min(nearest, Vector3.Distance(unit.transform.position, player.position));
            }
            return nearest;
        }

        private void ApplyDeployment()
        {
            EnsureUnitArrays();
            var wantedUnits = model != null && model.State != PoliceState.Patrol && model.State != PoliceState.Escaped
                ? Mathf.Max(1, heat?.ActiveUnitCount ?? 1)
                : 1;
            if (policeVehicles != null)
            {
                for (var i = 0; i < policeVehicles.Length; i++) policeVehicles[i]?.SetOperational(i < wantedUnits);
            }
            roadblock?.SetDeployed(heat != null && heat.RoadblockRequired && model != null && (model.State == PoliceState.Pursuit || model.State == PoliceState.Search));
        }

        private int CountOperationalUnits()
        {
            if (policeVehicles == null) return 0;
            var count = 0;
            for (var i = 0; i < policeVehicles.Length; i++) if (policeVehicles[i] != null && policeVehicles[i].IsOperational) count++;
            return count;
        }

        private void SetUnitStates(PoliceState state)
        {
            if (policeVehicles == null) return;
            for (var i = 0; i < policeVehicles.Length; i++)
            {
                if (policeVehicles[i] != null && policeVehicles[i].IsOperational) policeVehicles[i].SetState(state);
            }
        }

        private void UpdateDestinations(bool hasVisual, Vector3 velocity)
        {
            if (!model.HasLastSeen || policeVehicles == null) return;
            if (model.State == PoliceState.Pursuit)
            {
                var target = model.EstimatedPlayerPosition;
                var right = velocity.sqrMagnitude > 0.1f ? Vector3.Cross(Vector3.up, velocity.normalized) : player.right;
                for (var i = 0; i < policeVehicles.Length; i++)
                {
                    var unit = policeVehicles[i];
                    if (unit == null || !unit.IsOperational) continue;
                    var unitTarget = target;
                    if (hasVisual) unitTarget += velocity * (profile.interceptLeadTime + i * 0.2f);
                    if (i > 0) unitTarget += right * (i % 2 == 0 ? -12f : 12f);
                    unit.SetDestination(unitTarget);
                }
            }
            else if (model.State == PoliceState.Search)
            {
                var searchIndex = model.SearchWaypointIndex;
                if (searchIndex == lastSearchWaypointIndex) return;
                lastSearchWaypointIndex = searchIndex;
                for (var i = 0; i < policeVehicles.Length; i++)
                {
                    var unit = policeVehicles[i];
                    if (unit != null && unit.IsOperational) unit.SetSearchDestination(model.EstimatedPlayerPosition, searchIndex + i);
                }
            }
        }

        private void HandleRideEnded(RideEndReason reason)
        {
            active = false;
            if (policeVehicles != null)
            {
                for (var i = 0; i < policeVehicles.Length; i++) if (policeVehicles[i] != null) policeVehicles[i].enabled = false;
            }
            roadblock?.SetDeployed(false);
        }
    }
}
