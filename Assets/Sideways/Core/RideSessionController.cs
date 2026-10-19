using Sideways.Configuration;
using Sideways.UI;
using Sideways.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sideways.Core
{
    public sealed class RideSessionController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private VehicleController vehicle;
        [SerializeField] private RideRulesProfile rules;
        [SerializeField] private RideResultsView resultsView;
        [SerializeField] private MonoBehaviour scoreProviderBehaviour;

        private FlipTimer flipTimer;
        private IRideScoreProvider scoreProvider;
        private RideRecordStore recordStore;

        public RideSessionState State { get; private set; } = RideSessionState.Driving;
        public RideEndReason? EndReason { get; private set; }
        public float FlipElapsed => flipTimer?.Elapsed ?? 0f;

        private void Awake()
        {
            scoreProvider = scoreProviderBehaviour as IRideScoreProvider;
            recordStore = new RideRecordStore();
            if (rules != null) flipTimer = new FlipTimer(rules.flipAngle, rules.flipDuration);
            if (resultsView != null)
            {
                resultsView.SetVisible(false);
                resultsView.RestartRequested += RestartRide;
            }
        }

        private void OnDestroy()
        {
            if (resultsView != null) resultsView.RestartRequested -= RestartRide;
        }

        private void Update()
        {
            if (State != RideSessionState.Driving || player == null || flipTimer == null) return;
            var angle = Vector3.Angle(player.up, Vector3.up);
            if (flipTimer.Tick(angle, Time.deltaTime)) EndRide(RideEndReason.Flipped);
        }

        public void Configure(Transform playerTransform, VehicleController vehicleController, RideRulesProfile rideRules, RideResultsView view, MonoBehaviour optionalScoreProvider = null)
        {
            player = playerTransform;
            vehicle = vehicleController;
            rules = rideRules;
            resultsView = view;
            scoreProviderBehaviour = optionalScoreProvider;
            scoreProvider = optionalScoreProvider as IRideScoreProvider;
            flipTimer = rules != null ? new FlipTimer(rules.flipAngle, rules.flipDuration) : null;
        }

        public void EndRide(RideEndReason reason)
        {
            if (State == RideSessionState.Ended) return;
            State = RideSessionState.Ended;
            EndReason = reason;
            vehicle?.SetDrivingEnabled(false);
            if (vehicle?.Body != null)
            {
                vehicle.Body.linearVelocity = Vector3.zero;
                vehicle.Body.angularVelocity = Vector3.zero;
                vehicle.Body.isKinematic = true;
            }

            GameplayEvents.RaiseComboCancellation();

            int? score = null;
            int? best = null;
            if (scoreProvider != null)
            {
                score = scoreProvider.CurrentScore;
                recordStore.Record(score.Value);
                best = recordStore.BestScore;
            }

            resultsView?.Show(reason, score, best);
            GameplayEvents.RaiseRideEnded(reason);
        }

        public void RestartRide()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(scene.name);
            }
        }
    }
}
