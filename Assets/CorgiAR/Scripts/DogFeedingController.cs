using System;
using System.Collections;
using UnityEngine;
using ARWalking.UI;
using ShibaFeeding;

namespace CorgiAR
{
    /// <summary>
    /// CorgiAR side of the shared drag-throw feeding flow. Implements
    /// <see cref="IFeedableDog"/> so <see cref="FoodDragThrowUI"/> / <see cref="ThrownFood"/>
    /// (ported from the ShibaFeeding demo) can throw a treat to the AR pet: the pet
    /// follows the held treat, runs to where it lands, plays the Eating chain, and
    /// pops a "YUM" bubble before resuming its mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DogFeedingController : MonoBehaviour, IFeedableDog, IThrowBoundary
    {
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private DogAnimatorAdapter animatorAdapter;
        [SerializeField] private PetBinder binder;
        [SerializeField] private Transform mouthBone;

        [Header("Feel")]
        // The treat mesh is roughly 0.28 units tall. Keeping its centre above
        // the ground prevents the lower half from visually clipping the meadow.
        [SerializeField] private Vector3 foodLandingOffset = new(0f, 0.15f, 0.32f);
        [SerializeField, Min(0f)] private float headMouthForwardOffset = 0.11f;
        [SerializeField, Min(0f)] private float headMouthDownOffset = 0.045f;
        [Tooltip("The pet reacts only when a landed treat is this close on the ground plane.")]
        [SerializeField, Min(0.1f)] private float foodAwarenessRadius = 5.5f;
        [Tooltip("Distance at which the pet can take the treat without moving closer.")]
        [SerializeField, Min(0.05f)] private float foodReachDistance = 0.38f;
        [Tooltip("Abort only after the pet has made no progress for this long.")]
        [SerializeField, Min(0.5f)] private float approachStallTimeout = 1.5f;
        [SerializeField, Min(0.2f)] private float chewDuration = 2.6f;
        [SerializeField, Min(0.1f)] private float endDuration = 1.1f;
        [SerializeField] private Color popupColor = new(1f, 0.45f, 0.12f);
        [SerializeField] private string popupText = "YUM!  ♥";

        private Coroutine eatRoutine;

        public bool IsEating { get; private set; }
        public bool IsThrowBoundaryActive => companion != null && companion.UsesMovementBounds;
        public float ThrowPreviewGroundY => IsThrowBoundaryActive
            ? companion.PlayArea.GroundY
            : transform.position.y;

        /// <summary>Raised once the pet finishes a treat (used by <see cref="PetMoodController"/>).</summary>
        public event Action Fed;

        private void Awake()
        {
            if (companion == null) companion = GetComponent<DogCompanionController>();
            if (animatorAdapter == null) animatorAdapter = GetComponent<DogAnimatorAdapter>();
            if (binder == null) binder = GetComponent<PetBinder>();
            ResolveMouthIfNeeded();
        }

        /// <summary>Re-point the mouth bone after the visible pet was swapped.</summary>
        public void RebindCarry(Transform newMouthBone) => mouthBone = newMouthBone;

        public Vector3 GetFoodLandingPoint() => transform.TransformPoint(foodLandingOffset);

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

            float distance = PlanarDistance(transform.position, food.transform.position);
            if (distance > foodAwarenessRadius ||
                (companion != null && !companion.CanReach(food.transform.position)))
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
            // Captured now, not read from food.FoodId at the end: BeginBeingEaten's shrink
            // coroutine destroys the treat only slightly before this sequence finishes, so a
            // live read there would race Destroy() and could silently skip the EXP grant.
            string foodId = food.FoodId;

            // A treat at the mouth proceeds immediately. Otherwise the pet runs
            // over, but only after TryEat has confirmed it noticed and can reach it.
            if (companion != null)
            {
                companion.ChaseTarget(food.transform, run: true,
                    stopDistance: Mathf.Max(0.05f, foodReachDistance - 0.04f));
                float stallRemaining = approachStallTimeout;
                float bestDistance = PlanarDistance(transform.position, food.transform.position);
                while (stallRemaining > 0f && food != null &&
                       PlanarDistance(transform.position, food.transform.position) > foodReachDistance)
                {
                    float currentDistance = PlanarDistance(transform.position, food.transform.position);
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
                companion.StopChasing();

                bool reached = food != null &&
                               PlanarDistance(transform.position, food.transform.position) <= foodReachDistance;
                if (!reached)
                {
                    if (food != null)
                        food.BeginDisappearing();
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
            animatorAdapter?.RestartEatingAnimation();
            if (food != null)
                food.BeginBeingEaten(mouthBone != null ? mouthBone : transform);

            // The pet has now committed to this treat - nothing from here on can fail it. Grant the
            // reward now rather than after the closing animation wait below: the food's inventory
            // unit was already spent at pickup (see FoodDragThrowUI.ConsumeSelectedFood), and backing
            // out of AR/3D mode destroys this coroutine mid-wait, which used to silently skip the EXP
            // while still leaving the item spent.
            GrantFeedExperience(foodId);
            Fed?.Invoke();

            yield return new WaitForSeconds(0.5f);
            SpawnPopup();
            yield return new WaitForSeconds(Mathf.Max(0f, total - 0.5f));

            animatorAdapter?.SetPlaybackSpeed(1f);
            IsEating = false;
            eatRoutine = null;
        }

        /// <summary>Credits the food's Growth EXP to the companion actually being fed (the one bound
        /// in this AR/3D scene), not the player's lead/walking companion - those can differ whenever
        /// the player is viewing a non-lead pet. Falls back to the lead companion only if no binder
        /// is wired up (e.g. a test double).</summary>
        private void GrantFeedExperience(string foodId)
        {
            if (string.IsNullOrEmpty(foodId))
                return;
            UiPrototypeRuntime runtime = UiPrototypeRuntime.Instance;
            if (runtime == null)
                return;
            string companionId = binder != null && !string.IsNullOrEmpty(binder.CurrentId)
                ? binder.CurrentId
                : runtime.PrimaryCompanionId();
            runtime.GrantFeedExperience(foodId, companionId);
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
