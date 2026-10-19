using Sideways.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace Sideways.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class ResetButtonBinding : MonoBehaviour
    {
        [SerializeField] private VehicleInputSource inputSource;
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(RequestReset);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(RequestReset);
        }

        public void Configure(VehicleInputSource source) => inputSource = source;

        private void RequestReset() => inputSource?.RequestReset();
    }
}
