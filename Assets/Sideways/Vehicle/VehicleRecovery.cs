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
        private bool hasSafePose;
        private float nextSafePoseTime;

        public bool CanReset => hasSafePose;

        private void FixedUpdate()
        {
            if (controller == null || rules == null) return;

            if (Time.fixedTime >= nextSafePoseTime && IsSafePose())
            {
                safePosition = transform.position;
                safeRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized, Vector3.up);
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
            body.position = safePosition + Vector3.up * rules.resetVerticalOffset;
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
            hasSafePose = true;
        }

        private bool IsSafePose()
        {
            var rayOrigin = transform.position + Vector3.up * 0.5f;
            // Roads are trigger volumes so their overlapping segment seams cannot
            // physically block the vehicle. They still identify valid recovery areas.
            var roadBelow = Physics.Raycast(rayOrigin, Vector3.down, 2f, roadMask, QueryTriggerInteraction.Collide);
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
