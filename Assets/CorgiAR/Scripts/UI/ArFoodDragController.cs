using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using ShibaFeeding;

namespace CorgiAR.UI
{
    /// <summary>
    /// UI Toolkit port of <see cref="ShibaFeeding.FoodDragThrowUI"/>'s drag-and-throw logic, for
    /// the glass HUD (<see cref="CorgiArGlassHud"/>). The world-space math (screen-to-plane
    /// raycast, velocity tracking, throw boundary) is framework-agnostic and copied verbatim;
    /// only the input plumbing changes from uGUI's IPointerDownHandler/EventSystem to UI
    /// Toolkit's pointer events + explicit pointer capture. Kept separate from the original so
    /// the ShibaFeeding demo (still uGUI) is untouched.
    /// </summary>
    public sealed class ArFoodDragController
    {
        private readonly VisualElement element;
        private readonly Camera worldCamera;
        private readonly IFeedableDog shiba;
        private readonly IThrowBoundary throwBoundary;
        private readonly GameObject foodPrefab;

        private const float HeldHeight = 0.65f;
        private const float MaxThrowSpeed = 4f;
        private const float VelocitySmoothing = 18f;
        private const float MinHeldDistance = 0.4f;
        private const float MaxHeldDistance = 2.6f;

        private ThrownFood heldFood;
        private bool dragging;
        private int activePointerId = -1;
        private Vector3 lastHeldPosition;
        private float lastHeldTime;
        private Vector3 releaseVelocity;
        private Vector3 safeReleaseVelocity;
        private float heldFootprintRadius = 0.14f;
        private ThrowLandingIndicator landingIndicator;

        public ArFoodDragController(VisualElement element, Camera worldCamera, IFeedableDog shiba, GameObject foodPrefab)
        {
            this.element = element;
            this.worldCamera = worldCamera;
            this.shiba = shiba;
            this.throwBoundary = shiba as IThrowBoundary;
            this.foodPrefab = foodPrefab;

            element.RegisterCallback<PointerDownEvent>(OnPointerDown);
            element.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            element.RegisterCallback<PointerUpEvent>(OnPointerUp);
            element.RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag());
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (heldFood != null || shiba == null || shiba.IsEating)
                return;

            activePointerId = evt.pointerId;
            element.CapturePointer(activePointerId);
            dragging = true;

            Vector3 spawnPosition = ScreenToHeldWorld(PointerScreenPosition());
            GameObject foodObject = Object.Instantiate(foodPrefab, spawnPosition, Quaternion.identity);
            foodObject.name = "Low Poly Treat (Held)";
            heldFood = foodObject.GetComponent<ThrownFood>();
            if (heldFood == null)
                heldFood = foodObject.AddComponent<ThrownFood>();

            heldFood.SetHeld(true);
            heldFootprintRadius = ThrowLandingIndicator.MeasureFootprint(heldFood.transform, 0.14f);
            heldFood.transform.position = ConstrainHeldPosition(heldFood.transform.position);
            throwBoundary?.SetThrowAimActive(true);
            shiba.BeginFollowingHeldFood(heldFood.transform);
            element.style.opacity = 0.8f;

            lastHeldPosition = heldFood.transform.position;
            lastHeldTime = Time.time;
            releaseVelocity = Vector3.zero;
            safeReleaseVelocity = Vector3.zero;
            UpdateThrowPreview();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || heldFood == null || evt.pointerId != activePointerId)
                return;
            Vector3 newPosition = ScreenToHeldWorld(PointerScreenPosition());
            float dt = Mathf.Max(Time.time - lastHeldTime, 0.0001f);
            Vector3 instantVelocity = (newPosition - lastHeldPosition) / dt;
            float blend = 1f - Mathf.Exp(-VelocitySmoothing * dt);
            releaseVelocity = Vector3.Lerp(releaseVelocity, instantVelocity, blend);
            releaseVelocity = Vector3.ClampMagnitude(releaseVelocity, MaxThrowSpeed);
            heldFood.transform.position = newPosition;
            lastHeldPosition = newPosition;
            lastHeldTime = Time.time;
            UpdateThrowPreview();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId == activePointerId)
                element.ReleasePointer(activePointerId);
            ReleaseFood();
        }

        private void CancelDrag() => ReleaseFood();

        private void ReleaseFood()
        {
            if (!dragging)
                return;
            dragging = false;
            activePointerId = -1;
            element.style.opacity = 1f;
            if (heldFood == null)
                return;

            ThrownFood releasedFood = heldFood;
            UpdateThrowPreview();
            bool boundedThrow = throwBoundary != null && throwBoundary.IsThrowBoundaryActive;
            heldFood = null;
            shiba.EndFollowingHeldFood();
            releasedFood.SetHeld(false);
            throwBoundary?.SetThrowAimActive(false);
            ThrowLandingIndicator.HideIfAlive(ref landingIndicator);

            float groundY = shiba.GetFoodLandingPoint().y;
            releasedFood.Launch(safeReleaseVelocity, shiba, groundY, boundedThrow);
        }

        private static Vector2 PointerScreenPosition() =>
            Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;

        private Vector3 ScreenToHeldWorld(Vector2 screenPosition)
        {
            if (worldCamera == null)
                return Vector3.zero;
            float groundY = shiba != null ? shiba.GetFoodLandingPoint().y : 0f;
            Plane heldPlane = new Plane(Vector3.up, new Vector3(0f, groundY + HeldHeight, 0f));
            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            // At grazing angles (camera nearly level with the held plane - common when
            // the phone is held low relative to the AR ground), the raycast distance
            // can blow up or collapse from a tiny finger movement, making the held food
            // visually snap much closer/farther (and so much bigger/smaller) than
            // intended. Clamp to a plausible arm's-length range instead.
            if (heldPlane.Raycast(ray, out float distance))
                return ConstrainHeldPosition(ray.GetPoint(Mathf.Clamp(distance, MinHeldDistance, MaxHeldDistance)));
            return ConstrainHeldPosition(worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 2.4f)));
        }

        private Vector3 ConstrainHeldPosition(Vector3 position) =>
            throwBoundary != null && throwBoundary.IsThrowBoundaryActive
                ? throwBoundary.ConstrainHeldPosition(worldCamera, position, heldFootprintRadius)
                : position;

        private void UpdateThrowPreview()
        {
            safeReleaseVelocity = releaseVelocity;
            if (heldFood == null || throwBoundary == null || !throwBoundary.IsThrowBoundaryActive)
            {
                ThrowLandingIndicator.HideIfAlive(ref landingIndicator);
                return;
            }
            float groundY = shiba.GetFoodLandingPoint().y;
            safeReleaseVelocity = throwBoundary.ConstrainLaunchVelocity(worldCamera,
                heldFood.transform.position, releaseVelocity, groundY, heldFootprintRadius,
                out Vector3 landing, out bool limited);
            landing.y = throwBoundary.ThrowPreviewGroundY;
            if (landingIndicator == null)
                landingIndicator = ThrowLandingIndicator.Create(heldFootprintRadius);
            landingIndicator.Show(landing, limited);
        }
    }
}
