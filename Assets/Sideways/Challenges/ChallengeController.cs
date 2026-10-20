using System;
using Sideways.Configuration;
using Sideways.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sideways.Challenges
{
    public sealed class ChallengeController : MonoBehaviour
    {
        [SerializeField] private ChallengeDefinition[] challenges;
        [SerializeField] private DriftScoreController scoring;

        private readonly ChallengeModel model = new();
        private int selectedIndex;
        private ChallengeState previousState;

        public event Action<ChallengeSnapshot> StatusUpdated;
        public event Action<ChallengeReward> RewardEarned;

        public ChallengeSnapshot Snapshot => model.Snapshot;

        private void OnEnable()
        {
            RoadSignEvents.SignDestroyed += HandleSignDestroyed;
            if (scoring != null) scoring.DriftPointsEarned += HandleDriftPoints;
        }

        private void OnDisable()
        {
            RoadSignEvents.SignDestroyed -= HandleSignDestroyed;
            if (scoring != null) scoring.DriftPointsEarned -= HandleDriftPoints;
        }

        private void Update()
        {
            if (Keyboard.current?.cKey.wasPressedThisFrame == true) ActivateOrRetry();
            if (model.State == ChallengeState.Active)
            {
                model.Tick(Time.deltaTime);
                PublishAndHandleTerminal();
            }
        }

        public void Configure(ChallengeDefinition[] definitions, DriftScoreController scoreController)
        {
            if (isActiveAndEnabled && scoring != null) scoring.DriftPointsEarned -= HandleDriftPoints;
            challenges = definitions;
            scoring = scoreController;
            if (isActiveAndEnabled && scoring != null) scoring.DriftPointsEarned += HandleDriftPoints;
            Publish();
        }

        public void ActivateOrRetry()
        {
            if (challenges == null || challenges.Length == 0) return;
            if (model.State == ChallengeState.Active)
            {
                model.Cancel();
                PublishAndHandleTerminal();
                return;
            }

            if (model.State == ChallengeState.Succeeded) selectedIndex = (selectedIndex + 1) % challenges.Length;
            model.Start(challenges[selectedIndex]);
            previousState = ChallengeState.Active;
            Publish();
        }

        public void StartChallenge(int index)
        {
            if (challenges == null || index < 0 || index >= challenges.Length) return;
            selectedIndex = index;
            model.Start(challenges[selectedIndex]);
            previousState = ChallengeState.Active;
            Publish();
        }

        private void HandleSignDestroyed(string signId)
        {
            if (model.RecordRoadSign(signId)) PublishAndHandleTerminal();
        }

        private void HandleDriftPoints(int points)
        {
            if (model.RecordDriftScore(points)) PublishAndHandleTerminal();
        }

        private void PublishAndHandleTerminal()
        {
            var state = model.State;
            if (state == ChallengeState.Succeeded && previousState == ChallengeState.Active)
            {
                var snapshot = model.Snapshot;
                RewardEarned?.Invoke(new ChallengeReward(snapshot.ChallengeId, snapshot.Reward));
                scoring?.AwardBonus(snapshot.Reward, $"Challenge: {snapshot.DisplayName}");
            }
            previousState = state;
            Publish();
        }

        private void Publish() => StatusUpdated?.Invoke(model.Snapshot);
    }
}
