using UnityEngine;

namespace Sideways.Core
{
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [SerializeField, Range(30, 120)] private int targetFrameRate = 60;
        [SerializeField, Range(30, 120)] private int physicsRate = 60;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            Time.fixedDeltaTime = 1f / physicsRate;
        }
    }
}
