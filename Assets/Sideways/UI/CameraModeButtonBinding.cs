using Sideways.CameraSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Sideways.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class CameraModeButtonBinding : MonoBehaviour
    {
        [SerializeField] private ChaseCameraController cameraController;
        [SerializeField] private Text label;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Toggle);
            Refresh(cameraController != null ? cameraController.ViewMode : CameraViewMode.Overhead);
        }

        private void OnEnable()
        {
            if (cameraController != null) cameraController.ViewModeChanged += Refresh;
        }

        private void OnDisable()
        {
            if (cameraController != null) cameraController.ViewModeChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Toggle);
        }

        public void Configure(ChaseCameraController controller, Text buttonLabel)
        {
            if (isActiveAndEnabled && cameraController != null) cameraController.ViewModeChanged -= Refresh;
            cameraController = controller;
            label = buttonLabel;
            if (isActiveAndEnabled && cameraController != null) cameraController.ViewModeChanged += Refresh;
            Refresh(cameraController != null ? cameraController.ViewMode : CameraViewMode.Overhead);
        }

        private void Toggle() => cameraController?.ToggleViewMode();

        private void Refresh(CameraViewMode mode)
        {
            if (label != null) label.text = mode == CameraViewMode.Overhead ? "CAMERA: TOP  [V]" : "CAMERA: CHASE  [V]";
        }
    }
}
