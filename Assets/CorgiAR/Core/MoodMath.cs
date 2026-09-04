using UnityEngine;

namespace CorgiAR
{
    /// <summary>How content the pet is, derived from its hunger level.</summary>
    public enum PetMood
    {
        Happy,
        Neutral,
        Hungry,
        Starving
    }

    /// <summary>
    /// Pure mapping from a 0..1 hunger level to the pet's mood and the movement
    /// modifiers a hungry pet gets (slower, more likely to flop down and rest).
    /// </summary>
    public static class MoodMath
    {
        public static PetMood Classify(float hunger01)
        {
            hunger01 = Mathf.Clamp01(hunger01);
            if (hunger01 >= 0.85f) return PetMood.Starving;
            if (hunger01 >= 0.6f) return PetMood.Hungry;
            if (hunger01 >= 0.3f) return PetMood.Neutral;
            return PetMood.Happy;
        }

        /// <summary>1.0 when well-fed, easing down to 0.65 when starving.</summary>
        public static float SpeedMultiplier(float hunger01) =>
            Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(hunger01));

        /// <summary>Extra probability (0 → 0.4) of choosing "sit / rest" while roaming.</summary>
        public static float SitBias(float hunger01) =>
            Mathf.Lerp(0f, 0.4f, Mathf.Clamp01(hunger01));
    }
}
