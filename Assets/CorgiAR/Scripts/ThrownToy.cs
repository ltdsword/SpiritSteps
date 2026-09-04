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

        private void Awake() => baseScale = transform.localScale;

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
        public void Launch(Vector3 initialVelocity, IThrowTarget receiver, float groundY)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FlyPhysics(initialVelocity, receiver, groundY));
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
                routine = StartCoroutine(RollToRest());
            }
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

        private IEnumerator FlyPhysics(Vector3 velocity, IThrowTarget receiver, float groundY)
        {
            transform.localScale = baseScale;

            while (transform.position.y > groundY)
            {
                velocity += Physics.gravity * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                transform.Rotate(new Vector3(1f, 0.6f, 0.3f),
                    spinSpeed * Time.deltaTime * Mathf.Clamp01(velocity.magnitude / 3f), Space.Self);
                yield return null;
            }

            Vector3 landed = transform.position;
            landed.y = groundY;
            transform.position = landed;
            routine = null;
            if (receiver == null || !receiver.TryFetch(this))
                routine = StartCoroutine(RollToRest());
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

    }
}
