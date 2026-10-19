using System;

namespace Sideways.Core
{
    public static class GameplayEvents
    {
        public static event Action RoadReset;
        public static event Action ComboCancellationRequested;
        public static event Action<RideEndReason> RideEnded;

        internal static void RaiseRoadReset()
        {
            RoadReset?.Invoke();
            ComboCancellationRequested?.Invoke();
        }

        internal static void RaiseRideEnded(RideEndReason reason) => RideEnded?.Invoke(reason);

        public static void ClearAllListeners()
        {
            RoadReset = null;
            ComboCancellationRequested = null;
            RideEnded = null;
        }
    }
}
