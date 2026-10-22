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
        [SerializeField] private CameraViewMode viewMode;

        private UnityEngine.Camera cameraComponent;
        private bool reducedMotion;

        public event Action<CameraViewMode> ViewModeChanged;

        public CameraViewMode ViewMode => viewMode;

        private void Awake()
        {
            cameraComponent = GetComponent<UnityEngine.Camera>();
            if (profile != null) cameraComponent.fieldOfView = profile.fieldOfView;
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
            var baseLookAhead = overhead ? profile.overheadLookAheadDistance : profile.lookAheadDistance;
            var lookAheadDistance = baseLookAhead * (reducedMotion ? 0.25f : 1f);
            var lookAhead = velocity.sqrMagnitude > 0.1f ? velocity.normalized * lookAheadDistance : flatForward * lookAheadDistance;
            var lookPoint = target.position + Vector3.up * profile.lookHeight + lookAhead;
            var desired = overhead
                ? target.position - flatForward * profile.overheadBackOffset + Vector3.up * profile.overheadHeight
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
            var rotationT = 1f - Mathf.Exp(-profile.rotationDamping * dampingMultiplier * Time.unscaledDeltaTime);
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
            ViewModeChanged?.Invoke(viewMode);
        }
    }
}
