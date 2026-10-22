using System;
using Sideways.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sideways.CameraSystem
{
    public enum CameraViewMode
    {
        Chase,
        Overhead
    }

    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class ChaseCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private ChaseCameraProfile profile;
        [SerializeField] private LayerMask obstructionMask;
        [SerializeField] private CameraViewMode viewMode = CameraViewMode.Overhead;

        private UnityEngine.Camera cameraComponent;
        private bool reducedMotion;
        private Vector3 smoothedOverheadForward;

        public event Action<CameraViewMode> ViewModeChanged;

        public CameraViewMode ViewMode => viewMode;

        private void Awake()
        {
            cameraComponent = GetComponent<UnityEngine.Camera>();
            smoothedOverheadForward = target != null ? Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized : Vector3.forward;
            if (profile != null) cameraComponent.fieldOfView = viewMode == CameraViewMode.Overhead ? profile.overheadFieldOfView : profile.fieldOfView;
        }

        private void Update()
        {
            if (Keyboard.current?.vKey.wasPressedThisFrame == true) ToggleViewMode();
        }

        private void LateUpdate()
        {
            if (target == null || profile == null) return;
            if (cameraComponent == null) cameraComponent = GetComponent<UnityEngine.Camera>();

            var flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            var velocity = targetBody != null ? Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up) : Vector3.zero;
            var overhead = viewMode == CameraViewMode.Overhead;
            if (smoothedOverheadForward.sqrMagnitude < 0.001f) smoothedOverheadForward = flatForward;
            if (overhead)
            {
                var headingT = 1f - Mathf.Exp(-profile.overheadHeadingDamping * Time.unscaledDeltaTime);
                smoothedOverheadForward = Vector3.Slerp(smoothedOverheadForward, flatForward, headingT).normalized;
            }
            var framingForward = overhead ? smoothedOverheadForward : flatForward;
            var baseLookAhead = overhead ? profile.overheadLookAheadDistance : profile.lookAheadDistance;
            var lookAheadDistance = baseLookAhead * (reducedMotion ? 0.25f : 1f);
            var lookAhead = velocity.sqrMagnitude > 0.1f ? velocity.normalized * lookAheadDistance : framingForward * lookAheadDistance;
            var lookPoint = target.position + Vector3.up * profile.lookHeight + lookAhead;
            var desired = overhead
                ? target.position - framingForward * profile.overheadBackOffset + Vector3.up * profile.overheadHeight
                : target.position - flatForward * profile.distance + Vector3.up * profile.height;

            var castDirection = desired - lookPoint;
            var castDistance = castDirection.magnitude;
            if (!overhead && castDistance > 0.01f && Physics.SphereCast(lookPoint, profile.obstructionRadius, castDirection.normalized, out var hit, castDistance, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                desired = lookPoint + castDirection.normalized * Mathf.Max(0.5f, hit.distance - profile.obstructionPadding);
            }

            var dampingMultiplier = reducedMotion ? 2.5f : 1f;
            var positionDamping = overhead ? profile.overheadDamping : profile.positionDamping;
            var positionT = 1f - Mathf.Exp(-positionDamping * dampingMultiplier * Time.unscaledDeltaTime);
            var rotationDamping = overhead ? profile.overheadRotationDamping : profile.rotationDamping;
            var rotationT = 1f - Mathf.Exp(-rotationDamping * dampingMultiplier * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, positionT);
            var desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
            cameraComponent.fieldOfView = overhead ? profile.overheadFieldOfView : profile.fieldOfView;
        }

        public void Configure(Transform followTarget, Rigidbody rigidbody, ChaseCameraProfile cameraProfile, LayerMask obstacles)
        {
            target = followTarget;
            targetBody = rigidbody;
            profile = cameraProfile;
            obstructionMask = obstacles;
        }

        public void SetReducedMotion(bool enabled) => reducedMotion = enabled;

        public void ToggleViewMode()
        {
            viewMode = viewMode == CameraViewMode.Chase ? CameraViewMode.Overhead : CameraViewMode.Chase;
            if (viewMode == CameraViewMode.Overhead && target != null)
            {
                var flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
                if (flatForward.sqrMagnitude > 0.001f) smoothedOverheadForward = flatForward;
            }
            ViewModeChanged?.Invoke(viewMode);
        }
    }
}
