using NUnit.Framework;

namespace CorgiAR.Tests
{
    public sealed class DogAnimationMapTests
    {
        [TestCase(DogAnimationState.Breathing, 0)]
        [TestCase(DogAnimationState.WigglingTail, 1)]
        [TestCase(DogAnimationState.Walking, 2)]
        [TestCase(DogAnimationState.Running, 3)]
        [TestCase(DogAnimationState.Sitting, 4)]
        [TestCase(DogAnimationState.Eating, 5)]
        public void GetAnimationId_UsesPetLocomotionContract(
            DogAnimationState state, int expected)
        {
            Assert.That(DogAnimationMap.GetAnimationId(state), Is.EqualTo(expected));
        }
    }
}
