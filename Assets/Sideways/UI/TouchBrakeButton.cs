using Sideways.Vehicle;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sideways.UI
{
    public sealed class TouchBrakeButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private VehicleInputSource inputSource;
        private int activePointer = int.MinValue;

        public void Configure(VehicleInputSource source) => inputSource = source;

        public void OnPointerDown(PointerEventData eventData)
        {
            activePointer = eventData.pointerId;
            inputSource?.SetTouchBrake(true);
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
            inputSource?.SetTouchBrake(false);
            activePointer = int.MinValue;
        }
    }
}
