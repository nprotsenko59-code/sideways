using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sideways.Vehicle
{
    public sealed class VehicleInputSource : MonoBehaviour, IVehicleInputSource
    {
        [SerializeField, Min(0.1f)] private float response = 4.5f;
        [SerializeField, Min(0f)] private float touchHandbrakeReleasePulse = 0.25f;

        private bool leftHeld;
        private bool rightHeld;
        private bool resetRequested;
        private float steering;
        private float touchHandbrakeUntil;
        private readonly HashSet<int> touchHandbrakePointers = new();

        public float Steering => steering;
        public bool Handbrake =>
            (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
            touchHandbrakePointers.Count > 0 ||
            Time.unscaledTime < touchHandbrakeUntil;

        private void Update()
        {
            var keyboardTarget = ReadKeyboardTarget();
            var touchTarget = leftHeld == rightHeld ? 0f : leftHeld ? -1f : 1f;
            var target = Mathf.Abs(keyboardTarget) > 0.01f ? keyboardTarget : touchTarget;
            steering = VehicleMath.MoveSteering(steering, target, response, Time.unscaledDeltaTime);

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                resetRequested = true;
            }
        }

        public void Configure(float inputResponse) => response = Mathf.Max(0.1f, inputResponse);

        public void SetTouchSteering(int direction, bool held)
        {
            if (direction < 0) leftHeld = held;
            if (direction > 0) rightHeld = held;
        }

        public void SetTouchHandbrake(int pointerId, bool held)
        {
            if (held)
            {
                touchHandbrakePointers.Add(pointerId);
                touchHandbrakeUntil = 0f;
                return;
            }

            if (touchHandbrakePointers.Remove(pointerId))
            {
                touchHandbrakeUntil = Time.unscaledTime + touchHandbrakeReleasePulse;
            }
        }

        public void RequestReset() => resetRequested = true;

        public bool ConsumeResetRequest()
        {
            var value = resetRequested;
            resetRequested = false;
            return value;
        }

        private void OnDisable()
        {
            leftHeld = false;
            rightHeld = false;
            touchHandbrakePointers.Clear();
            touchHandbrakeUntil = 0f;
        }

        private static float ReadKeyboardTarget()
        {
            if (Keyboard.current == null) return 0f;
            var left = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            var right = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
            if (left == right) return 0f;
            return left ? -1f : 1f;
        }
    }
}
