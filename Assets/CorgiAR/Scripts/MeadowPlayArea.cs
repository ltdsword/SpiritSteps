using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Single source of truth for the non-AR garden. The decorative meadow can
    /// extend far beyond this rectangle; only gameplay and camera pivots are
    /// bounded. Disabled in AR.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeadowPlayArea : MonoBehaviour
    {
        private const float DefaultMinimumVisualGroundSize = 48f;
        private const float DefaultTextureTilesPerWorldUnit = 0.375f;

        [Header("Decorative meadow coverage")]
        [Tooltip("Minimum visual ground width/depth. This keeps the edge outside the camera at maximum zoom and pan.")]
        [SerializeField, Min(24f)] private float minimumVisualGroundSize = 48f;
        [Tooltip("Maintains the authored grass texture density when the visual ground is enlarged.")]
        [SerializeField, Min(0.01f)] private float textureTilesPerWorldUnit = 0.375f;

        [Header("Garden bounds")]
        [Tooltip("How far the desktop camera pivot may explore from the garden centre.")]
        [SerializeField] private Vector2 halfExtents = new(3.2f, 2.6f);
        [Tooltip("Distant safety net inside the decorative meadow. Normal play uses the viewport edge instead.")]
        [SerializeField] private Vector2 hardFallbackHalfExtents = new(10f, 10f);
        [SerializeField, Min(0f)] private float landingPadding = 0.08f;
        [SerializeField, Min(0.05f)] private float heldEdgeSoftness = 0.45f;

        [Header("Camera composition")]
        [SerializeField] private Rect viewportSafeRect = new(0.08f, 0.12f, 0.84f, 0.80f);
        [SerializeField] private Rect cameraFollowRect = new(0.12f, 0.16f, 0.76f, 0.66f);
        [SerializeField, Range(0.01f, 0.2f)] private float viewportEdgeSoftness = 0.06f;

        [SerializeField, HideInInspector] private bool boundaryActive;
        [SerializeField, HideInInspector] private Vector3 center;
        [SerializeField, HideInInspector] private float groundY;

        private bool throwAimActive;
        private MaterialPropertyBlock groundMaterialProperties;

        public bool IsBoundaryActive => boundaryActive && isActiveAndEnabled;
        public bool IsThrowAimActive => IsBoundaryActive && throwAimActive;
        public Vector3 Center => center;
        public float GroundY => groundY;
        public Vector2 HalfExtents => halfExtents;
        public Rect ViewportSafeRect => viewportSafeRect;

        private void Awake()
        {
            EnsureVisualGroundCoverage();
        }

        private void EnsureVisualGroundCoverage()
        {
            float visualGroundSize = Mathf.Max(DefaultMinimumVisualGroundSize,
                minimumVisualGroundSize);
            float tilesPerWorldUnit = textureTilesPerWorldUnit > 0f
                ? textureTilesPerWorldUnit
                : DefaultTextureTilesPerWorldUnit;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Max(Mathf.Abs(scale.x), visualGroundSize);
            scale.z = Mathf.Max(Mathf.Abs(scale.z), visualGroundSize);
            transform.localScale = scale;

            Renderer groundRenderer = GetComponent<Renderer>();
            if (groundRenderer == null)
                return;

            groundMaterialProperties ??= new MaterialPropertyBlock();
            groundRenderer.GetPropertyBlock(groundMaterialProperties);
            Vector4 textureTransform = new(
                scale.x * tilesPerWorldUnit,
                scale.z * tilesPerWorldUnit,
                0f,
                0f);
            groundMaterialProperties.SetVector("_BaseMap_ST", textureTransform);
            groundMaterialProperties.SetVector("_MainTex_ST", textureTransform);
            groundRenderer.SetPropertyBlock(groundMaterialProperties);
        }

        public void Configure(Vector3 worldCenter, float worldGroundY, bool active)
        {
            center = new Vector3(worldCenter.x, worldGroundY, worldCenter.z);
            groundY = worldGroundY;
            boundaryActive = active;
            if (!active)
                throwAimActive = false;
        }

        public void SetBoundaryActive(bool active)
        {
            boundaryActive = active;
            if (!active)
                throwAimActive = false;
        }

        public void SetThrowAimActive(bool active)
        {
            if (IsBoundaryActive)
                throwAimActive = active;
        }

        public bool Contains(Vector3 point, float footprintRadius = 0f)
        {
            if (!IsBoundaryActive)
                return true;

            Vector2 extents = InsetExtents(footprintRadius);
            const float tolerance = 0.001f;
            return Mathf.Abs(point.x - center.x) <= extents.x + tolerance &&
                   Mathf.Abs(point.z - center.z) <= extents.y + tolerance;
        }

        /// <summary>
        /// The desktop pet and landed props share the portion of meadow that is
        /// comfortably visible. The authored rectangle remains a distant safety
        /// net; the viewport is the boundary the player actually experiences.
        /// </summary>
        public bool Contains(Camera camera, Vector3 point, float footprintRadius = 0f)
        {
            if (!IsBoundaryActive)
                return true;
            if (!Contains(point, footprintRadius))
                return false;
            if (camera == null)
                return true;

            Vector3 viewport = camera.WorldToViewportPoint(
                new Vector3(point.x, groundY, point.z));
            const float tolerance = 0.001f;
            return viewport.z > 0f &&
                   viewport.x >= viewportSafeRect.xMin - tolerance &&
                   viewport.x <= viewportSafeRect.xMax + tolerance &&
                   viewport.y >= viewportSafeRect.yMin - tolerance &&
                   viewport.y <= viewportSafeRect.yMax + tolerance;
        }

        public Vector3 ClampGameplayPoint(Vector3 point, float footprintRadius = 0f)
        {
            if (!IsBoundaryActive)
                return point;

            Vector2 extents = InsetExtents(footprintRadius);
            point.x = Mathf.Clamp(point.x, center.x - extents.x, center.x + extents.x);
            point.z = Mathf.Clamp(point.z, center.z - extents.y, center.z + extents.y);
            return point;
        }

        public Vector3 ClampReachablePoint(Camera camera, Vector3 point,
            float footprintRadius = 0f)
        {
            if (!IsBoundaryActive)
                return point;

            // Projection is repeated because the world rectangle and the
            // perspective ground trapezoid are different convex shapes.
            for (int iteration = 0; iteration < 8; iteration++)
            {
                point = ClampGameplayPoint(point, footprintRadius);
                TryClampPointToSafeViewport(camera, groundY, ref point);
            }
            return point;
        }

        /// <summary>
        /// Applies a screen-space soft edge to one movement step. Only the
        /// outward component is reduced, so the pet can naturally slide along
        /// an edge or immediately walk back toward the centre without sticking.
        /// </summary>
        public Vector3 ConstrainPetMotion(Camera camera, Vector3 current,
            Vector3 desired, float footprintRadius = 0f)
        {
            if (!IsBoundaryActive)
                return desired;

            desired = ClampGameplayPoint(desired, footprintRadius);
            if (camera == null)
                return desired;

            Vector3 currentViewport = camera.WorldToViewportPoint(
                new Vector3(current.x, groundY, current.z));
            Vector3 desiredViewport = camera.WorldToViewportPoint(
                new Vector3(desired.x, groundY, desired.z));
            if (currentViewport.z <= 0f || desiredViewport.z <= 0f)
                return desired;

            Vector2 delta = desiredViewport - currentViewport;
            delta.x *= OutwardMovementScale(currentViewport.x, delta.x,
                viewportSafeRect.xMin, viewportSafeRect.xMax);
            delta.y *= OutwardMovementScale(currentViewport.y, delta.y,
                viewportSafeRect.yMin, viewportSafeRect.yMax);

            Vector3 constrainedViewport = new(
                ConstrainViewportStep(currentViewport.x, delta.x,
                    viewportSafeRect.xMin, viewportSafeRect.xMax),
                ConstrainViewportStep(currentViewport.y, delta.y,
                    viewportSafeRect.yMin, viewportSafeRect.yMax),
                0f);
            if (!TryViewportPointToGround(camera, constrainedViewport, groundY,
                    out Vector3 constrained))
                return desired;

            constrained.y = desired.y;
            return ClampGameplayPoint(constrained, footprintRadius);
        }

        public Vector3 ClampCameraPivot(Vector3 pivot)
        {
            if (!IsBoundaryActive)
                return pivot;
            pivot.x = Mathf.Clamp(pivot.x, center.x - halfExtents.x, center.x + halfExtents.x);
            pivot.z = Mathf.Clamp(pivot.z, center.z - halfExtents.y, center.z + halfExtents.y);
            return pivot;
        }

        public Vector3 ConstrainHeldPosition(Camera camera, Vector3 desiredPosition,
            float footprintRadius)
        {
            if (!IsBoundaryActive)
                return desiredPosition;

            Vector2 extents = InsetExtents(footprintRadius + landingPadding);
            desiredPosition.x = SoftLimitAxis(desiredPosition.x, center.x, extents.x);
            desiredPosition.z = SoftLimitAxis(desiredPosition.z, center.z, extents.y);

            if (camera != null)
            {
                Vector3 viewport = camera.WorldToViewportPoint(
                    new Vector3(desiredPosition.x, groundY, desiredPosition.z));
                if (viewport.z > 0f)
                {
                    viewport.x = SoftLimitViewportAxis(viewport.x,
                        viewportSafeRect.xMin, viewportSafeRect.xMax);
                    viewport.y = SoftLimitViewportAxis(viewport.y,
                        viewportSafeRect.yMin, viewportSafeRect.yMax);
                    if (TryViewportPointToGround(camera, viewport, groundY,
                            out Vector3 visiblePosition))
                    {
                        desiredPosition.x = visiblePosition.x;
                        desiredPosition.z = visiblePosition.z;
                    }
                }
            }
            return desiredPosition;
        }

        public Vector3 ConstrainLaunchVelocity(Camera camera, Vector3 origin,
            Vector3 initialVelocity, float targetGroundY, float footprintRadius,
            out Vector3 predictedLanding, out bool wasLimited)
        {
            predictedLanding = ThrowBallistics.LandingPoint(origin, initialVelocity, targetGroundY);
            wasLimited = false;
            if (!IsBoundaryActive)
                return initialVelocity;

            Vector2 extents = InsetExtents(footprintRadius + landingPadding);
            Vector3 unconstrainedLanding = predictedLanding;

            // Alternating projection finds a point in the intersection of the
            // rectangular garden and the camera's perspective ground trapezoid.
            // Unlike an AABB approximation, this remains correct in portrait.
            for (int iteration = 0; iteration < 8; iteration++)
            {
                predictedLanding.x = Mathf.Clamp(predictedLanding.x,
                    center.x - extents.x, center.x + extents.x);
                predictedLanding.z = Mathf.Clamp(predictedLanding.z,
                    center.z - extents.y, center.z + extents.y);
                TryClampPointToSafeViewport(camera, groundY, ref predictedLanding);
            }

            wasLimited = (predictedLanding - unconstrainedLanding).sqrMagnitude > 0.000001f;
            return ThrowBallistics.VelocityForLanding(origin, initialVelocity,
                targetGroundY, predictedLanding);
        }

        public Vector3 GetVisibilityCorrection(Camera camera, Vector3 worldPoint)
        {
            if (!IsBoundaryActive || camera == null)
                return Vector3.zero;

            Vector3 visiblePoint = worldPoint;
            if (!TryClampPointToViewport(camera, groundY, cameraFollowRect,
                    ref visiblePoint))
                return Vector3.zero;
            return worldPoint - visiblePoint;
        }

        private Vector2 InsetExtents(float inset)
        {
            return new Vector2(
                Mathf.Max(0.1f, hardFallbackHalfExtents.x - Mathf.Max(0f, inset)),
                Mathf.Max(0.1f, hardFallbackHalfExtents.y - Mathf.Max(0f, inset)));
        }

        private float SoftLimitAxis(float value, float axisCenter, float halfExtent)
        {
            float delta = value - axisCenter;
            float distance = Mathf.Abs(delta);
            float softness = Mathf.Min(heldEdgeSoftness, halfExtent);
            float softStart = halfExtent - softness;
            if (distance <= softStart)
                return value;

            float resisted = softStart + softness *
                (1f - Mathf.Exp(-(distance - softStart) / Mathf.Max(0.001f, softness)));
            return axisCenter + Mathf.Sign(delta) * Mathf.Min(resisted, halfExtent);
        }

        private float SoftLimitViewportAxis(float value, float minimum, float maximum)
        {
            if (value < minimum + viewportEdgeSoftness)
            {
                float penetration = minimum + viewportEdgeSoftness - value;
                return minimum + viewportEdgeSoftness *
                    Mathf.Exp(-penetration / viewportEdgeSoftness);
            }
            if (value > maximum - viewportEdgeSoftness)
            {
                float penetration = value - (maximum - viewportEdgeSoftness);
                return maximum - viewportEdgeSoftness *
                    Mathf.Exp(-penetration / viewportEdgeSoftness);
            }
            return value;
        }

        private float OutwardMovementScale(float current, float delta,
            float minimum, float maximum)
        {
            if (Mathf.Abs(delta) <= 0.000001f)
                return 1f;

            float available = delta < 0f ? current - minimum : maximum - current;
            if (available <= 0f)
                return 0f;
            float t = Mathf.Clamp01(available / Mathf.Max(0.0001f, viewportEdgeSoftness));
            return t * t * (3f - 2f * t);
        }

        private static float ConstrainViewportStep(float current, float delta,
            float minimum, float maximum)
        {
            // If a camera zoom/pan temporarily put the pet outside, do not snap
            // it. Block farther-out movement and let inward input or the soft
            // camera correction bring it back naturally.
            if (current < minimum)
                return delta > 0f ? Mathf.Min(current + delta, maximum) : current;
            if (current > maximum)
                return delta < 0f ? Mathf.Max(current + delta, minimum) : current;
            return Mathf.Clamp(current + delta, minimum, maximum);
        }

        private bool TryClampPointToSafeViewport(Camera camera, float planeY, ref Vector3 point)
        {
            return TryClampPointToViewport(camera, planeY, viewportSafeRect, ref point);
        }

        private static bool TryClampPointToViewport(Camera camera, float planeY,
            Rect viewportRect, ref Vector3 point)
        {
            if (camera == null)
                return false;

            Vector3 groundPoint = new(point.x, planeY, point.z);
            Vector3 viewport = camera.WorldToViewportPoint(groundPoint);
            if (viewport.z <= 0f)
                return false;
            float clampedX = Mathf.Clamp(viewport.x, viewportRect.xMin, viewportRect.xMax);
            float clampedY = Mathf.Clamp(viewport.y, viewportRect.yMin, viewportRect.yMax);
            if (Mathf.Approximately(clampedX, viewport.x) &&
                Mathf.Approximately(clampedY, viewport.y))
                return true;

            if (!TryViewportPointToGround(camera,
                    new Vector3(clampedX, clampedY, 0f), planeY,
                    out Vector3 clampedPoint))
                return false;
            point.x = clampedPoint.x;
            point.z = clampedPoint.z;
            return true;
        }

        private static bool TryViewportPointToGround(Camera camera,
            Vector3 viewportPoint, float planeY, out Vector3 groundPoint)
        {
            Plane plane = new(Vector3.up, new Vector3(0f, planeY, 0f));
            Ray ray = camera.ViewportPointToRay(viewportPoint);
            if (plane.Raycast(ray, out float distance))
            {
                groundPoint = ray.GetPoint(distance);
                return true;
            }
            groundPoint = default;
            return false;
        }
    }
}
