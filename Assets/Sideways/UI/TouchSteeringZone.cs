using Sideways.Vehicle;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sideways.UI
{
    public sealed class TouchSteeringZone : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private VehicleInputSource inputSource;
        [SerializeField, Range(-1, 1)] private int direction;
        private int activePointer = int.MinValue;
        private int activeDirection;

        public void Configure(VehicleInputSource source, int steeringDirection)
        {
            inputSource = source;
            direction = steeringDirection < 0 ? -1 : 1;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            activePointer = eventData.pointerId;
            SetPointerDirection(direction);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer) return;
            Release();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer) return;
            SetPointerDirection(eventData.position.x < Screen.width * 0.5f ? -1 : 1);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) Release();
        }

        private void OnDisable() => Release();

        private void Release()
        {
            if (activeDirection != 0) inputSource?.SetTouchSteering(activeDirection, false);
            activeDirection = 0;
            activePointer = int.MinValue;
        }

        private void SetPointerDirection(int nextDirection)
        {
            nextDirection = nextDirection < 0 ? -1 : 1;
            if (activeDirection == nextDirection) return;
            if (activeDirection != 0) inputSource?.SetTouchSteering(activeDirection, false);
            activeDirection = nextDirection;
            inputSource?.SetTouchSteering(activeDirection, true);
        }
    }
}
