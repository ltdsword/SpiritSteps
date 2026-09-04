using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        [SerializeField] private Color normalColor = new(0.10f, 0.28f, 0.52f, 0.85f);
        [SerializeField] private Color pressedColor = new(0.20f, 0.44f, 0.78f, 0.96f);

        private IThrowTarget target;
        private ThrownToy heldToy;
        private bool dragging;

        // Real, unassisted throw velocity: measured from the actual drag motion
        // (world position delta / time), nothing added. Holding still and
        // releasing yields ~zero velocity, so the ball just drops under gravity.
        private Vector3 lastHeldPosition;
        private float lastHeldTime;
        private Vector3 releaseVelocity;

        public void Configure(Camera camera, IThrowTarget receiver, GameObject prefab, Graphic graphic)
        {
            worldCamera = camera;
            target = receiver;
            fetchTarget = receiver as MonoBehaviour;
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
            if (target != null) return;
            target = fetchTarget as IThrowTarget;
            if (target == null)
            {
                target = FindFirstObjectByType<ToyFetchController>();
                fetchTarget = target as MonoBehaviour;
            }
        }

        private void Update()
        {
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
            target.BeginAim(heldToy.transform);
            SetColor(pressedColor);

            lastHeldPosition = spawnPos;
            lastHeldTime = Time.time;
            releaseVelocity = Vector3.zero;
        }

        private void MoveHeld(Vector2 screenPosition)
        {
            if (!dragging || heldToy == null) return;
            Vector3 newPosition = ScreenToWorld(screenPosition);
            float dt = Mathf.Max(Time.time - lastHeldTime, 0.0001f);
            releaseVelocity = (newPosition - lastHeldPosition) / dt;
            heldToy.transform.position = newPosition;
            lastHeldPosition = newPosition;
            lastHeldTime = Time.time;
        }

        private void Release(Vector2 screenPosition)
        {
            if (!dragging) return;
            dragging = false;
            SetColor(normalColor);
            if (heldToy == null) return;

            ThrownToy toy = heldToy;
            heldToy = null;
            target.EndAim();
            toy.SetHeld(false);

            float groundY = target is ToyFetchController f ? f.GroundY : target.GetThrowAnchorPoint().y;
            toy.Launch(releaseVelocity, target, groundY);
        }

        private bool IsInside(Vector2 screenPosition)
        {
            RectTransform rect = transform as RectTransform;
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null);
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            if (worldCamera == null) return Vector3.zero;
            return worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, heldDepth));
        }

        private void SetColor(Color color)
        {
            if (buttonGraphic != null) buttonGraphic.color = color;
        }
    }
}
