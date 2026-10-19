using Sideways.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sideways.UI
{
    public sealed class DebugHud : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicle;
        [SerializeField] private VehicleRecovery recovery;
        [SerializeField] private Text text;
        [SerializeField] private GameObject panel;
        [SerializeField] private bool startVisible = true;

        private float smoothedDelta = 1f / 60f;
        private float nextRefresh;

        private void Awake()
        {
            if (panel != null) panel.SetActive(startVisible);
        }

        private void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.08f);
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame && panel != null)
            {
                panel.SetActive(!panel.activeSelf);
            }

            if (panel == null || !panel.activeSelf || text == null || vehicle == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            var telemetry = vehicle.Telemetry;
            text.text =
                $"SPEED  {telemetry.Speed * 3.6f,5:0.0} km/h\n" +
                $"STEER  {telemetry.Steering,5:0.00}\n" +
                $"SLIP   {telemetry.SlipAngle,5:0.0}°\n" +
                $"GROUND {(telemetry.Grounded ? "YES" : "NO ")}\n" +
                $"DRIFT  {(telemetry.Drifting ? "YES" : "NO ")}\n" +
                $"TILT   {telemetry.AngleFromUpright,5:0.0}°\n" +
                $"RESET  {(recovery != null && recovery.CanReset ? "READY" : "WAIT ")}\n" +
                $"FPS    {(smoothedDelta > 0f ? 1f / smoothedDelta : 0f),5:0}";
        }

        public void Configure(VehicleController controller, VehicleRecovery vehicleRecovery, Text label, GameObject rootPanel)
        {
            vehicle = controller;
            recovery = vehicleRecovery;
            text = label;
            panel = rootPanel;
        }
    }
}
