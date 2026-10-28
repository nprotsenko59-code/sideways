using Sideways.Configuration;
using Sideways.Core;
using UnityEngine;

namespace Sideways.Vehicle
{
    public sealed class VehicleRecovery : MonoBehaviour
    {
        [SerializeField] private VehicleController controller;
        [SerializeField] private VehicleInputSource inputSource;
        [SerializeField] private RideRulesProfile rules;
        [SerializeField] private LayerMask roadMask;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private Vector3 clearanceHalfExtents = new(1.2f, 0.6f, 2.2f);

        private Vector3 safePosition;
        private Quaternion safeRotation;
        private Vector3 safeNormal = Vector3.up;
        private bool hasSafePose;
        private float nextSafePoseTime;

        public bool CanReset => hasSafePose;

        private void FixedUpdate()
        {
            if (controller == null || rules == null) return;

            if (Time.fixedTime >= nextSafePoseTime && IsSafePose(out var surfaceNormal))
            {
                safePosition = transform.position;
                safeNormal = surfaceNormal;
                var forward = Vector3.ProjectOnPlane(transform.forward, safeNormal).normalized;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(Vector3.forward, safeNormal).normalized;
                safeRotation = Quaternion.LookRotation(forward, safeNormal);
                hasSafePose = true;
                nextSafePoseTime = Time.fixedTime + rules.safePoseInterval;
            }

            if (controller.DrivingEnabled && inputSource != null && inputSource.ConsumeResetRequest())
            {
                ResetToRoad();
            }
        }

        public void Configure(VehicleController vehicleController, VehicleInputSource source, RideRulesProfile rideRules, LayerMask roads, LayerMask obstacles)
        {
            controller = vehicleController;
            inputSource = source;
            rules = rideRules;
            roadMask = roads;
            obstacleMask = obstacles;
        }

        public bool ResetToRoad()
        {
            if (!hasSafePose || controller == null || rules == null) return false;
            var body = controller.Body;
            body.position = safePosition + safeNormal * rules.resetVerticalOffset;
            body.rotation = safeRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
            GameplayEvents.RaiseRoadReset();
            return true;
        }

        internal void SetSafePoseForTests(Vector3 position, Quaternion rotation)
        {
            safePosition = position;
            safeRotation = rotation;
            safeNormal = rotation * Vector3.up;
            hasSafePose = true;
        }

        private bool IsSafePose(out Vector3 surfaceNormal)
        {
            surfaceNormal = Vector3.up;
            var rayOrigin = transform.position + transform.up * 0.5f;
            // Roads are trigger volumes so their overlapping segment seams cannot
            // physically block the vehicle. They still identify valid recovery areas.
            var roadBelow = Physics.Raycast(rayOrigin, -transform.up, out var roadHit, 2.5f, roadMask, QueryTriggerInteraction.Collide);
            if (roadBelow) surfaceNormal = roadHit.normal;
            var obstacleFree = !Physics.CheckBox(transform.position, clearanceHalfExtents + Vector3.one * rules.safePoseClearance, transform.rotation, obstacleMask, QueryTriggerInteraction.Ignore);
            return SafePoseRules.IsEligible(
                controller.Telemetry.Grounded,
                Vector3.Angle(transform.up, Vector3.up),
                rules.safePoseMaximumTilt,
                roadBelow,
                obstacleFree);
        }
    }
}
