using UnityEngine;
using UnityEngine.InputSystem;

namespace Sideways.Vehicle
{
    public sealed class VehicleInputSource : MonoBehaviour, IVehicleInputSource
    {
        [SerializeField, Min(0.1f)] private float response = 4.5f;
        [SerializeField, Range(0.1f, 1f)] private float initialSteeringStrength = 0.38f;
        [SerializeField, Min(0.1f)] private float fullDriftHoldDuration = 1.1f;

        private bool leftHeld;
        private bool rightHeld;
        private bool touchBrakeHeld;
        private bool resetRequested;
        private float steering;
        private float steeringHoldDuration;
        private int lastDirection;

        public float Steering => steering;
        public float DriftStrength { get; private set; }
        public bool Brake => touchBrakeHeld || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);

        private void Update()
        {
            var keyboardDirection = ReadKeyboardDirection();
            var touchDirection = leftHeld == rightHeld ? 0 : leftHeld ? -1 : 1;
            var direction = keyboardDirection != 0 ? keyboardDirection : touchDirection;
            if (direction == 0)
            {
                steeringHoldDuration = 0f;
                lastDirection = 0;
                DriftStrength = Mathf.MoveTowards(DriftStrength, 0f, Time.unscaledDeltaTime * 5f);
            }
            else
            {
                if (lastDirection != 0 && direction != lastDirection)
                {
                    steeringHoldDuration = 0f;
                }

                lastDirection = direction;
                steeringHoldDuration += Time.unscaledDeltaTime;
                DriftStrength = VehicleMath.HoldDriftStrength(steeringHoldDuration, fullDriftHoldDuration);
            }

            var steeringMagnitude = direction == 0 ? 0f : Mathf.Lerp(initialSteeringStrength, 1f, DriftStrength);
            var target = direction * steeringMagnitude;
            steering = VehicleMath.MoveSteering(steering, target, response, Time.unscaledDeltaTime);

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                resetRequested = true;
            }
        }

        public void Configure(float inputResponse, float initialStrength = 0.38f, float holdDuration = 1.1f)
        {
            response = Mathf.Max(0.1f, inputResponse);
            initialSteeringStrength = Mathf.Clamp(initialStrength, 0.1f, 1f);
            fullDriftHoldDuration = Mathf.Max(0.1f, holdDuration);
        }

        public void SetTouchSteering(int direction, bool held)
        {
            if (direction < 0) leftHeld = held;
            if (direction > 0) rightHeld = held;
        }

        public void SetTouchBrake(bool held) => touchBrakeHeld = held;

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
            touchBrakeHeld = false;
            steeringHoldDuration = 0f;
            lastDirection = 0;
            DriftStrength = 0f;
        }

        private static int ReadKeyboardDirection()
        {
            if (Keyboard.current == null) return 0;
            var left = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            var right = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
            if (left == right) return 0;
            return left ? -1 : 1;
        }
    }
}
