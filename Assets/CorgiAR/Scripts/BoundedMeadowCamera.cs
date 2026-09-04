using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CorgiAR
{
    /// <summary>
    /// Elevated, softly-following meadow camera for the non-AR preview. Drag an
    /// empty part of the screen to inspect the nearby field; both panning and
    /// following stay inside the authored play area.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class BoundedMeadowCamera : MonoBehaviour
    {
        [Header("Pikmin-style view")]
        [SerializeField] private Transform target;
        [SerializeField] private float yaw = 180f;
        [SerializeField, Range(20f, 60f)] private float pitch = 36f;
        [SerializeField, Min(1f)] private float distance = 4.1f;
        [SerializeField, Min(0f)] private float targetHeight = 0.42f;
        [SerializeField, Range(30f, 70f)] private float fieldOfView = 50f;

        [Header("Moderate zoom")]
        [SerializeField] private Vector2 zoomLimits = new(1f, 5.2f);
        [Tooltip("World-space zoom distance per normalized mouse-wheel notch.")]
        [SerializeField, Min(0.01f)] private float wheelZoomSensitivity = 0.34f;
        [SerializeField, Min(0.0001f)] private float pinchZoomSensitivity = 0.006f;
        [SerializeField, Min(0.1f)] private float zoomSharpness = 15f;

        [Header("Bounded exploration")]
        [SerializeField] private Vector2 worldHalfExtents = new(4.4f, 3.6f);
        [SerializeField, Min(0f)] private float maxPanFromPet = 1.65f;
        [SerializeField, Min(0.0001f)] private float dragWorldUnitsPerPixel = 0.0045f;

        [Header("Smoothing")]
        [SerializeField, Min(0.1f)] private float followSharpness = 5.5f;

        private Camera meadowCamera;
        private Vector3 movementCenter;
        private Vector3 panOffset;
        private Vector3 smoothPivot;
        private float targetDistance;
        private float currentDistance;
        private bool initialized;
        private bool pointerPanning;

        private void Awake()
        {
            meadowCamera = GetComponent<Camera>();
            FindTargetIfNeeded();
        }

        private void OnEnable()
        {
            initialized = false;
            pointerPanning = false;
        }

        private void LateUpdate()
        {
            FindTargetIfNeeded();
            if (target == null)
                return;

            if (!initialized)
                InitializeView();

            ReadPanInput();

            Vector3 desiredPivot = target.position + Vector3.up * targetHeight + panOffset;
            desiredPivot.x = Mathf.Clamp(desiredPivot.x,
                movementCenter.x - worldHalfExtents.x, movementCenter.x + worldHalfExtents.x);
            desiredPivot.z = Mathf.Clamp(desiredPivot.z,
                movementCenter.z - worldHalfExtents.y, movementCenter.z + worldHalfExtents.y);

            float blend = 1f - Mathf.Exp(-followSharpness * Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
            smoothPivot = Vector3.Lerp(smoothPivot, desiredPivot, blend);
            float zoomBlend = 1f - Mathf.Exp(-zoomSharpness * Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, zoomBlend);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(
                smoothPivot - rotation * Vector3.forward * currentDistance,
                rotation);
        }

        private void InitializeView()
        {
            movementCenter = target.position;
            smoothPivot = movementCenter + Vector3.up * targetHeight;
            meadowCamera.fieldOfView = fieldOfView;
            targetDistance = Mathf.Clamp(distance, zoomLimits.x, zoomLimits.y);
            currentDistance = targetDistance;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(
                smoothPivot - rotation * Vector3.forward * currentDistance,
                rotation);
            initialized = true;
        }

        private void FindTargetIfNeeded()
        {
            if (target != null)
                return;
            DogCompanionController companion = FindFirstObjectByType<DogCompanionController>();
            if (companion != null)
                target = companion.transform;
        }

        private void ReadPanInput()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null &&
                touchscreen.touches[0].press.isPressed &&
                touchscreen.touches[1].press.isPressed)
            {
                pointerPanning = false;
                var first = touchscreen.touches[0];
                var second = touchscreen.touches[1];
                Vector2 firstPosition = first.position.ReadValue();
                Vector2 secondPosition = second.position.ReadValue();
                float currentSeparation = Vector2.Distance(firstPosition, secondPosition);
                float previousSeparation = Vector2.Distance(
                    firstPosition - first.delta.ReadValue(),
                    secondPosition - second.delta.ReadValue());
                ZoomBy(currentSeparation - previousSeparation, pinchZoomSensitivity);
                return;
            }

            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                var touch = touchscreen.primaryTouch;
                int pointerId = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();
                if (touch.press.wasPressedThisFrame)
                    pointerPanning = CanStartPan(position, pointerId);
                if (pointerPanning)
                    PanByScreenDelta(-touch.delta.ReadValue());
                return;
            }

            if (touchscreen != null && touchscreen.primaryTouch.press.wasReleasedThisFrame)
                pointerPanning = false;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                // Input System backends report either +/-1 or +/-120 per notch.
                // Normalize both so zoom feels the same and reacts immediately.
                float normalizedNotches = Mathf.Abs(wheel) > 10f ? wheel / 120f : wheel;
                ZoomBy(normalizedNotches, wheelZoomSensitivity);
            }

            Vector2 mousePosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
                pointerPanning = CanStartPan(mousePosition, -1);
            if (pointerPanning && mouse.leftButton.isPressed)
                PanByScreenDelta(-mouse.delta.ReadValue());
            if (mouse.leftButton.wasReleasedThisFrame)
                pointerPanning = false;
        }

        private void ZoomBy(float inputDelta, float sensitivity)
        {
            targetDistance = Mathf.Clamp(
                targetDistance - inputDelta * sensitivity,
                zoomLimits.x,
                zoomLimits.y);
        }

        private bool CanStartPan(Vector2 screenPosition, int pointerId)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId))
                return false;

            Ray ray = meadowCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, meadowCamera.farClipPlane,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == target || hitTransform.IsChildOf(target))
                    return false;
            }
            return true;
        }

        private void PanByScreenDelta(Vector2 screenDelta)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            panOffset += (right * screenDelta.x + forward * screenDelta.y) * dragWorldUnitsPerPixel;
            panOffset = Vector3.ClampMagnitude(panOffset, maxPanFromPet);
        }
    }
}
