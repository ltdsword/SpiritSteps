using System;
using System.Collections;
using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Grows the currently selected pet after completed meals. The authored model
    /// size is the Young baseline; only the runtime "Pet Visual" root is scaled,
    /// so imported FBXs and rig bones remain untouched.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class PetGrowthController : MonoBehaviour
    {
        [Header("Growth Sources")]
        [SerializeField] private DogFeedingController feeding;
        [SerializeField] private PetBinder binder;
        [SerializeField] private DogGroundAligner groundAligner;

        [Header("Chicken Requirements")]
        [Tooltip("Completed chicken meals required to grow from Baby to Young.")]
        [SerializeField, Min(1)] private int chickensForYoung = 5;
        [Tooltip("Additional completed chicken meals required to grow from Young to Adult.")]
        [SerializeField, Min(1)] private int additionalChickensForAdult = 10;

        [Header("Scale (Young is the authored model size)")]
        [SerializeField, Range(0.5f, 1f)] private float babyScale = 0.8f;
        [SerializeField, Range(0.8f, 1.2f)] private float youngScale = 1f;
        [SerializeField, Range(1f, 1.5f)] private float adultScale = 1.22f;
        [SerializeField, Min(0f)] private float growthTransitionSeconds = 0.7f;

        [Header("Runtime Test Progress")]
        [Tooltip("Starts at zero so entering Play Mode always begins at Baby for this prototype.")]
        [SerializeField, Min(0)] private int consumedChickenCount;

        private Transform trackedVisual;
        private Vector3 authoredYoungScale;
        private Coroutine transitionRoutine;

        public event Action<PetGrowthStage> StageChanged;

        public int ConsumedChickenCount => consumedChickenCount;
        public int ChickenUntilNextStage => PetGrowthMath.ChickenUntilNextStage(
            consumedChickenCount, chickensForYoung, additionalChickensForAdult);
        public PetGrowthStage CurrentStage => PetGrowthMath.StageForChickenCount(
            consumedChickenCount, chickensForYoung, additionalChickensForAdult);

        private void Awake()
        {
            if (feeding == null) feeding = GetComponent<DogFeedingController>();
            if (binder == null) binder = GetComponent<PetBinder>();
            if (groundAligner == null) groundAligner = GetComponent<DogGroundAligner>();
        }

        private void OnEnable()
        {
            if (feeding != null) feeding.Fed += OnFed;
            if (binder != null) binder.PetChanged += OnPetChanged;
        }

        private void Start()
        {
            ApplyCurrentStage(false);
        }

        private void OnDisable()
        {
            if (feeding != null) feeding.Fed -= OnFed;
            if (binder != null) binder.PetChanged -= OnPetChanged;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }
        }

        private void OnFed()
        {
            PetGrowthStage before = CurrentStage;
            consumedChickenCount++;
            PetGrowthStage after = CurrentStage;
            if (after == before)
                return;

            ApplyCurrentStage(true);
            StageChanged?.Invoke(after);
        }

        private void OnPetChanged(string unusedId)
        {
            trackedVisual = null;
            ApplyCurrentStage(false);
        }

        [ContextMenu("Reset Growth To Baby")]
        public void ResetToBaby()
        {
            PetGrowthStage before = CurrentStage;
            consumedChickenCount = 0;
            ApplyCurrentStage(false);
            if (before != PetGrowthStage.Baby)
                StageChanged?.Invoke(PetGrowthStage.Baby);
        }

        /// <summary>Useful for test UI and future save-game restoration.</summary>
        public void SetConsumedChickenCount(int value, bool animateStageChange = false)
        {
            PetGrowthStage before = CurrentStage;
            consumedChickenCount = Mathf.Max(0, value);
            PetGrowthStage after = CurrentStage;
            ApplyCurrentStage(animateStageChange && after != before);
            if (after != before)
                StageChanged?.Invoke(after);
        }

        private void ApplyCurrentStage(bool animate)
        {
            Transform visual = binder != null ? binder.CurrentVisual : transform.Find("Pet Visual");
            if (visual == null)
                return;

            if (visual != trackedVisual)
            {
                trackedVisual = visual;
                authoredYoungScale = visual.localScale;
            }

            float multiplier = ScaleFor(CurrentStage);
            Vector3 targetScale = authoredYoungScale * multiplier;

            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (!animate || growthTransitionSeconds <= 0f || !isActiveAndEnabled)
            {
                visual.localScale = targetScale;
                AlignVisual();
                return;
            }

            transitionRoutine = StartCoroutine(GrowVisual(visual, targetScale));
        }

        private IEnumerator GrowVisual(Transform visual, Vector3 targetScale)
        {
            Vector3 startScale = visual.localScale;
            Vector3 startPosition = visual.localPosition;
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            Vector3 startCapsuleCenter = capsule.center;
            float startCapsuleRadius = capsule.radius;
            float startCapsuleHeight = capsule.height;

            // Measure the final grounded pose once, then interpolate the cached
            // values. This avoids baking the skinned mesh on every tween frame.
            visual.localScale = targetScale;
            AlignVisual();
            Vector3 targetPosition = visual.localPosition;
            Vector3 targetCapsuleCenter = capsule.center;
            float targetCapsuleRadius = capsule.radius;
            float targetCapsuleHeight = capsule.height;

            visual.localScale = startScale;
            visual.localPosition = startPosition;
            capsule.center = startCapsuleCenter;
            capsule.radius = startCapsuleRadius;
            capsule.height = startCapsuleHeight;
            Physics.SyncTransforms();

            float elapsed = 0f;
            while (elapsed < growthTransitionSeconds && visual != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(elapsed / Mathf.Max(0.001f, growthTransitionSeconds)));
                visual.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                visual.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, t);
                capsule.center = Vector3.LerpUnclamped(startCapsuleCenter, targetCapsuleCenter, t);
                capsule.radius = Mathf.LerpUnclamped(startCapsuleRadius, targetCapsuleRadius, t);
                capsule.height = Mathf.LerpUnclamped(startCapsuleHeight, targetCapsuleHeight, t);
                yield return null;
            }

            if (visual != null)
            {
                visual.localScale = targetScale;
                visual.localPosition = targetPosition;
                capsule.center = targetCapsuleCenter;
                capsule.radius = targetCapsuleRadius;
                capsule.height = targetCapsuleHeight;
                Physics.SyncTransforms();
            }
            transitionRoutine = null;
        }

        private void AlignVisual()
        {
            if (groundAligner == null || trackedVisual == null)
                return;
            groundAligner.Rebind(trackedVisual);
            groundAligner.Align();
        }

        private float ScaleFor(PetGrowthStage stage)
        {
            return stage switch
            {
                PetGrowthStage.Baby => babyScale,
                PetGrowthStage.Adult => adultScale,
                _ => youngScale
            };
        }
    }
}
