using System.Collections;
using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// "Throw the ball" fetch loop, on the companion wrapper. Ports the run-to /
    /// carry pattern from <see cref="DogFeedingController"/> but the toy is carried
    /// in the pet's mouth back to the player and dropped instead of eaten.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToyFetchController : MonoBehaviour, IThrowTarget
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

        private Coroutine fetchRoutine;
        private Transform returnAnchor;
        private ThrownToy carriedToy;

        public bool IsBusy => fetchRoutine != null;

        public void SetCamera(Camera camera) => playerCamera = camera;
        public void RebindCarry(Transform bone) => carryBone = bone;

        private void Awake()
        {
            if (companion == null) companion = GetComponent<DogCompanionController>();
            if (feeding == null) feeding = GetComponent<DogFeedingController>();
            returnAnchor = new GameObject("Toy Return Anchor").transform;
            returnAnchor.SetParent(transform, false);
        }

        public Vector3 GetThrowAnchorPoint() => transform.TransformPoint(throwAnchorOffset);

        /// <summary>Ground height the toy should be thrown along.</summary>
        public float GroundY => transform.position.y;

        // Keep the carried toy glued to the pet's mouth every frame (rig-agnostic:
        // anchored on the carry bone's position + an offset in the pet's own frame).
        private void LateUpdate()
        {
            if (carriedToy == null)
                return;
            Transform anchor = carryBone != null ? carryBone : transform;
            carriedToy.transform.position = anchor.position + transform.TransformVector(mouthOffset);
            carriedToy.transform.rotation = transform.rotation;
        }

        public void BeginAim(Transform heldToy)
        {
            if (IsBusy || heldToy == null || companion == null)
                return;
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
            float guard = 4f;
            while (guard > 0f && toy != null &&
                   Planar(transform.position, toy.transform.position) > reachDistance)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
            companion?.StopChasing();
            bool reached = toy != null && Planar(transform.position, toy.transform.position) <= reachDistance;
            if (!reached)
            {
                fetchRoutine = null;
                yield break;
            }

            // 2. pick it up — parked on the mouth by LateUpdate
            toy.SetCarried(true);
            carriedToy = toy;
            toy.transform.SetParent(carryBone != null ? carryBone : transform, worldPositionStays: true);
            companion?.BeginInteraction(0.45f, DogAnimationState.WigglingTail);
            yield return new WaitForSeconds(0.45f);

            // 3. carry it back to the player, who may be moving
            returnAnchor.position = CameraGround();
            companion?.ChaseTarget(returnAnchor, run: true, stopDistance: returnDistance);
            guard = 5f;
            while (guard > 0f && Planar(transform.position, returnAnchor.position) > returnDistance)
            {
                returnAnchor.position = CameraGround();
                guard -= Time.deltaTime;
                yield return null;
            }
            companion?.StopChasing();
            if (toy == null)
            {
                carriedToy = null;
                companion?.BeginInteraction(0.6f, DogAnimationState.WigglingTail);
                fetchRoutine = null;
                yield break;
            }

            // 4. drop it and wag
            carriedToy = null;
            toy.transform.SetParent(null, worldPositionStays: true);
            Vector3 dropPos = toy.transform.position;
            dropPos.y = transform.position.y;
            toy.transform.position = dropPos;
            toy.SetCarried(false);
            companion?.BeginInteraction(1f, DogAnimationState.WigglingTail);
            fetchRoutine = null;
        }

        private Vector3 CameraGround()
        {
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            if (cam == null)
                return transform.position;
            Vector3 p = cam.transform.position;
            p.y = transform.position.y;
            return p;
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
