using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CorgiAR
{
    /// <summary>
    /// uGUI left-hand thumbstick. Drag the knob; the normalised vector is pushed
    /// to <see cref="DogCompanionController.SetManualInput"/>. Also works with the
    /// mouse for editor testing. Hidden unless the pet is placed and in Manual.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform baseRect;
        [SerializeField] private RectTransform knob;
        [SerializeField] private DogCompanionController companion;
        [SerializeField, Min(10f)] private float radius = 96f;

        private int pointerId = -2;

        private void Reset() => baseRect = transform as RectTransform;

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerId = eventData.pointerId;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId || baseRect == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                baseRect, eventData.position, eventData.pressEventCamera, out Vector2 local);
            Vector2 clamped = Vector2.ClampMagnitude(local, radius);
            if (knob != null)
                knob.anchoredPosition = clamped;
            companion?.SetManualInput(clamped / radius, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId)
                return;
            pointerId = -2;
            if (knob != null)
                knob.anchoredPosition = Vector2.zero;
            companion?.SetManualInput(Vector2.zero, false);
        }

        private void OnDisable()
        {
            pointerId = -2;
            if (knob != null)
                knob.anchoredPosition = Vector2.zero;
            companion?.SetManualInput(Vector2.zero, false);
        }
    }
}
