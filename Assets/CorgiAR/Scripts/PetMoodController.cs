using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Simple hunger-driven mood. Hunger rises over time and resets when the pet
    /// eats (<see cref="DogFeedingController.Fed"/>). A hungry pet moves slower and
    /// is more likely to flop down and rest — the modifiers are pushed into
    /// <see cref="DogCompanionController.SetMoodModifiers"/>. <see cref="ForceHungry"/>
    /// pins hunger to full for the HUD "đói" demo button.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetMoodController : MonoBehaviour
    {
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private DogFeedingController feeding;

        [Tooltip("Seconds for hunger to climb from 0 to full.")]
        [SerializeField, Min(5f)] private float secondsToStarving = 120f;

        [SerializeField] private float hunger01;
        private bool forcedHungry;

        public float Hunger01 => Mathf.Clamp01(hunger01);
        public PetMood Mood => MoodMath.Classify(Hunger01);

        private void Awake()
        {
            if (companion == null) companion = GetComponent<DogCompanionController>();
            if (feeding == null) feeding = GetComponent<DogFeedingController>();
        }

        private void OnEnable()
        {
            if (feeding != null) feeding.Fed += OnFed;
        }

        private void OnDisable()
        {
            if (feeding != null) feeding.Fed -= OnFed;
        }

        private void OnFed()
        {
            forcedHungry = false;
            hunger01 = 0f;
        }

        /// <summary>HUD demo button: pin hunger to full (toggle off to resume normal decay).</summary>
        public void ForceHungry(bool on)
        {
            forcedHungry = on;
            if (on) hunger01 = 1f;
        }

        public void ToggleForceHungry() => ForceHungry(!forcedHungry);

        private void Update()
        {
            if (!forcedHungry)
                hunger01 = Mathf.Clamp01(hunger01 + Time.deltaTime / Mathf.Max(5f, secondsToStarving));

            companion?.SetMoodModifiers(
                MoodMath.SpeedMultiplier(hunger01), MoodMath.SitBias(hunger01));
        }
    }
}
