using System;
using Sideways.Configuration;
using Sideways.Vehicle;
using UnityEngine;

namespace Sideways.Core
{
    public sealed class DriftScoreController : MonoBehaviour, IRideScoreProvider
    {
        [SerializeField] private VehicleController vehicle;
        [SerializeField] private VehicleCollisionSensor collisionSensor;
        [SerializeField] private ScoringProfile profile;

        private DriftScoringModel model;
        private bool subscribed;

        public event Action<DriftScoreSnapshot> ScoreUpdated;
        public event Action<int> ComboBanked;
        public event Action ComboCancelled;
        public event Action<int> DriftPointsEarned;
        public event Action<int, string> BonusAwarded;

        public int CurrentScore => Snapshot.TotalScore;
        public DriftScoreSnapshot Snapshot => model?.Snapshot ?? default;
        public float ComboGraceDuration => profile != null ? profile.comboGraceDuration : 1f;

        private void Awake() => EnsureModel();

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        public void Configure(VehicleController vehicleController, VehicleCollisionSensor sensor, ScoringProfile scoringProfile)
        {
            Unsubscribe();
            vehicle = vehicleController;
            collisionSensor = sensor;
            profile = scoringProfile;
            model = profile != null ? new DriftScoringModel(profile) : null;
            if (isActiveAndEnabled) Subscribe();
            Publish();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            if (vehicle != null) vehicle.TelemetryUpdated += HandleTelemetry;
            if (collisionSensor != null) collisionSensor.ImpactReported += HandleImpact;
            GameplayEvents.ComboCancellationRequested += HandleComboCancellation;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (vehicle != null) vehicle.TelemetryUpdated -= HandleTelemetry;
            if (collisionSensor != null) collisionSensor.ImpactReported -= HandleImpact;
            GameplayEvents.ComboCancellationRequested -= HandleComboCancellation;
            subscribed = false;
        }

        private void EnsureModel()
        {
            if (model == null && profile != null) model = new DriftScoringModel(profile);
        }

        private void HandleTelemetry(VehicleTelemetry telemetry)
        {
            EnsureModel();
            if (model == null) return;
            var before = model.Snapshot;
            var after = model.Tick(telemetry, Time.fixedDeltaTime);
            var earned = Mathf.Max(0, after.TotalScore - before.TotalScore);
            if (earned > 0 && after.Scoring) DriftPointsEarned?.Invoke(earned);
            if (before.HasActiveCombo && !after.HasActiveCombo && after.BankedScore > before.BankedScore)
            {
                ComboBanked?.Invoke(after.BankedScore - before.BankedScore);
            }
            ScoreUpdated?.Invoke(after);
        }

        private void HandleImpact(VehicleImpact impact)
        {
            EnsureModel();
            if (model == null) return;
            var cancelled = model.RegisterImpact(impact.RelativeSpeed);
            if (cancelled) ComboCancelled?.Invoke();
            Publish();
        }

        private void HandleComboCancellation()
        {
            EnsureModel();
            if (model == null) return;
            if (model.CancelCombo()) ComboCancelled?.Invoke();
            Publish();
        }

        public bool AwardBonus(int points, string reason)
        {
            EnsureModel();
            if (model == null || !model.AwardBankedBonus(points)) return false;
            BonusAwarded?.Invoke(points, reason ?? string.Empty);
            Publish();
            return true;
        }

        public void SetComboGraceHeld(bool held)
        {
            EnsureModel();
            model?.SetComboGraceHeld(held);
        }

        private void Publish() => ScoreUpdated?.Invoke(Snapshot);
    }
}
