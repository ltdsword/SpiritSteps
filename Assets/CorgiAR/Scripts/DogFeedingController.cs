using System;
using System.Collections;
using UnityEngine;
using ShibaFeeding;

namespace CorgiAR
{
    /// <summary>
    /// CorgiAR side of the shared drag-throw feeding flow. Implements
    /// <see cref="IFeedableDog"/> so <see cref="FoodDragThrowUI"/> / <see cref="ThrownFood"/>
    /// (ported from the ShibaFeeding demo) can throw a treat to the AR pet: the pet
    /// follows the held treat, runs to where it lands, plays the Eating chain, and
    /// pops a "NGON QUÁ" bubble before resuming its mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DogFeedingController : MonoBehaviour, IFeedableDog
    {
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private DogAnimatorAdapter animatorAdapter;
        [SerializeField] private Transform mouthBone;

        [Header("Feel")]
        // The treat mesh is roughly 0.28 units tall. Keeping its centre above
        // the ground prevents the lower half from visually clipping the meadow.
        [SerializeField] private Vector3 foodLandingOffset = new(0f, 0.15f, 0.32f);
        [SerializeField, Min(0f)] private float headMouthForwardOffset = 0.11f;
        [SerializeField, Min(0f)] private float headMouthDownOffset = 0.045f;
        [SerializeField, Min(0.2f)] private float chewDuration = 2.6f;
        [SerializeField, Min(0.1f)] private float endDuration = 1.1f;
        [SerializeField] private Color popupColor = new(1f, 0.45f, 0.12f);
        [SerializeField] private string popupText = "NGON QUÁ!  ♥";

        private Coroutine eatRoutine;

        public bool IsEating { get; private set; }

        /// <summary>Raised once the pet finishes a treat (used by <see cref="PetMoodController"/>).</summary>
        public event Action Fed;

        private void Awake()
        {
            if (companion == null) companion = GetComponent<DogCompanionController>();
            if (animatorAdapter == null) animatorAdapter = GetComponent<DogAnimatorAdapter>();
            ResolveMouthIfNeeded();
        }

        /// <summary>Re-point the mouth bone after the visible pet was swapped.</summary>
        public void RebindCarry(Transform newMouthBone) => mouthBone = newMouthBone;

        public Vector3 GetFoodLandingPoint() => transform.TransformPoint(foodLandingOffset);

        public void BeginFollowingHeldFood(Transform heldFood)
        {
            if (IsEating || companion == null || heldFood == null)
                return;
            companion.ChaseTarget(heldFood, run: false, stopDistance: 0.45f);
        }

        public void EndFollowingHeldFood()
        {
            if (!IsEating)
                companion?.StopChasing();
        }

        public bool TryEat(ThrownFood food)
        {
            if (IsEating || food == null)
                return false;
            ResolveMouthIfNeeded();
            if (eatRoutine != null)
                StopCoroutine(eatRoutine);
            eatRoutine = StartCoroutine(EatSequence(food));
            return true;
        }

        private IEnumerator EatSequence(ThrownFood food)
        {
            IsEating = true;

            // Run to the treat if it landed out of reach. Chasing itself is
            // clamped to the play-area boundary (DogCompanionController), so a
            // treat thrown beyond it is never actually reached — the guard here
            // only protects against never triggering; when it fires without the
            // pet closing the distance, the treat is out of bounds and stays
            // uneaten rather than being force-eaten from afar.
            if (companion != null)
            {
                companion.ChaseTarget(food.transform, run: true, stopDistance: 0.3f);
                float guard = 4f;
                while (guard > 0f && food != null &&
                       PlanarDistance(transform.position, food.transform.position) > 0.34f)
                {
                    guard -= Time.deltaTime;
                    yield return null;
                }
                companion.StopChasing();

                bool reached = food != null &&
                               PlanarDistance(transform.position, food.transform.position) <= 0.34f;
                if (!reached)
                {
                    IsEating = false;
                    eatRoutine = null;
                    yield break;
                }
            }

            float total = chewDuration + endDuration;
            float eatingSpeed = 1f;
            if (animatorAdapter != null &&
                animatorAdapter.TryGetSingleEatingClip(out AnimationClip eatingClip) &&
                !eatingClip.isLooping)
            {
                // Some UAA clips (notably Shiba) contain the complete down/eat/up
                // action. Fit one pass to the interaction instead of looping the
                // head-up ending into a second, abruptly cut-off bite.
                const float transitionAllowance = 0.18f;
                eatingSpeed = eatingClip.length /
                              Mathf.Max(0.1f, total - transitionAllowance);
            }
            companion?.BeginInteraction(total, DogAnimationState.Eating);
            animatorAdapter?.SetPlaybackSpeed(eatingSpeed);
            if (food != null)
                food.BeginBeingEaten(mouthBone != null ? mouthBone : transform);

            yield return new WaitForSeconds(0.5f);
            SpawnPopup();
            yield return new WaitForSeconds(Mathf.Max(0f, total - 0.5f));

            animatorAdapter?.SetPlaybackSpeed(1f);
            IsEating = false;
            eatRoutine = null;
            Fed?.Invoke();
        }

        private void SpawnPopup()
        {
            ResolveMouthIfNeeded();
            Vector3 position = mouthBone != null
                ? mouthBone.position + Vector3.up * 0.12f
                : transform.position + Vector3.up * 0.5f;
            var popup = new GameObject("Yum Popup");
            popup.transform.position = position;
            popup.AddComponent<JuicyPopup>().Initialize(popupText, popupColor, Camera.main);
        }

        /// <summary>Refresh the mouth anchor whenever PetBinder swaps the runtime visual.</summary>
        public void RebindMouth(Transform visualRoot)
        {
            Transform resolved = FindDeep(visualRoot, "DEF-jaw_master")
                                 ?? FindDeep(visualRoot, "Head_end");
            if (resolved != null)
            {
                mouthBone = resolved;
                return;
            }

            // Some pet families expose only a Head bone. Create a stable child
            // anchor at the snout rather than treating the skull centre as mouth.
            Transform head = FindDeep(visualRoot, "DEF-spine.006")
                             ?? FindDeep(visualRoot, "Head");
            if (head == null)
            {
                mouthBone = null;
                return;
            }

            Transform anchor = head.Find("Food Mouth Anchor");
            if (anchor == null)
            {
                GameObject anchorObject = new GameObject("Food Mouth Anchor");
                anchor = anchorObject.transform;
                anchor.SetParent(head, true);
            }
            anchor.position = head.position + transform.forward * headMouthForwardOffset -
                              Vector3.up * headMouthDownOffset;
            anchor.rotation = transform.rotation;
            mouthBone = anchor;
        }

        private void ResolveMouthIfNeeded()
        {
            if (mouthBone != null)
                return;
            Transform visual = transform.Find("Pet Visual");
            RebindMouth(visual != null ? visual : transform);
        }

        private static Transform FindDeep(Transform root, string targetName)
        {
            if (root == null)
                return null;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == targetName)
                    return children[i];
            }
            return null;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
