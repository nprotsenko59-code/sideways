using Sideways.Configuration;
using UnityEngine;

namespace Sideways.CameraSystem
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class ChaseCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private ChaseCameraProfile profile;
        [SerializeField] private LayerMask obstructionMask;

        private UnityEngine.Camera cameraComponent;
        private bool reducedMotion;

        private void Awake()
        {
            cameraComponent = GetComponent<UnityEngine.Camera>();
            if (profile != null) cameraComponent.fieldOfView = profile.fieldOfView;
        }

        private void LateUpdate()
        {
            if (target == null || profile == null) return;
            if (cameraComponent == null) cameraComponent = GetComponent<UnityEngine.Camera>();

            var flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            var velocity = targetBody != null ? Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up) : Vector3.zero;
            var lookAheadDistance = profile.lookAheadDistance * (reducedMotion ? 0.25f : 1f);
            var lookAhead = velocity.sqrMagnitude > 0.1f ? velocity.normalized * lookAheadDistance : flatForward * lookAheadDistance;
            var lookPoint = target.position + Vector3.up * profile.lookHeight + lookAhead;
            var desired = target.position - flatForward * profile.distance + Vector3.up * profile.height;

            var castDirection = desired - lookPoint;
            var castDistance = castDirection.magnitude;
            if (castDistance > 0.01f && Physics.SphereCast(lookPoint, profile.obstructionRadius, castDirection.normalized, out var hit, castDistance, obstructionMask, QueryTriggerInteraction.Ignore))
            {
                desired = lookPoint + castDirection.normalized * Mathf.Max(0.5f, hit.distance - profile.obstructionPadding);
            }

            var dampingMultiplier = reducedMotion ? 2.5f : 1f;
            var positionT = 1f - Mathf.Exp(-profile.positionDamping * dampingMultiplier * Time.unscaledDeltaTime);
            var rotationT = 1f - Mathf.Exp(-profile.rotationDamping * dampingMultiplier * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, positionT);
            var desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
            cameraComponent.fieldOfView = profile.fieldOfView;
        }

        public void Configure(Transform followTarget, Rigidbody rigidbody, ChaseCameraProfile cameraProfile, LayerMask obstacles)
        {
            target = followTarget;
            targetBody = rigidbody;
            profile = cameraProfile;
            obstructionMask = obstacles;
        }

        public void SetReducedMotion(bool enabled) => reducedMotion = enabled;
    }
}
