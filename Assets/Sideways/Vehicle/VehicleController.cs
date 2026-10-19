using System;
using Sideways.Configuration;
using UnityEngine;

namespace Sideways.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleController : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private MonoBehaviour inputSourceBehaviour;
        [SerializeField] private VehicleHandlingProfile profile;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private Vector3 centerOfMass = new(0f, -0.35f, 0f);

        private IVehicleInputSource inputSource;
        private bool drivingEnabled = true;
        private bool drifting;
        private Vector3 groundNormal = Vector3.up;
        private VehicleTelemetry telemetry;

        public event Action<VehicleTelemetry> TelemetryUpdated;

        public VehicleTelemetry Telemetry => telemetry;
        public bool DrivingEnabled => drivingEnabled;
        public Rigidbody Body => body;

        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
            inputSource = inputSourceBehaviour as IVehicleInputSource;
            if (body != null) body.centerOfMass = centerOfMass;
        }

        private void FixedUpdate()
        {
            if (body == null || profile == null) return;

            var grounded = TryGetGround(out groundNormal);
            var steering = drivingEnabled && inputSource != null ? inputSource.Steering : 0f;

            if (drivingEnabled && grounded)
            {
                ApplyDriveForces(steering);
            }

            UpdateTelemetry(steering, grounded);
        }

        public void Configure(Rigidbody rigidbody, IVehicleInputSource source, MonoBehaviour sourceBehaviour, VehicleHandlingProfile handling, LayerMask roadMask, Vector3 massCenter)
        {
            body = rigidbody;
            inputSource = source;
            inputSourceBehaviour = sourceBehaviour;
            profile = handling;
            groundMask = roadMask;
            centerOfMass = massCenter;
            if (body != null) body.centerOfMass = centerOfMass;
        }

        public void SetDrivingEnabled(bool enabled) => drivingEnabled = enabled;

        private void ApplyDriveForces(float steering)
        {
            var forward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            var right = Vector3.Cross(groundNormal, forward).normalized;
            var planarVelocity = VehicleMath.GroundPlaneVelocity(body.linearVelocity, groundNormal);
            var forwardSpeed = Vector3.Dot(planarVelocity, forward);
            var lateralSpeed = Vector3.Dot(planarVelocity, right);
            var speed = planarVelocity.magnitude;

            if (forwardSpeed < profile.maxSpeed)
            {
                body.AddForce(forward * profile.acceleration, ForceMode.Acceleration);
            }
            else
            {
                body.AddForce(-forward * Mathf.Min(forwardSpeed - profile.maxSpeed, profile.overspeedBraking), ForceMode.Acceleration);
            }

            var speedRatio = Mathf.Clamp01(speed / profile.maxSpeed);
            var steeringScale = Mathf.Lerp(1f, profile.minimumSteeringAtMaxSpeed, speedRatio);
            body.AddTorque(groundNormal * (steering * profile.steeringAcceleration * steeringScale), ForceMode.Acceleration);
            var yawSpeed = Vector3.Dot(body.angularVelocity, groundNormal);
            body.AddTorque(-groundNormal * yawSpeed * profile.yawDamping, ForceMode.Acceleration);

            var driftDemand = speed >= profile.driftMinimumSpeed && Mathf.Abs(steering) >= profile.driftInputThreshold;
            var grip = driftDemand || drifting ? profile.driftLateralGrip : profile.normalLateralGrip;
            body.AddForce(-right * lateralSpeed * grip, ForceMode.Acceleration);
            body.AddForce(-groundNormal * profile.downforce * speedRatio, ForceMode.Acceleration);
        }

        private bool TryGetGround(out Vector3 normal)
        {
            var origin = transform.position + transform.up * profile.groundProbeHeight;
            if (Physics.SphereCast(origin, profile.groundProbeRadius, -transform.up, out var hit, profile.groundProbeLength, groundMask, QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return true;
            }

            normal = Vector3.up;
            return false;
        }

        private void UpdateTelemetry(float steering, bool grounded)
        {
            var planarVelocity = VehicleMath.GroundPlaneVelocity(body.linearVelocity, grounded ? groundNormal : Vector3.up);
            var slip = VehicleMath.SlipAngleDegrees(transform.forward, planarVelocity, grounded ? groundNormal : Vector3.up);
            var absSlip = Mathf.Abs(slip);
            if (!drifting && grounded && planarVelocity.magnitude >= profile.driftMinimumSpeed && absSlip >= profile.driftEnterAngle) drifting = true;
            if (drifting && (!grounded || planarVelocity.magnitude < profile.driftMinimumSpeed || absSlip <= profile.driftExitAngle)) drifting = false;

            telemetry = new VehicleTelemetry(
                planarVelocity.magnitude,
                steering,
                planarVelocity,
                slip,
                grounded,
                drifting,
                Vector3.Angle(transform.up, Vector3.up));
            TelemetryUpdated?.Invoke(telemetry);
        }
    }
}
