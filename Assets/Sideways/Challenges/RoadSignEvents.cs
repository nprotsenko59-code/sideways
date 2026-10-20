using System;

namespace Sideways.Challenges
{
    public static class RoadSignEvents
    {
        public static event Action<string> SignDestroyed;

        internal static void RaiseDestroyed(string signId) => SignDestroyed?.Invoke(signId);

        public static void ClearAllListeners() => SignDestroyed = null;
    }
}
