using System;
using Sideways.Configuration;
using Sideways.Core;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Stunts
{
    public readonly struct StuntCompletedEvent
    {
        public StuntCompletedEvent(float airtime, int points)
        {
            Airtime = airtime;
            Points = points;
        }

        public float Airtime { get; }
        public int Points { get; }
    }

    public sealed class StuntController : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicle;
        [SerializeField] private DriftScoreController scoring;
        [SerializeField] private StuntProfile profile;

        private AirborneStuntModel model;
        private bool comboHeld;

        public event Action<StuntCompletedEvent> StuntCompleted;
        public bool IsStunting => model != null && model.IsAirborneStunt;

        private void OnEnable()
        {
            if (vehicle != null) vehicle.TelemetryUpdated += HandleTelemetry;
            StuntRamp.RampEntered += HandleRampEntered;
        }

        private void OnDisable()
        {
            if (vehicle != null) vehicle.TelemetryUpdated -= HandleTelemetry;
            StuntRamp.RampEntered -= HandleRampEntered;
            SetComboHeld(false);
        }

        public void Configure(VehicleController vehicleController, DriftScoreController scoreController, StuntProfile stuntProfile)
        {
            if (isActiveAndEnabled && vehicle != null) vehicle.TelemetryUpdated -= HandleTelemetry;
            vehicle = vehicleController;
            scoring = scoreController;
            profile = stuntProfile;
            model = profile != null ? new AirborneStuntModel(profile) : null;
            if (isActiveAndEnabled && vehicle != null) vehicle.TelemetryUpdated += HandleTelemetry;
        }

        private void HandleRampEntered(string rampId, VehicleController enteringVehicle)
        {
            if (enteringVehicle != vehicle || model == null) return;
            model.Arm(vehicle.Telemetry.Speed);
        }

        private void HandleTelemetry(VehicleTelemetry telemetry)
        {
            if (model == null || vehicle == null) return;
            var update = model.Tick(
                telemetry.Grounded,
                telemetry.Speed,
                telemetry.AngleFromUpright,
                vehicle.Body.linearVelocity.y,
                Time.fixedDeltaTime);
            SetComboHeld(update.ComboHold && scoring != null && scoring.Snapshot.HasActiveCombo);
            if (update.Bonus <= 0) return;
            scoring?.AwardBonus(update.Bonus, $"CLEAN LANDING  {update.Airtime:0.0}s");
            StuntCompleted?.Invoke(new StuntCompletedEvent(update.Airtime, update.Bonus));
        }

        private void SetComboHeld(bool held)
        {
            if (comboHeld == held) return;
            comboHeld = held;
            scoring?.SetComboGraceHeld(held);
        }
    }
}
