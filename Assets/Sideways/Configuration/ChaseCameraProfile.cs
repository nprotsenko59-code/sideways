using UnityEngine;

namespace Sideways.Configuration
{
    [CreateAssetMenu(menuName = "Sideways/Chase Camera Profile", fileName = "ChaseCameraProfile")]
    public sealed class ChaseCameraProfile : ScriptableObject
    {
        [Min(1f)] public float distance = 10f;
        [Min(0f)] public float height = 7f;
        [Min(0f)] public float positionDamping = 7f;
        [Min(0f)] public float rotationDamping = 9f;
        [Min(0f)] public float lookAheadDistance = 3f;
        [Min(0f)] public float lookHeight = 1f;
        [Range(20f, 100f)] public float fieldOfView = 62f;
        [Min(0.05f)] public float obstructionRadius = 0.35f;
        [Min(0f)] public float obstructionPadding = 0.25f;

        [Header("Overhead experiment")]
        [Min(5f)] public float overheadHeight = 28f;
        [Min(0f)] public float overheadBackOffset = 10f;
        [Min(0f)] public float overheadLookAheadDistance = 5f;
        [Min(0f)] public float overheadDamping = 6f;
        [Min(0f)] public float overheadHeadingDamping = 2.4f;
        [Min(0f)] public float overheadRotationDamping = 5f;
        [Range(20f, 100f)] public float overheadFieldOfView = 55f;
    }
}
