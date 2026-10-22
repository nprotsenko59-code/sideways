using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Police
{
    public sealed class PolicePursuitModel
    {
        private readonly PoliceProfile profile;
        private float lostSightElapsed;
        private float searchElapsed;
        private float searchStateElapsed;
        private float escapedElapsed;
        private float bustedElapsed;
        private float timeSinceVisual;

        public PolicePursuitModel(PoliceProfile policeProfile)
        {
            profile = policeProfile;
        }

        public PoliceState State { get; private set; } = PoliceState.Patrol;
        public bool HasLastSeen { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public Vector3 LastSeenVelocity { get; private set; }
        public Vector3 EstimatedPlayerPosition => LastSeenPosition + LastSeenVelocity * Mathf.Min(timeSinceVisual, profile.predictionHorizon);
        public float SearchRemaining => State == PoliceState.Search ? Mathf.Max(0f, profile.searchDuration - searchElapsed) : 0f;
        public int SearchWaypointIndex => State == PoliceState.Search ? Mathf.FloorToInt(searchStateElapsed / profile.searchWaypointInterval) : 0;

        public PoliceModelResult Tick(bool hasVisual, bool recklessDriving, Vector3 observedPlayerPosition, float playerSpeed, float policeDistance, float deltaTime)
        {
            return Tick(hasVisual, recklessDriving, observedPlayerPosition, Vector3.zero, playerSpeed, policeDistance, deltaTime);
        }

        public PoliceModelResult Tick(bool hasVisual, bool recklessDriving, Vector3 observedPlayerPosition, Vector3 observedPlayerVelocity, float playerSpeed, float policeDistance, float deltaTime)
        {
            var previous = State;
            var busted = false;
            deltaTime = Mathf.Max(0f, deltaTime);

            if (hasVisual)
            {
                HasLastSeen = true;
                LastSeenPosition = observedPlayerPosition;
                LastSeenVelocity = Vector3.ProjectOnPlane(observedPlayerVelocity, Vector3.up);
                timeSinceVisual = 0f;
            }
            else if (HasLastSeen && (State == PoliceState.Pursuit || State == PoliceState.Search))
            {
                timeSinceVisual += deltaTime;
            }

            switch (State)
            {
                case PoliceState.Patrol:
                    if (hasVisual && recklessDriving)
                    {
                        State = PoliceState.Pursuit;
                        lostSightElapsed = 0f;
                    }
                    break;
                case PoliceState.Pursuit:
                    if (hasVisual)
                    {
                        lostSightElapsed = 0f;
                    }
                    else
                    {
                        lostSightElapsed += deltaTime;
                        if (lostSightElapsed >= profile.lostSightDelay)
                        {
                            State = PoliceState.Search;
                            searchElapsed = 0f;
                            searchStateElapsed = 0f;
                            bustedElapsed = 0f;
                        }
                    }
                    break;
                case PoliceState.Search:
                    searchStateElapsed += deltaTime;
                    if (hasVisual)
                    {
                        State = PoliceState.Pursuit;
                        lostSightElapsed = 0f;
                        searchElapsed = 0f;
                        searchStateElapsed = 0f;
                    }
                    else
                    {
                        var escapeEligible = playerSpeed >= profile.minimumEscapeSpeed && policeDistance >= profile.minimumEscapeDistance;
                        searchElapsed = escapeEligible
                            ? searchElapsed + deltaTime
                            : Mathf.Max(0f, searchElapsed - deltaTime * profile.escapeProgressDecay);
                        if (searchElapsed >= profile.searchDuration)
                        {
                            State = PoliceState.Escaped;
                            escapedElapsed = 0f;
                        }
                    }
                    break;
                case PoliceState.Escaped:
                    escapedElapsed += deltaTime;
                    if (escapedElapsed >= profile.escapedDisplayDuration)
                    {
                        State = PoliceState.Patrol;
                        HasLastSeen = false;
                        LastSeenVelocity = Vector3.zero;
                        timeSinceVisual = 0f;
                    }
                    break;
            }

            if (State == PoliceState.Pursuit && hasVisual && policeDistance <= profile.bustedDistance && playerSpeed <= profile.bustedMaximumPlayerSpeed)
            {
                bustedElapsed += deltaTime;
                if (bustedElapsed >= profile.bustedHoldDuration)
                {
                    busted = true;
                    State = PoliceState.Patrol;
                    HasLastSeen = false;
                    LastSeenVelocity = Vector3.zero;
                    timeSinceVisual = 0f;
                    bustedElapsed = 0f;
                }
            }
            else
            {
                bustedElapsed = 0f;
            }

            return new PoliceModelResult(previous, State, busted, SearchRemaining);
        }
    }

    public readonly struct PoliceModelResult
    {
        public PoliceModelResult(PoliceState previousState, PoliceState state, bool busted, float searchRemaining)
        {
            PreviousState = previousState;
            State = state;
            Busted = busted;
            SearchRemaining = searchRemaining;
        }

        public PoliceState PreviousState { get; }
        public PoliceState State { get; }
        public bool Busted { get; }
        public float SearchRemaining { get; }
        public bool StateChanged => PreviousState != State;
    }
}
