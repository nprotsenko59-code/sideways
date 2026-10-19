using System;
using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Police
{
    public sealed class PoliceVisionSensor : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private PoliceProfile profile;
        [SerializeField] private LayerMask obstructionMask;
        [SerializeField] private Vector3 eyeOffset = new(0f, 1.1f, 0.8f);
        [SerializeField] private Vector3 targetOffset = new(0f, 0.5f, 0f);

        private float nextScanTime;

        public event Action<PlayerSighting> SightingReported;
        public event Action<bool> VisibilityChanged;

        public bool HasVisual { get; private set; }

        private void Update()
        {
            if (profile == null || Time.time < nextScanTime) return;
            nextScanTime = Time.time + profile.scanInterval;
            ScanNow();
        }

        public void Configure(Transform player, PoliceProfile policeProfile, LayerMask obstacles)
        {
            target = player;
            profile = policeProfile;
            obstructionMask = obstacles;
        }

        public bool ScanNow()
        {
            var visible = false;
            if (target != null && profile != null)
            {
                var origin = transform.TransformPoint(eyeOffset);
                var targetPoint = target.TransformPoint(targetOffset);
                visible = PoliceMath.IsWithinVision(origin, transform.forward, targetPoint, profile.visionRange, profile.visionAngle)
                    && !Physics.Linecast(origin, targetPoint, obstructionMask, QueryTriggerInteraction.Ignore);
            }

            if (visible != HasVisual)
            {
                HasVisual = visible;
                VisibilityChanged?.Invoke(visible);
            }

            if (visible) SightingReported?.Invoke(new PlayerSighting(target.position, Time.time));
            return visible;
        }

        private void OnDrawGizmosSelected()
        {
            if (profile == null) return;
            Gizmos.color = HasVisual ? Color.red : Color.yellow;
            var origin = transform.TransformPoint(eyeOffset);
            Gizmos.DrawWireSphere(origin, profile.visionRange);
            Gizmos.DrawRay(origin, Quaternion.Euler(0f, -profile.visionAngle * 0.5f, 0f) * transform.forward * profile.visionRange);
            Gizmos.DrawRay(origin, Quaternion.Euler(0f, profile.visionAngle * 0.5f, 0f) * transform.forward * profile.visionRange);
        }
    }
}
