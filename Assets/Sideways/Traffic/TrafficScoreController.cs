using Sideways.Core;
using UnityEngine;

namespace Sideways.Traffic
{
    public sealed class TrafficScoreController : MonoBehaviour
    {
        [SerializeField] private DriftScoreController scoring;

        public NearMissEvent LastNearMiss { get; private set; }

        private void OnEnable() => TrafficEvents.NearMiss += HandleNearMiss;

        private void OnDisable() => TrafficEvents.NearMiss -= HandleNearMiss;

        public void Configure(DriftScoreController scoreController) => scoring = scoreController;

        private void HandleNearMiss(NearMissEvent nearMiss)
        {
            LastNearMiss = nearMiss;
            scoring?.AwardBonus(nearMiss.Points, "Near miss");
        }
    }
}
