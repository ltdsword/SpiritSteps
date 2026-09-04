using UnityEngine;

namespace ShibaFeeding
{
    /// <summary>Visual-only feedback for the anchored food source; it never moves the thrown world food.</summary>
    public sealed class FoodSourceVisualFeedback : MonoBehaviour
    {
        private const string TutorialSeenKey = "ShibaFeeding.FoodSourceTutorialSeen";

        [SerializeField] private RectTransform iconRoot;
        [SerializeField] private CanvasGroup tutorialGroup;
        [SerializeField] private bool hideTutorialAfterFirstThrow = true;

        [Header("Idle motion")]
        [SerializeField, Min(0f)] private float bobHeight = 3.5f;
        [SerializeField, Min(0f)] private float bobSpeed = 2.1f;
        [SerializeField, Range(0f, 0.08f)] private float pulseAmount = 0.014f;

        [Header("Hold feedback")]
        [SerializeField, Range(1f, 1.2f)] private float heldScale = 1.085f;
        [SerializeField, Min(0.1f)] private float scaleSharpness = 13f;

        private Vector2 baseAnchoredPosition;
        private float currentScale = 1f;
        private float tutorialTargetAlpha;
        private bool held;

        public void Configure(RectTransform visualRoot, CanvasGroup hintGroup)
        {
            iconRoot = visualRoot;
            tutorialGroup = hintGroup;
        }

        private void Awake()
        {
            if (iconRoot == null)
            {
                Transform found = transform.Find("Food Item Visual");
                iconRoot = found as RectTransform;
            }

            if (tutorialGroup == null)
            {
                Transform found = transform.Find("Tutorial Hint");
                if (found != null)
                    tutorialGroup = found.GetComponent<CanvasGroup>();
            }

            if (iconRoot != null)
                baseAnchoredPosition = iconRoot.anchoredPosition;

            bool shouldShowTutorial = !hideTutorialAfterFirstThrow || !PlayerPrefs.HasKey(TutorialSeenKey);
            tutorialTargetAlpha = shouldShowTutorial ? 0.62f : 0f;
            if (tutorialGroup != null)
            {
                tutorialGroup.alpha = tutorialTargetAlpha;
                tutorialGroup.gameObject.SetActive(shouldShowTutorial);
            }
        }

        private void Update()
        {
            if (iconRoot == null)
                return;

            float time = Time.unscaledTime;
            float bob = held ? 0f : Mathf.Sin(time * bobSpeed) * bobHeight;
            iconRoot.anchoredPosition = baseAnchoredPosition + Vector2.up * bob;

            float idlePulse = held ? 0f : Mathf.Sin(time * bobSpeed * 0.82f) * pulseAmount;
            float desiredScale = (held ? heldScale : 1f) + idlePulse;
            currentScale = Mathf.Lerp(currentScale, desiredScale,
                1f - Mathf.Exp(-scaleSharpness * Time.unscaledDeltaTime));
            iconRoot.localScale = Vector3.one * currentScale;

            if (tutorialGroup != null && tutorialGroup.gameObject.activeSelf)
            {
                tutorialGroup.alpha = Mathf.MoveTowards(tutorialGroup.alpha, tutorialTargetAlpha,
                    Time.unscaledDeltaTime * 3.2f);
                if (tutorialTargetAlpha <= 0f && tutorialGroup.alpha <= 0.01f)
                    tutorialGroup.gameObject.SetActive(false);
            }
        }

        public void SetHeld(bool value)
        {
            held = value;
        }

        public void MarkInteractionUsed()
        {
            held = false;
            if (!hideTutorialAfterFirstThrow)
                return;

            PlayerPrefs.SetInt(TutorialSeenKey, 1);
            tutorialTargetAlpha = 0f;
        }

        [ContextMenu("Reset Food Source Tutorial")]
        private void ResetTutorial()
        {
            PlayerPrefs.DeleteKey(TutorialSeenKey);
            tutorialTargetAlpha = 0.62f;
            if (tutorialGroup != null)
                tutorialGroup.gameObject.SetActive(true);
        }
    }
}
