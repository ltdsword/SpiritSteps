using System.Collections;
using UnityEngine;
using ShibaFeeding;

namespace CorgiAR
{
    /// <summary>
    /// "Throw the ball" fetch loop, on the companion wrapper. Ports the run-to /
    /// carry pattern from <see cref="DogFeedingController"/> but the toy is carried
    /// in the pet's mouth back to the player and dropped instead of eaten.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToyFetchController : MonoBehaviour, IThrowTarget, IThrowBoundary
    {
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private DogFeedingController feeding;
        [SerializeField] private Transform carryBone;
        [SerializeField] private Camera playerCamera;

        [Header("Feel")]
        [SerializeField] private Vector3 throwAnchorOffset = new(0f, 0.03f, 0.4f);
        [Tooltip("Where the carried toy sits, in the pet's local frame (Z = toward the snout).")]
        [SerializeField] private Vector3 mouthOffset = new(0f, -0.03f, 0.10f);
        [SerializeField, Min(0.1f)] private float reachDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float returnDistance = 0.85f;
        [SerializeField, Min(0.1f)] private float toyAwarenessRadius = 5.5f;
        [SerializeField, Min(0.5f)] private float approachStallTimeout = 1.5f;
        [Tooltip("The pet keeps turning naturally before it releases the ball.")]
        [SerializeField, Range(1f, 45f)] private float returnFacingTolerance = 12f;

        private Coroutine fetchRoutine;
        private Transform returnAnchor;
        private ThrownToy carriedToy;
        private Vector3 capturedDesktopReturnPoint;
        private bool hasCapturedDesktopReturnPoint;

        public bool IsBusy => fetchRoutine != null;
        public bool IsThrowBoundaryActive => companion != null && companion.UsesMovementBounds;
        public float ThrowPreviewGroundY => IsThrowBoundaryActive
            ? companion.PlayArea.GroundY
            : transform.position.y;

        public void SetCamera(Camera camera) => playerCamera = camera;
        public void RebindCarry(Transform bone) => carryBone = bone;

        private void Awake()
        {
            if (companion == null) companion = GetComponent<DogCompanionController>();
            if (feeding == null) feeding = GetComponent<DogFeedingController>();
            returnAnchor = new GameObject("Toy Return Anchor").transform;
        }

        public Vector3 GetThrowAnchorPoint() => transform.TransformPoint(throwAnchorOffset);

        public Vector3 ConstrainHeldPosition(Camera camera, Vector3 desiredPosition, float footprintRadius) =>
            IsThrowBoundaryActive
                ? companion.PlayArea.ConstrainHeldPosition(camera, desiredPosition, footprintRadius)
                : desiredPosition;

        public Vector3 ConstrainLaunchVelocity(Camera camera, Vector3 origin,
            Vector3 initialVelocity, float landingY, float footprintRadius,
            out Vector3 predictedLanding, out bool wasLimited)
        {
            if (IsThrowBoundaryActive)
                return companion.PlayArea.ConstrainLaunchVelocity(camera, origin, initialVelocity,
                    landingY, footprintRadius, out predictedLanding, out wasLimited);
            predictedLanding = ThrowBallistics.LandingPoint(origin, initialVelocity, landingY);
            wasLimited = false;
            return initialVelocity;
        }

        public void SetThrowAimActive(bool active)
        {
            if (IsThrowBoundaryActive)
                companion.PlayArea.SetThrowAimActive(active);
        }

        /// <summary>Ground height the toy should be thrown along.</summary>
        public float GroundY => transform.position.y;

        // Keep the carried toy glued to the pet's mouth every frame (rig-agnostic:
        // anchored on the carry bone's position + an offset in the pet's own frame).
        private void LateUpdate()
        {
            if (carriedToy == null)
                return;
            SnapCarriedToyToMouth();
        }

        private void SnapCarriedToyToMouth()
        {
            Transform anchor = carryBone != null ? carryBone : transform;
            carriedToy.transform.position = anchor.position + transform.TransformVector(mouthOffset);
            carriedToy.transform.rotation = transform.rotation;
        }

        public void BeginAim(Transform heldToy)
        {
            if (IsBusy || heldToy == null || companion == null)
                return;

            // Desktop camera follows the pet, so remember the user's position
            // before the pet starts chasing. The anchor must not be parented to
            // the pet or it will move along with the fetcher.
            if (companion.UsesMovementBounds)
            {
                capturedDesktopReturnPoint = CameraGround();
                hasCapturedDesktopReturnPoint = true;
            }
            companion.ChaseTarget(heldToy, run: false, stopDistance: 0.5f);
        }

        public void EndAim()
        {
            if (!IsBusy)
                companion?.StopChasing();
        }

        public bool TryFetch(ThrownToy toy)
        {
            if (IsBusy || toy == null || (feeding != null && feeding.IsEating))
                return false;
            if (Planar(transform.position, toy.transform.position) > toyAwarenessRadius ||
                (companion != null && !companion.CanReach(toy.transform.position)))
                return false;
            if (companion != null && companion.UsesMovementBounds && !hasCapturedDesktopReturnPoint)
            {
                capturedDesktopReturnPoint = CameraGround();
                hasCapturedDesktopReturnPoint = true;
            }
            fetchRoutine = StartCoroutine(FetchSequence(toy));
            return true;
        }

        private IEnumerator FetchSequence(ThrownToy toy)
        {
            // 1. run out to the toy. Chasing itself is clamped to the play-area
            // boundary (DogCompanionController), so a ball thrown beyond it is
            // never actually reached — if the guard fires without the pet
            // closing the distance, the ball is out of bounds and stays where
            // it landed rather than being force-fetched from afar.
            companion?.ChaseTarget(toy.transform, run: true, stopDistance: 0.3f);
            float stallRemaining = approachStallTimeout;
            float bestDistance = Planar(transform.position, toy.transform.position);
            while (stallRemaining > 0f && toy != null &&
                   Planar(transform.position, toy.transform.position) > reachDistance)
            {
                float currentDistance = Planar(transform.position, toy.transform.position);
                if (currentDistance < bestDistance - 0.01f)
                {
                    bestDistance = currentDistance;
                    stallRemaining = approachStallTimeout;
                }
                else
                {
                    stallRemaining -= Time.deltaTime;
                }
                yield return null;
            }
            companion?.StopChasing();
            bool reached = toy != null && Planar(transform.position, toy.transform.position) <= reachDistance;
            if (!reached)
            {
                if (toy != null)
                    toy.BeginResting();
                hasCapturedDesktopReturnPoint = false;
                fetchRoutine = null;
                yield break;
            }

            // 2. pick it up — parked on the mouth by LateUpdate
            toy.SetCarried(true);
            carriedToy = toy;
            toy.transform.SetParent(carryBone != null ? carryBone : transform, worldPositionStays: true);
            companion?.BeginInteraction(0.45f, DogAnimationState.WigglingTail);
            yield return new WaitForSeconds(0.45f);

            // 3. In desktop preview return to the position captured before the
            // follow camera moved. In AR, keep tracking the real camera/player.
            bool stableReturn = companion != null && companion.UsesMovementBounds &&
                                hasCapturedDesktopReturnPoint;
            returnAnchor.position = stableReturn ? capturedDesktopReturnPoint : CameraGround();
            companion?.ChaseTarget(returnAnchor, run: true, stopDistance: returnDistance);
            float guard = 5f;
            while (guard > 0f &&
                   (Planar(transform.position, returnAnchor.position) > returnDistance ||
                    !IsFacing(returnAnchor.position)))
            {
                if (!stableReturn)
                    returnAnchor.position = CameraGround();
                guard -= Time.deltaTime;
                yield return null;
            }
            companion?.StopChasing();
            if (toy == null)
            {
                carriedToy = null;
                companion?.BeginInteraction(0.6f, DogAnimationState.WigglingTail);
                hasCapturedDesktopReturnPoint = false;
                fetchRoutine = null;
                yield break;
            }

            // 4. drop it and wag
            SnapCarriedToyToMouth();
            carriedToy = null;
            toy.transform.SetParent(null, worldPositionStays: true);
            Vector3 dropPos = toy.transform.position;
            dropPos.y = transform.position.y;
            toy.transform.position = dropPos;
            toy.SetCarried(false);
            companion?.BeginInteraction(1f, DogAnimationState.WigglingTail);
            hasCapturedDesktopReturnPoint = false;
            fetchRoutine = null;
        }

        private void OnDestroy()
        {
            if (returnAnchor != null)
                Destroy(returnAnchor.gameObject);
        }

        private Vector3 CameraGround()
        {
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            if (cam == null)
                return transform.position;
            Vector3 p = cam.transform.position;
            p.y = transform.position.y;
            return companion != null ? companion.GetReachablePoint(p) : p;
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private bool IsFacing(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return true;
            return Vector3.Angle(transform.forward, direction) <= returnFacingTolerance;
        }
    }
}
