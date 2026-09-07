using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using ShibaFeeding;

namespace CorgiAR.UI
{
    /// <summary>UI Toolkit port of <see cref="ToyDragThrowUI"/>'s drag-and-throw logic - see
    /// <see cref="ArFoodDragController"/> for why this is a separate copy.</summary>
    public sealed class ArBallDragController
    {
        private readonly VisualElement element;
        private readonly Camera worldCamera;
        private readonly IThrowTarget target;
        private readonly IThrowBoundary throwBoundary;
        private readonly GameObject ballPrefab;

        private const float HeldHeight = 0.65f;
        private const float MaxThrowSpeed = 4f;
        private const float VelocitySmoothing = 18f;
        private const float MinHeldDistance = 0.4f;
        private const float MaxHeldDistance = 2.6f;

        private ThrownToy heldToy;
        private bool dragging;
        private int activePointerId = -1;
        private Vector3 lastHeldPosition;
        private float lastHeldTime;
        private Vector3 releaseVelocity;
        private Vector3 safeReleaseVelocity;
        private float heldFootprintRadius = 0.16f;
        private ThrowLandingIndicator landingIndicator;

        public ArBallDragController(VisualElement element, Camera worldCamera, IThrowTarget target, GameObject ballPrefab)
        {
            this.element = element;
            this.worldCamera = worldCamera;
            this.target = target;
            this.throwBoundary = target as IThrowBoundary;
            this.ballPrefab = ballPrefab;

            element.RegisterCallback<PointerDownEvent>(OnPointerDown);
            element.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            element.RegisterCallback<PointerUpEvent>(OnPointerUp);
            element.RegisterCallback<PointerCaptureOutEvent>(_ => ReleaseBall());
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (heldToy != null || target == null || target.IsBusy || ballPrefab == null)
                return;

            activePointerId = evt.pointerId;
            element.CapturePointer(activePointerId);
            dragging = true;

            Vector3 spawnPos = ScreenToWorld(PointerScreenPosition());
            GameObject go = Object.Instantiate(ballPrefab, spawnPos, Quaternion.identity);
            go.name = "Play Ball (Held)";
            heldToy = go.GetComponent<ThrownToy>();
            if (heldToy == null)
            {
                Object.Destroy(go);
                dragging = false;
                return;
            }
            heldToy.SetHeld(true);
            heldFootprintRadius = ThrowLandingIndicator.MeasureFootprint(heldToy.transform, 0.16f);
            heldToy.transform.position = ConstrainHeldPosition(heldToy.transform.position);
            throwBoundary?.SetThrowAimActive(true);
            target.BeginAim(heldToy.transform);
            element.style.opacity = 0.8f;

            lastHeldPosition = heldToy.transform.position;
            lastHeldTime = Time.time;
            releaseVelocity = Vector3.zero;
            safeReleaseVelocity = Vector3.zero;
            UpdateThrowPreview();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || heldToy == null || evt.pointerId != activePointerId)
                return;
            Vector3 newPosition = ScreenToWorld(PointerScreenPosition());
            float dt = Mathf.Max(Time.time - lastHeldTime, 0.0001f);
            Vector3 instantVelocity = (newPosition - lastHeldPosition) / dt;
            float blend = 1f - Mathf.Exp(-VelocitySmoothing * dt);
            releaseVelocity = Vector3.Lerp(releaseVelocity, instantVelocity, blend);
            releaseVelocity = Vector3.ClampMagnitude(releaseVelocity, MaxThrowSpeed);
            heldToy.transform.position = newPosition;
            lastHeldPosition = newPosition;
            lastHeldTime = Time.time;
            UpdateThrowPreview();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId == activePointerId)
                element.ReleasePointer(activePointerId);
            ReleaseBall();
        }

        private void ReleaseBall()
        {
            if (!dragging)
                return;
            dragging = false;
            activePointerId = -1;
            element.style.opacity = 1f;
            if (heldToy == null)
                return;

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

        private static Vector2 PointerScreenPosition() =>
            Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            if (worldCamera == null)
                return Vector3.zero;
            float groundY = target is ToyFetchController fetch ? fetch.GroundY
                : target != null ? target.GetThrowAnchorPoint().y : 0f;
            Plane heldPlane = new Plane(Vector3.up, new Vector3(0f, groundY + HeldHeight, 0f));
            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            // See ArFoodDragController.ScreenToHeldWorld: at grazing angles a tiny finger
            // movement can send the raycast distance to near-zero or very far, making the
            // held ball visually snap bigger/smaller. Clamp to a plausible arm's-length range.
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
            if (heldToy == null || throwBoundary == null || !throwBoundary.IsThrowBoundaryActive)
            {
                ThrowLandingIndicator.HideIfAlive(ref landingIndicator);
                return;
            }
            float groundY = target is ToyFetchController fetch ? fetch.GroundY : target.GetThrowAnchorPoint().y;
            safeReleaseVelocity = throwBoundary.ConstrainLaunchVelocity(worldCamera,
                heldToy.transform.position, releaseVelocity, groundY, heldFootprintRadius,
                out Vector3 landing, out bool limited);
            landing.y = throwBoundary.ThrowPreviewGroundY;
            if (landingIndicator == null)
                landingIndicator = ThrowLandingIndicator.Create(heldFootprintRadius);
            landingIndicator.Show(landing, limited);
        }
    }
}
