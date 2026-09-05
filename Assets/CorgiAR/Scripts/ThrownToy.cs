using System.Collections;
using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// A play toy (ball) thrown from the HUD. Flies under real gravity from the
    /// velocity measured off the player's actual drag (see
    /// <see cref="ShibaFeeding.ThrownFood"/> for the food equivalent); on landing
    /// it asks the <see cref="IThrowTarget"/> to fetch it. Unlike food it is not
    /// consumed — after being dropped it just rolls to a stop and lingers a while.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class ThrownToy : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float spinSpeed = 640f;
        [SerializeField, Min(1f)] private float lingerSeconds = 6f;
        [SerializeField, Range(0.2f, 1f)] private float carriedScale = 0.62f;

        private Coroutine routine;
        private Vector3 baseScale;
        private Renderer[] visualRenderers;
        private float restingGroundY;

        private void Awake()
        {
            baseScale = transform.localScale;
            visualRenderers = GetComponentsInChildren<Renderer>(true);
        }

        public void SetHeld(bool held)
        {
            if (routine != null) StopCoroutine(routine);
            routine = held ? StartCoroutine(HeldPulse()) : null;
        }

        /// <summary>
        /// Throw with a real initial velocity (measured from the actual drag
        /// motion — no aim-assist, no added force). Gravity does the rest, so
        /// a ~zero velocity release just drops the ball straight down.
        /// </summary>
        public void Launch(Vector3 initialVelocity, IThrowTarget receiver, float groundY,
            bool deterministicTrajectory = false)
        {
            if (routine != null) StopCoroutine(routine);
            restingGroundY = groundY;
            routine = StartCoroutine(FlyPhysics(initialVelocity, receiver, groundY,
                deterministicTrajectory));
        }

        /// <summary>Pet picked the toy up — freeze its own motion while it's carried.</summary>
        public void SetCarried(bool carried)
        {
            if (carried)
            {
                if (routine != null) StopCoroutine(routine);
                routine = null;
                transform.localScale = baseScale * carriedScale;
            }
            else
            {
                transform.localScale = baseScale;
                restingGroundY = transform.position.y;
                PlaceOnGround(restingGroundY);
                routine = StartCoroutine(RollToRest());
            }
        }

        /// <summary>Leaves an abandoned/unreachable ball visible briefly, then removes it.</summary>
        public void BeginResting()
        {
            if (routine != null)
                StopCoroutine(routine);
            PlaceOnGround(restingGroundY);
            routine = StartCoroutine(RollToRest());
        }

        private IEnumerator HeldPulse()
        {
            float time = 0f;
            while (true)
            {
                time += Time.unscaledDeltaTime;
                transform.localScale = baseScale * (1f + Mathf.Sin(time * 7f) * 0.07f);
                transform.Rotate(Vector3.up, 70f * Time.unscaledDeltaTime, Space.World);
                yield return null;
            }
        }

        private IEnumerator FlyPhysics(Vector3 velocity, IThrowTarget receiver, float groundY,
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
                    transform.Rotate(new Vector3(1f, 0.6f, 0.3f),
                        spinSpeed * Time.deltaTime * Mathf.Clamp01(currentVelocity.magnitude / 3f), Space.Self);
                    yield return null;
                }
            }
            else
            {
                // Preserve the existing AR flight exactly.
                while (transform.position.y > groundY)
                {
                    velocity += Physics.gravity * Time.deltaTime;
                    transform.position += velocity * Time.deltaTime;
                    transform.Rotate(new Vector3(1f, 0.6f, 0.3f),
                        spinSpeed * Time.deltaTime * Mathf.Clamp01(velocity.magnitude / 3f), Space.Self);
                    yield return null;
                }
            }

            Vector3 landed = transform.position;
            landed.y = groundY;
            transform.position = landed;
            PlaceOnGround(groundY);
            routine = null;
            if (receiver == null || !receiver.TryFetch(this))
                BeginResting();
        }

        private IEnumerator RollToRest()
        {
            float elapsed = 0f;
            Vector3 start = transform.position;
            while (elapsed < 0.6f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.6f;
                transform.position = start + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.12f);
                transform.Rotate(Vector3.right, 220f * Time.deltaTime, Space.World);
                yield return null;
            }
            yield return new WaitForSeconds(lingerSeconds);
            Destroy(gameObject);
        }

        private void PlaceOnGround(float groundY)
        {
            if (!TryGetVisualBounds(out Bounds visualBounds))
                return;
            transform.position += Vector3.up * (groundY - visualBounds.min.y);
        }

        private bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            if (visualRenderers == null)
                visualRenderers = GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in visualRenderers)
            {
                if (renderer == null || renderer is TrailRenderer || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

    }
}
