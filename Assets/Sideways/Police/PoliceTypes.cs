using UnityEngine;

namespace Sideways.Police
{
    public enum PoliceState
    {
        Patrol,
        Pursuit,
        Search,
        Escaped
    }

    public readonly struct PoliceStateTransition
    {
        public PoliceStateTransition(PoliceState previous, PoliceState current)
        {
            Previous = previous;
            Current = current;
        }

        public PoliceState Previous { get; }
        public PoliceState Current { get; }
    }

    public readonly struct PlayerSighting
    {
        public PlayerSighting(Vector3 position, float time)
        {
            Position = position;
            Time = time;
        }

        public Vector3 Position { get; }
        public float Time { get; }
    }

    public readonly struct PoliceStatusSnapshot
    {
        public PoliceStatusSnapshot(PoliceState state, bool hasVisual, bool hasLastSeen, Vector3 lastSeenPosition, float searchRemaining, int heatLevel = 0, int activeUnits = 1, bool roadblockDeployed = false)
        {
            State = state;
            HasVisual = hasVisual;
            HasLastSeen = hasLastSeen;
            LastSeenPosition = lastSeenPosition;
            SearchRemaining = searchRemaining;
            HeatLevel = heatLevel;
            ActiveUnits = activeUnits;
            RoadblockDeployed = roadblockDeployed;
        }

        public PoliceState State { get; }
        public bool HasVisual { get; }
        public bool HasLastSeen { get; }
        public Vector3 LastSeenPosition { get; }
        public float SearchRemaining { get; }
        public int HeatLevel { get; }
        public int ActiveUnits { get; }
        public bool RoadblockDeployed { get; }
    }
}
