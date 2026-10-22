using System.Collections.Generic;
using UnityEngine;

namespace Sideways.Traffic
{
    public static class TrafficJunctionCoordinator
    {
        private static readonly Dictionary<string, int> Claims = new();

        public static bool TryClaim(string junctionId, int vehicleId)
        {
            if (string.IsNullOrEmpty(junctionId)) return true;
            if (!Claims.TryGetValue(junctionId, out var owner) || owner == vehicleId)
            {
                Claims[junctionId] = vehicleId;
                return true;
            }
            return false;
        }

        public static void Release(string junctionId, int vehicleId)
        {
            if (!string.IsNullOrEmpty(junctionId) && Claims.TryGetValue(junctionId, out var owner) && owner == vehicleId) Claims.Remove(junctionId);
        }

        public static void Clear() => Claims.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
