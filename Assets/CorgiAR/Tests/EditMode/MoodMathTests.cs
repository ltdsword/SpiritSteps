using NUnit.Framework;
using UnityEngine;

namespace CorgiAR.Tests
{
    public sealed class MoodMathTests
    {
        [Test]
        public void Classify_MovesThroughAllMoodsAsHungerRises()
        {
            Assert.AreEqual(PetMood.Happy, MoodMath.Classify(0f));
            Assert.AreEqual(PetMood.Neutral, MoodMath.Classify(0.4f));
            Assert.AreEqual(PetMood.Hungry, MoodMath.Classify(0.7f));
            Assert.AreEqual(PetMood.Starving, MoodMath.Classify(1f));
        }

        [Test]
        public void SpeedMultiplier_EasesFromOneToSixtyFivePercent()
        {
            Assert.AreEqual(1f, MoodMath.SpeedMultiplier(0f), 1e-4f);
            Assert.AreEqual(0.65f, MoodMath.SpeedMultiplier(1f), 1e-4f);
            Assert.Less(MoodMath.SpeedMultiplier(0.5f), 1f);
            Assert.Greater(MoodMath.SpeedMultiplier(0.5f), 0.65f);
        }

        [Test]
        public void SitBias_IsZeroWhenFedAndGrowsWithHunger()
        {
            Assert.AreEqual(0f, MoodMath.SitBias(0f), 1e-4f);
            Assert.AreEqual(0.4f, MoodMath.SitBias(1f), 1e-4f);
        }

        [Test]
        public void Helpers_ClampOutOfRangeInput()
        {
            Assert.AreEqual(PetMood.Happy, MoodMath.Classify(-5f));
            Assert.AreEqual(0.65f, MoodMath.SpeedMultiplier(9f), 1e-4f);
            Assert.AreEqual(0f, MoodMath.SitBias(-1f), 1e-4f);
        }
    }
}
