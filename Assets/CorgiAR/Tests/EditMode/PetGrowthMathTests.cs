using NUnit.Framework;

namespace CorgiAR.Tests
{
    public sealed class PetGrowthMathTests
    {
        [TestCase(0, PetGrowthStage.Baby)]
        [TestCase(4, PetGrowthStage.Baby)]
        [TestCase(5, PetGrowthStage.Young)]
        [TestCase(14, PetGrowthStage.Young)]
        [TestCase(15, PetGrowthStage.Adult)]
        [TestCase(99, PetGrowthStage.Adult)]
        public void StageForChickenCount_UsesFiveThenTenAdditionalMeals(
            int count, PetGrowthStage expected)
        {
            Assert.AreEqual(expected, PetGrowthMath.StageForChickenCount(count, 5, 10));
        }

        [TestCase(0, 5)]
        [TestCase(4, 1)]
        [TestCase(5, 10)]
        [TestCase(14, 1)]
        [TestCase(15, 0)]
        public void ChickenUntilNextStage_ReportsRemainingMeals(int count, int expected)
        {
            Assert.AreEqual(expected, PetGrowthMath.ChickenUntilNextStage(count, 5, 10));
        }

        [Test]
        public void InvalidInputs_AreClampedToUsableThresholds()
        {
            Assert.AreEqual(PetGrowthStage.Baby,
                PetGrowthMath.StageForChickenCount(-3, 0, 0));
            Assert.AreEqual(PetGrowthStage.Young,
                PetGrowthMath.StageForChickenCount(1, 0, 0));
            Assert.AreEqual(PetGrowthStage.Adult,
                PetGrowthMath.StageForChickenCount(2, 0, 0));
        }
    }
}
