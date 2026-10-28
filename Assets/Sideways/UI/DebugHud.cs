using Sideways.Vehicle;
using Unity.Profiling;
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
        [SerializeField] private bool startVisible;

        private float smoothedDelta = 1f / 60f;
        private float nextRefresh;
        private ProfilerRecorder gcAllocatedRecorder;

        private void Awake()
        {
            if (panel != null) panel.SetActive(startVisible);
        }

        private void OnEnable()
        {
            gcAllocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        private void OnDisable()
        {
            gcAllocatedRecorder.Dispose();
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
                $"STEER  {telemetry.Steering,5:0.00}  {telemetry.SteeringAngleDegrees,5:0.0}°\n" +
                $"SLIP   {telemetry.SlipAngle,5:0.0}°  F {telemetry.FrontSlipAngle,5:0.0}°  R {telemetry.RearSlipAngle,5:0.0}°\n" +
                $"GRIP   F {telemetry.FrontGripUtilization,4:0.00}  R {telemetry.RearGripUtilization,4:0.00}\n" +
                $"DRIFT  {(telemetry.Drifting ? "YES" : "NO ")}  {telemetry.DriftIntensity,4:0.00}  HOLD {telemetry.ControlDriftStrength,4:0.00}\n" +
                $"GROUND {(telemetry.Grounded ? "YES" : "NO ")}\n" +
                $"BRAKE  {(telemetry.Brake ? "ON " : "OFF")}\n" +
                $"YAW    {telemetry.YawRateDegrees,5:0.0}°/s\n" +
                $"TILT   {telemetry.AngleFromUpright,5:0.0}°\n" +
                $"RESET  {(recovery != null && recovery.CanReset ? "READY" : "WAIT ")}\n" +
                $"FPS    {(smoothedDelta > 0f ? 1f / smoothedDelta : 0f),5:0}\n" +
                $"GC     {(gcAllocatedRecorder.Valid ? gcAllocatedRecorder.LastValue : 0),5} B";
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
