using System;
using System.Collections.Generic;
using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Challenges
{
    public sealed class ChallengeModel
    {
        private readonly HashSet<string> countedSignIds = new(StringComparer.Ordinal);
        private ChallengeDefinition definition;
        private int progress;
        private float timeRemaining;

        public ChallengeSnapshot Snapshot => definition == null
            ? new ChallengeSnapshot(ChallengeState.Idle, string.Empty, string.Empty, default, 0, 0, 0f, 0)
            : new ChallengeSnapshot(State, definition.challengeId, definition.displayName, definition.type, progress, definition.target, timeRemaining, definition.reward);

        public ChallengeState State { get; private set; } = ChallengeState.Idle;

        public void Start(ChallengeDefinition challenge)
        {
            definition = challenge != null ? challenge : throw new ArgumentNullException(nameof(challenge));
            progress = 0;
            timeRemaining = challenge.duration;
            countedSignIds.Clear();
            State = ChallengeState.Active;
        }

        public ChallengeSnapshot Tick(float deltaTime)
        {
            if (State != ChallengeState.Active) return Snapshot;
            timeRemaining = Mathf.Max(0f, timeRemaining - Mathf.Max(0f, deltaTime));
            if (timeRemaining <= 0f) State = ChallengeState.Failed;
            return Snapshot;
        }

        public bool RecordRoadSign(string signId)
        {
            if (State != ChallengeState.Active || definition.type != ChallengeType.DestroyRoadSigns || string.IsNullOrWhiteSpace(signId)) return false;
            if (!countedSignIds.Add(signId)) return false;
            AddProgress(1);
            return true;
        }

        public bool RecordDriftScore(int points)
        {
            if (State != ChallengeState.Active || definition.type != ChallengeType.EarnDriftScore || points <= 0) return false;
            AddProgress(points);
            return true;
        }

        public void Cancel()
        {
            if (State == ChallengeState.Active) State = ChallengeState.Cancelled;
        }

        private void AddProgress(int amount)
        {
            progress = Mathf.Min(definition.target, progress + amount);
            if (progress >= definition.target) State = ChallengeState.Succeeded;
        }
    }
}
