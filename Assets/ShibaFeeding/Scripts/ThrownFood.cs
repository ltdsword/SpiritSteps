using System.Collections;
using UnityEngine;
using CorgiAR;

namespace ShibaFeeding
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class ThrownFood : MonoBehaviour
    {
        [Header("Throw tuning")]
        [SerializeField, Min(0f)] private float spinSpeed = 720f;
        [SerializeField, Min(0f)] private float eatShrinkDelay = 0.55f;
        [SerializeField, Min(0.1f)] private float eatShrinkDuration = 3.05f;
        [SerializeField, Min(0f)] private float groundClearance = 0.008f;
        [SerializeField] private Vector3 groundRestEuler = new(12f, 28f, 18f);
        [SerializeField, Min(0.1f)] private float mouthFollowSharpness = 22f;
        [SerializeField, Min(0f)] private float ignoredLingerDuration = 2.5f;
        [SerializeField, Min(0.1f)] private float ignoredDisappearDuration = 0.85f;

        private Coroutine movementRoutine;
        private Vector3 baseScale;
        private Renderer[] visualRenderers;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];

        private void Awake()
        {
            baseScale = transform.localScale;
            visualRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private void LateUpdate() => KeepVisualAboveGround();

        public void SetHeld(bool held)
        {
            if (movementRoutine != null)
                StopCoroutine(movementRoutine);
            movementRoutine = held ? StartCoroutine(HeldPulse()) : null;
        }

        /// <summary>
        /// Throw with a real initial velocity (measured from the actual drag
        /// motion — no aim-assist, no added force). Gravity does the rest, so
        /// a ~zero velocity release just drops the treat straight down.
        /// </summary>
        public void Launch(Vector3 initialVelocity, IFeedableDog receiver, float groundY,
            bool deterministicTrajectory = false)
        {
            if (movementRoutine != null)
                StopCoroutine(movementRoutine);
            movementRoutine = StartCoroutine(FlyPhysics(initialVelocity, receiver, groundY,
                deterministicTrajectory));
        }

        public void BeginBeingEaten(Transform mouth)
        {
            if (movementRoutine != null)
                StopCoroutine(movementRoutine);
            movementRoutine = StartCoroutine(ShrinkUnderMouth(mouth));
        }

        /// <summary>Softly removes a treat that the pet did not notice or could not reach.</summary>
        public void BeginDisappearing()
        {
            if (movementRoutine != null)
                StopCoroutine(movementRoutine);
            movementRoutine = StartCoroutine(FadeAfterDelay());
        }

        private IEnumerator HeldPulse()
        {
            float time = 0f;
            while (true)
            {
                time += Time.unscaledDeltaTime;
                float pulse = 1f + Mathf.Sin(time * 7f) * 0.08f;
                transform.localScale = baseScale * pulse;
                transform.Rotate(Vector3.up, 75f * Time.unscaledDeltaTime, Space.World);
                yield return null;
            }
        }

        private IEnumerator FlyPhysics(Vector3 velocity, IFeedableDog receiver, float groundY,
            bool deterministicTrajectory)
        {
            transform.localScale = baseScale;

            if (deterministicTrajectory)
            {
                Vector3 origin = transform.position;
                float duration = ThrowBallistics.TimeToGround(origin, velocity, groundY);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                    transform.position = ThrowBallistics.PositionAtTime(origin, velocity, elapsed);
                    Vector3 currentVelocity = velocity + Physics.gravity * elapsed;
                    transform.Rotate(new Vector3(1f, 0.65f, 0.35f),
                        spinSpeed * Time.deltaTime * Mathf.Clamp01(currentVelocity.magnitude / 3f), Space.Self);
                    yield return null;
                }
            }
            else
            {
                // Preserve the existing AR / standalone demo flight exactly.
                while (transform.position.y > groundY)
                {
                    velocity += Physics.gravity * Time.deltaTime;
                    transform.position += velocity * Time.deltaTime;
                    transform.Rotate(new Vector3(1f, 0.65f, 0.35f),
                        spinSpeed * Time.deltaTime * Mathf.Clamp01(velocity.magnitude / 3f), Space.Self);
                    yield return null;
                }
            }

            Vector3 landed = transform.position;
            landed.y = groundY;
            transform.position = landed;
            // Give the landed treat a deliberate, readable resting pose instead
            // of leaving it at an arbitrary frame of the flight spin.
            transform.rotation = Quaternion.Euler(groundRestEuler);
            movementRoutine = null;
            if (receiver == null || !receiver.TryEat(this))
                BeginDisappearing();
        }

        private IEnumerator ShrinkUnderMouth(Transform mouth)
        {
            Vector3 startScale = transform.localScale;
            float groundY = transform.position.y;
            Quaternion restRotation = transform.rotation;
            float delay = 0f;
            while (delay < eatShrinkDelay)
            {
                delay += Time.deltaTime;
                FollowMouthOnGround(mouth, groundY);
                transform.rotation = restRotation;
                yield return null;
            }

            float elapsed = 0f;

            while (elapsed < eatShrinkDuration)
            {
                elapsed += Time.deltaTime;
                // A linear bite-down remains visibly gradual on small mobile
                // treats; SmoothStep made the final half appear to vanish early.
                float t = Mathf.Clamp01(elapsed / eatShrinkDuration);
                FollowMouthOnGround(mouth, groundY);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                // Keep the asymmetric drumstick visually locked in place while
                // shrinking. Rotating here made it appear to slide behind the dog.
                transform.rotation = restRotation;
                yield return null;
            }
            Destroy(gameObject);
        }

        private void FollowMouthOnGround(Transform mouth, float groundY)
        {
            if (mouth == null)
                return;
            Vector3 target = new Vector3(mouth.position.x, groundY, mouth.position.z);
            float blend = 1f - Mathf.Exp(-mouthFollowSharpness * Mathf.Max(Time.deltaTime, 0.0001f));
            transform.position = Vector3.Lerp(transform.position, target, blend);
        }

        private IEnumerator FadeAfterDelay()
        {
            Vector3 startScale = transform.localScale;
            if (ignoredLingerDuration > 0f)
                yield return new WaitForSeconds(ignoredLingerDuration);

            float elapsed = 0f;
            while (elapsed < ignoredDisappearDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ignoredDisappearDuration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero,
                    t * t * (3f - 2f * t));
                yield return null;
            }
            Destroy(gameObject);
        }

        private void KeepVisualAboveGround()
        {
            if (!TryGetVisualBounds(out Bounds visualBounds))
                return;

            // Probe from well above the treat so this also recovers a held item
            // dragged slightly below the meadow. Ignore the treat itself and
            // rigidbody-backed characters; only stable horizontal surfaces count.
            Vector3 origin = new Vector3(transform.position.x, transform.position.y + 5f, transform.position.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 12f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            bool foundGround = false;
            float highestGround = float.NegativeInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = groundHits[index];
                Collider surface = hit.collider;
                if (surface == null || hit.normal.y < 0.6f)
                    continue;
                Transform surfaceTransform = surface.transform;
                if (surfaceTransform == transform || surfaceTransform.IsChildOf(transform))
                    continue;
                // Another treat is not terrain. Without this check, overlapping
                // treats repeatedly lift one another and appear to bounce away.
                if (surface.GetComponentInParent<ThrownFood>() != null)
                    continue;
                if (surface.attachedRigidbody != null)
                    continue;

                highestGround = Mathf.Max(highestGround, hit.point.y);
                foundGround = true;
            }

            if (!foundGround)
                return;

            float penetration = highestGround + groundClearance - visualBounds.min.y;
            if (penetration > 0f)
                transform.position += Vector3.up * penetration;
        }

        private bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            if (visualRenderers == null)
                return false;

            foreach (Renderer renderer in visualRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is TrailRenderer)
                    continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return hasBounds;
        }
    }
}
