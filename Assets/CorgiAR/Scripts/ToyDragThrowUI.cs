using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ShibaFeeding;

namespace CorgiAR
{
    /// <summary>
    /// Drag the ball chip out of the HUD and release to throw it for the pet to
    /// fetch. Trimmed copy of <see cref="ShibaFeeding.FoodDragThrowUI"/> retargeted
    /// to <see cref="IThrowTarget"/>.
    /// </summary>
    public sealed class ToyDragThrowUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private MonoBehaviour fetchTarget;
        [SerializeField] private GameObject toyPrefab;
        [SerializeField] private Graphic buttonGraphic;

        [SerializeField, Min(0.5f)] private float heldDepth = 2.4f;
        [SerializeField, Min(0.05f)] private float heldHeight = 0.65f;
        [SerializeField, Min(0.1f)] private float maxThrowSpeed = 4f;
        [SerializeField, Min(0.1f)] private float velocitySmoothing = 18f;
        [SerializeField] private Color normalColor = new(0.10f, 0.28f, 0.52f, 0.85f);
        [SerializeField] private Color pressedColor = new(0.20f, 0.44f, 0.78f, 0.96f);

        private IThrowTarget target;
        private IThrowBoundary throwBoundary;
        private ThrownToy heldToy;
        private bool dragging;

        // Real, unassisted throw velocity: measured from the actual drag motion
        // (world position delta / time), nothing added. Holding still and
        // releasing yields ~zero velocity, so the ball just drops under gravity.
        private Vector3 lastHeldPosition;
        private float lastHeldTime;
        private Vector3 releaseVelocity;
        private Vector3 safeReleaseVelocity;
        private float heldFootprintRadius = 0.16f;
        private ThrowLandingIndicator landingIndicator;

        public void Configure(Camera camera, IThrowTarget receiver, GameObject prefab, Graphic graphic)
        {
            worldCamera = camera;
            target = receiver;
            fetchTarget = receiver as MonoBehaviour;
            throwBoundary = receiver as IThrowBoundary;
            toyPrefab = prefab;
            buttonGraphic = graphic;
        }

        /// <summary>Re-point at the live AR/preview camera (the one baked in at HUD
        /// generation time gets disabled when the app switches into real AR).</summary>
        public void SetCamera(Camera camera) => worldCamera = camera;

        private void Awake()
        {
            ResolveTarget();
            if (buttonGraphic == null) buttonGraphic = GetComponent<Graphic>();
            SetColor(normalColor);
        }

