using Sideways.Vehicle;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sideways.UI
{
    public sealed class TouchHandbrakeGesture : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private VehicleInputSource inputSource;
        [SerializeField, Range(0.03f, 0.4f)] private float activationDistanceScreenHeight = 0.1f;
        [SerializeField, Min(20f)] private float minimumActivationPixels = 70f;

        private int activePointer = int.MinValue;
        private float startY;
        private bool activated;

        public void Configure(VehicleInputSource source)
        {
            inputSource = source;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Release();
            activePointer = eventData.pointerId;
            startY = eventData.position.y;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer || activated) return;
            var activationDistance = Mathf.Max(minimumActivationPixels, Screen.height * activationDistanceScreenHeight);
            if (startY - eventData.position.y < activationDistance) return;

            activated = true;
            inputSource?.SetTouchHandbrake(activePointer, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) Release();
        }

        private void OnDisable() => Release();

        private void Release()
        {
            if (activated) inputSource?.SetTouchHandbrake(activePointer, false);
            activePointer = int.MinValue;
            activated = false;
        }
    }
}
