using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ARWalking.UI
{
    public sealed class IllustratedMapManipulator : PointerManipulator
    {
        readonly VisualElement _content;
        readonly Dictionary<int, Vector2> _pointers = new Dictionary<int, Vector2>();
        readonly float _minimumZoom;
        readonly float _maximumZoom;
        Vector2 _translation;
        float _zoom = 1f;
        float _previousPinchDistance;

        public IllustratedMapManipulator(VisualElement content, float minimumZoom, float maximumZoom)
        {
            _content = content;
            _minimumZoom = Mathf.Max(1f, minimumZoom);
            _maximumZoom = Mathf.Max(_minimumZoom, maximumZoom);
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.RegisterCallback<WheelEvent>(OnWheel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            target.UnregisterCallback<WheelEvent>(OnWheel);
        }

        public void Recenter()
        {
            _zoom = 1f;
            _translation = Vector2.zero;
            ApplyTransform();
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            _pointers[evt.pointerId] = evt.position;
            target.CapturePointer(evt.pointerId);
            if (_pointers.Count == 2)
                _previousPinchDistance = PointerDistance();
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_pointers.TryGetValue(evt.pointerId, out var previous))
                return;

            _pointers[evt.pointerId] = evt.position;
            if (_pointers.Count >= 2)
            {
                var distance = PointerDistance();
                if (_previousPinchDistance > 0.01f)
                    SetZoom(_zoom * distance / _previousPinchDistance);
                _previousPinchDistance = distance;
            }
            else
            {
                _translation += (Vector2)evt.position - previous;
                ClampTranslation();
                ApplyTransform();
            }
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt) => ReleasePointer(evt.pointerId);
        void OnPointerCancel(PointerCancelEvent evt) => ReleasePointer(evt.pointerId);

        void ReleasePointer(int pointerId)
        {
            _pointers.Remove(pointerId);
            if (target.HasPointerCapture(pointerId))
                target.ReleasePointer(pointerId);
            _previousPinchDistance = _pointers.Count == 2 ? PointerDistance() : 0f;
        }

        void OnWheel(WheelEvent evt)
        {
            SetZoom(_zoom * (evt.delta.y > 0 ? 0.9f : 1.1f));
            evt.StopPropagation();
        }

        void SetZoom(float value)
        {
            _zoom = Mathf.Clamp(value, _minimumZoom, _maximumZoom);
            ClampTranslation();
            ApplyTransform();
        }

        float PointerDistance()
        {
            using (var enumerator = _pointers.Values.GetEnumerator())
            {
                enumerator.MoveNext();
                var first = enumerator.Current;
                enumerator.MoveNext();
                return Vector2.Distance(first, enumerator.Current);
            }
        }

        void ClampTranslation()
        {
            var size = target.contentRect.size;
            var maxX = Mathf.Max(0f, size.x * (_zoom - 1f) * 0.5f);
            var maxY = Mathf.Max(0f, size.y * (_zoom - 1f) * 0.5f);
            _translation.x = Mathf.Clamp(_translation.x, -maxX, maxX);
            _translation.y = Mathf.Clamp(_translation.y, -maxY, maxY);
        }

        void ApplyTransform()
        {
            _content.style.scale = new Scale(new Vector3(_zoom, _zoom, 1f));
            _content.style.translate = new Translate(_translation.x, _translation.y, 0f);
        }
    }
}