        private void ResolveTarget()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (target != null)
            {
                if (throwBoundary == null)
                    throwBoundary = fetchTarget as IThrowBoundary;
                return;
            }
            target = fetchTarget as IThrowTarget;
            if (target == null)
            {
                target = FindFirstObjectByType<ToyFetchController>();
                fetchTarget = target as MonoBehaviour;
            }
            throwBoundary = fetchTarget as IThrowBoundary;
        }

        private void Update()
        {
            if (EventSystem.current != null && EventSystem.current.currentInputModule != null &&
                EventSystem.current.currentInputModule.isActiveAndEnabled)
                return;

            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                Vector2 p = touch.position.ReadValue();
                if (touch.press.wasPressedThisFrame && IsInside(p)) BeginHold(p);
                else if (dragging && touch.press.isPressed) MoveHeld(p);
                else if (dragging && touch.press.wasReleasedThisFrame) Release(p);
                return;
            }
            if (Mouse.current == null) return;
            Vector2 m = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame && IsInside(m)) BeginHold(m);
            else if (dragging && Mouse.current.leftButton.isPressed) MoveHeld(m);
            else if (dragging && Mouse.current.leftButton.wasReleasedThisFrame) Release(m);
        }

        public void OnPointerDown(PointerEventData e) => BeginHold(e.position);
        public void OnDrag(PointerEventData e) => MoveHeld(e.position);
        public void OnPointerUp(PointerEventData e) => Release(e.position);

        private void BeginHold(Vector2 screenPosition)
        {
            ResolveTarget();
            if (heldToy != null || target == null || target.IsBusy || toyPrefab == null)
                return;
            dragging = true;
            Vector3 spawnPos = ScreenToWorld(screenPosition);
            GameObject go = Instantiate(toyPrefab, spawnPos, Quaternion.identity);
            go.name = "Play Ball (Held)";
            heldToy = go.GetComponent<ThrownToy>();
            if (heldToy == null)
            {
                Destroy(go);
                dragging = false;
                return;
            }
            heldToy.SetHeld(true);
            heldFootprintRadius = ThrowLandingIndicator.MeasureFootprint(heldToy.transform, 0.16f);
            heldToy.transform.position = ConstrainHeldPosition(heldToy.transform.position);
            throwBoundary?.SetThrowAimActive(true);
            target.BeginAim(heldToy.transform);
            SetColor(pressedColor);

            lastHeldPosition = heldToy.transform.position;
            lastHeldTime = Time.time;
            releaseVelocity = Vector3.zero;
            safeReleaseVelocity = Vector3.zero;
            UpdateThrowPreview();
        }

        private void MoveHeld(Vector2 screenPosition)
        {
            if (!dragging || heldToy == null) return;
            Vector3 newPosition = ScreenToWorld(screenPosition);
            float dt = Mathf.Max(Time.time - lastHeldTime, 0.0001f);
            Vector3 instantVelocity = (newPosition - lastHeldPosition) / dt;
            float blend = 1f - Mathf.Exp(-velocitySmoothing * dt);
            releaseVelocity = Vector3.Lerp(releaseVelocity, instantVelocity, blend);
            releaseVelocity = Vector3.ClampMagnitude(releaseVelocity, maxThrowSpeed);
            heldToy.transform.position = newPosition;
            lastHeldPosition = newPosition;
            lastHeldTime = Time.time;
            UpdateThrowPreview();
        }

        private void Release(Vector2 screenPosition)
        {
            if (!dragging) return;
            dragging = false;
            SetColor(normalColor);
            if (heldToy == null) return;

            ThrownToy toy = heldToy;
            UpdateThrowPreview();
            bool boundedThrow = throwBoundary != null && throwBoundary.IsThrowBoundaryActive;
            heldToy = null;
            target.EndAim();
            toy.SetHeld(false);
            throwBoundary?.SetThrowAimActive(false);
            ThrowLandingIndicator.HideIfAlive(ref landingIndicator);

            float groundY = target is ToyFetchController f ? f.GroundY : target.GetThrowAnchorPoint().y;
            toy.Launch(safeReleaseVelocity, target, groundY, boundedThrow);
        }

        private bool IsInside(Vector2 screenPosition)
        {
            RectTransform rect = transform as RectTransform;
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null);
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            if (worldCamera == null) return Vector3.zero;

            float groundY = target is ToyFetchController fetch
                ? fetch.GroundY
                : target != null ? target.GetThrowAnchorPoint().y : 0f;
            Plane heldPlane = new Plane(Vector3.up, new Vector3(0f, groundY + heldHeight, 0f));
            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            // At grazing angles (camera nearly level with the held plane) a tiny finger
            // movement can send this distance to near-zero or very far, making the held
            // ball visually snap bigger/smaller. Clamp to a plausible arm's-length range.
            if (heldPlane.Raycast(ray, out float distance))
                return ConstrainHeldPosition(ray.GetPoint(Mathf.Clamp(distance, 0.4f, 2.6f)));

            return ConstrainHeldPosition(worldCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, heldDepth)));
        }

        private Vector3 ConstrainHeldPosition(Vector3 position)
        {
            return throwBoundary != null && throwBoundary.IsThrowBoundaryActive
                ? throwBoundary.ConstrainHeldPosition(worldCamera, position, heldFootprintRadius)
                : position;
        }

        private void UpdateThrowPreview()
        {
            safeReleaseVelocity = releaseVelocity;
            if (heldToy == null || throwBoundary == null || !throwBoundary.IsThrowBoundaryActive)
            {
                ThrowLandingIndicator.HideIfAlive(ref landingIndicator);
                return;
            }

            float groundY = target is ToyFetchController fetch
                ? fetch.GroundY
                : target.GetThrowAnchorPoint().y;
            safeReleaseVelocity = throwBoundary.ConstrainLaunchVelocity(worldCamera,
                heldToy.transform.position, releaseVelocity, groundY, heldFootprintRadius,
                out Vector3 landing, out bool limited);
            landing.y = throwBoundary.ThrowPreviewGroundY;
            if (landingIndicator == null)
                landingIndicator = ThrowLandingIndicator.Create(heldFootprintRadius);
            landingIndicator.Show(landing, limited);
        }

        private void OnDisable()
        {
            throwBoundary?.SetThrowAimActive(false);
            ThrowLandingIndicator.HideIfAlive(ref landingIndicator);
        }

        private void SetColor(Color color)
        {
            if (buttonGraphic != null) buttonGraphic.color = color;
        }
    }
}
