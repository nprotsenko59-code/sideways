using Sideways.Vehicle;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sideways.UI
{
    public sealed class TouchSteeringZone : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private VehicleInputSource inputSource;
        [SerializeField, Range(-1, 1)] private int direction;
        private int activePointer = int.MinValue;

        public void Configure(VehicleInputSource source, int steeringDirection)
        {
            inputSource = source;
            direction = steeringDirection < 0 ? -1 : 1;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            activePointer = eventData.pointerId;
            inputSource?.SetTouchSteering(direction, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer) return;
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer) return;
            Release();
        }

        private void OnDisable() => Release();

        private void Release()
        {
            inputSource?.SetTouchSteering(direction, false);
            activePointer = int.MinValue;
        }
    }
}
