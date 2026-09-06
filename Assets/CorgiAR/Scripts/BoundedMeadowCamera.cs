using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CorgiAR
{
    /// <summary>
    /// Elevated garden camera for the non-AR preview. Its pivot belongs to the
    /// meadow, not the pet; it only moves automatically when the pet approaches
    /// the viewport safe edge.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class BoundedMeadowCamera : MonoBehaviour
    {
        [Header("Pikmin-style view")]
        [SerializeField] private Transform target;
        [SerializeField] private MeadowPlayArea playArea;
        [SerializeField] private float yaw = 180f;
        [SerializeField, Range(20f, 60f)] private float pitch = 36f;
        [SerializeField, Min(1f)] private float distance = 4.1f;
        [SerializeField, Min(0f)] private float targetHeight = 0.42f;
        [SerializeField, Range(30f, 70f)] private float fieldOfView = 50f;

        [Header("Moderate zoom")]
        [SerializeField] private Vector2 zoomLimits = new(1f, 4.8f);
        [Tooltip("World-space zoom distance per normalized mouse-wheel notch.")]
        [SerializeField, Min(0.01f)] private float wheelZoomSensitivity = 0.34f;
        [SerializeField, Min(0.0001f)] private float pinchZoomSensitivity = 0.006f;
        [SerializeField, Min(0.1f)] private float zoomSharpness = 15f;

        [Header("Bounded exploration")]
        [SerializeField, Min(0.0001f)] private float dragWorldUnitsPerPixel = 0.0045f;

        [Header("Smoothing")]
        [SerializeField, Min(0.1f)] private float followSharpness = 5.5f;

        private Camera meadowCamera;
        private Vector3 gardenCenter;
        private Vector3 panOffset;
        private Vector3 smoothPivot;
        private float targetDistance;
        private float currentDistance;
        private bool initialized;
        private bool pointerPanning;

        public void Configure(Transform pet, MeadowPlayArea area)
        {
            target = pet;
            playArea = area;
            initialized = false;
        }

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

            if (playArea == null || !playArea.IsThrowAimActive)
                ReadPanInput();
            else
                pointerPanning = false;

            if (playArea != null && playArea.IsBoundaryActive && !playArea.IsThrowAimActive)
            {
                Vector3 correction = playArea.GetVisibilityCorrection(meadowCamera, target.position);
                if (correction.sqrMagnitude > 0.000001f)
                {
                    Vector3 corrected = playArea.ClampCameraPivot(gardenCenter + panOffset + correction);
                    panOffset = corrected - gardenCenter;
                    panOffset.y = 0f;
                }
            }

            Vector3 desiredGroundPivot = gardenCenter + panOffset;
            if (playArea != null)
                desiredGroundPivot = playArea.ClampCameraPivot(desiredGroundPivot);
            Vector3 desiredPivot = desiredGroundPivot + Vector3.up * targetHeight;

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
            if (playArea == null)
            {
                DogCompanionController companion = target.GetComponent<DogCompanionController>();
                if (companion != null)
                    playArea = companion.PlayArea;
            }
            gardenCenter = playArea != null && playArea.IsBoundaryActive
                ? playArea.Center
                : target.position;
            panOffset = Vector3.zero;
            smoothPivot = gardenCenter + Vector3.up * targetHeight;
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
            {
                target = companion.transform;
                if (playArea == null)
                    playArea = companion.PlayArea;
            }
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
            if (playArea != null && playArea.IsBoundaryActive)
            {
                Vector3 clampedPivot = playArea.ClampCameraPivot(gardenCenter + panOffset);
                panOffset = clampedPivot - gardenCenter;
                panOffset.y = 0f;
            }
        }
    }
}
