using UnityEngine;
using UnityEngine.InputSystem;

namespace ShibaFeeding
{
    /// <summary>Soft third-person orbit camera that keeps the pet readable inside the meadow.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class MeadowOrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.72f, 0f);

        [Header("Orbit limits")]
        [SerializeField] private float startingYaw = 0f;
        [SerializeField] private float startingPitch = 31f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(20f, 57f);
        [SerializeField] private float startingDistance = 6.2f;
        [SerializeField] private Vector2 distanceLimits = new Vector2(4.4f, 7.8f);
        [SerializeField] private Vector2 meadowLimits = new Vector2(5.4f, 3.6f);
        [SerializeField, Min(0f)] private float maxPanFromPet = 1.15f;

        [Header("Controls")]
        [SerializeField] private float orbitSensitivity = 0.16f;
        [SerializeField] private float zoomSensitivity = 0.006f;
        [SerializeField] private float keyboardOrbitSpeed = 72f;
        [SerializeField] private float keyboardPanSpeed = 1.7f;
        [SerializeField] private float panSensitivity = 0.006f;

        [Header("Smoothing")]
        [SerializeField] private float followSharpness = 5.5f;
        [SerializeField] private float orbitSharpness = 10f;
        [SerializeField] private float zoomSharpness = 9f;

        private float yaw;
        private float pitch;
        private float targetDistance;
        private float currentDistance;
        private Vector3 panOffset;
        private Vector3 smoothPivot;
        private Quaternion smoothRotation;
        private bool initialized;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            initialized = false;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            if (!initialized)
                InitializeCamera();

            ReadMouseAndKeyboard();
            ReadTwoFingerTouch();

            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            Vector3 petPivot = target.position + targetOffset;
            Vector3 desiredPivot = petPivot + panOffset;
            desiredPivot.x = Mathf.Clamp(desiredPivot.x, -meadowLimits.x, meadowLimits.x);
            desiredPivot.z = Mathf.Clamp(desiredPivot.z, -meadowLimits.y, meadowLimits.y);
            desiredPivot.y = Mathf.Max(0.45f, desiredPivot.y);

            smoothPivot = Vector3.Lerp(smoothPivot, desiredPivot, Damp(followSharpness, dt));
            Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
            smoothRotation = Quaternion.Slerp(smoothRotation, desiredRotation, Damp(orbitSharpness, dt));
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Damp(zoomSharpness, dt));

            Vector3 desiredPosition = smoothPivot - smoothRotation * Vector3.forward * currentDistance;
            desiredPosition.y = Mathf.Max(0.55f, desiredPosition.y);
            transform.SetPositionAndRotation(desiredPosition, smoothRotation);
        }

        private void InitializeCamera()
        {
            yaw = startingYaw;
            pitch = Mathf.Clamp(startingPitch, pitchLimits.x, pitchLimits.y);
            targetDistance = Mathf.Clamp(startingDistance, distanceLimits.x, distanceLimits.y);
            currentDistance = targetDistance;
            smoothPivot = target.position + targetOffset;
            smoothRotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(
                smoothPivot - smoothRotation * Vector3.forward * currentDistance,
                smoothRotation);
            initialized = true;
        }

        private void ReadMouseAndKeyboard()
        {
            float dt = Time.unscaledDeltaTime;
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed)
                {
                    yaw += delta.x * orbitSensitivity;
                    pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, pitchLimits.x, pitchLimits.y);
                }
                if (mouse.middleButton.isPressed)
                    PanByScreenDelta(-delta * panSensitivity);

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSensitivity,
                        distanceLimits.x, distanceLimits.y);
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            float orbit = 0f;
            if (keyboard.qKey.isPressed) orbit -= 1f;
            if (keyboard.eKey.isPressed) orbit += 1f;
            yaw += orbit * keyboardOrbitSpeed * dt;

            Vector2 pan = Vector2.zero;
            if (keyboard.aKey.isPressed) pan.x -= 1f;
            if (keyboard.dKey.isPressed) pan.x += 1f;
            if (keyboard.sKey.isPressed) pan.y -= 1f;
            if (keyboard.wKey.isPressed) pan.y += 1f;
            if (pan.sqrMagnitude > 0f)
                PanByScreenDelta(pan.normalized * keyboardPanSpeed * dt);
        }

        private void ReadTwoFingerTouch()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return;

            var first = touchscreen.touches[0];
            var second = touchscreen.touches[1];
            if (!first.press.isPressed || !second.press.isPressed)
                return;

            Vector2 firstDelta = first.delta.ReadValue();
            Vector2 secondDelta = second.delta.ReadValue();
            Vector2 averageDelta = (firstDelta + secondDelta) * 0.5f;
            yaw += averageDelta.x * orbitSensitivity * 0.7f;
            pitch = Mathf.Clamp(pitch - averageDelta.y * orbitSensitivity * 0.7f,
                pitchLimits.x, pitchLimits.y);

            Vector2 firstPosition = first.position.ReadValue();
            Vector2 secondPosition = second.position.ReadValue();
            float currentSeparation = Vector2.Distance(firstPosition, secondPosition);
            float previousSeparation = Vector2.Distance(firstPosition - firstDelta, secondPosition - secondDelta);
            targetDistance = Mathf.Clamp(targetDistance - (currentSeparation - previousSeparation) * 0.012f,
                distanceLimits.x, distanceLimits.y);
        }

        private void PanByScreenDelta(Vector2 delta)
        {
            Vector3 right = Vector3.ProjectOnPlane(smoothRotation * Vector3.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(smoothRotation * Vector3.forward, Vector3.up).normalized;
            panOffset += right * delta.x + forward * delta.y;
            panOffset = Vector3.ClampMagnitude(panOffset, maxPanFromPet);
        }

        private static float Damp(float sharpness, float deltaTime)
        {
            return 1f - Mathf.Exp(-sharpness * deltaTime);
        }
    }
}
