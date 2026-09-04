using NUnit.Framework;
using UnityEngine;

namespace CorgiAR.Tests
{
    public sealed class PettingDetectorTests
    {
        [Test]
        public void AddSample_TriggersAfterTwoDirectionReversalsOverDog()
        {
            var detector = new PettingDetector(0.6f, 70f, 18f, 1.2);
            Assert.False(detector.AddSample(new PettingSample(new Vector2(100, 100), true, 0.9f, 0.00)));
            Assert.False(detector.AddSample(new PettingSample(new Vector2(150, 102), true, 0.9f, 0.20)));
            Assert.False(detector.AddSample(new PettingSample(new Vector2(105, 101), true, 0.9f, 0.40)));
            Assert.True(detector.AddSample(new PettingSample(new Vector2(160, 103), true, 0.9f, 0.60)));
        }

        [Test]
        public void AddSample_DoesNotTriggerWhenPointerLeavesDog()
        {
            var detector = new PettingDetector(0.6f, 70f, 18f, 1.2);
            detector.AddSample(new PettingSample(new Vector2(100, 100), true, 0.9f, 0.00));
            detector.AddSample(new PettingSample(new Vector2(160, 100), false, 0.9f, 0.20));
            detector.AddSample(new PettingSample(new Vector2(100, 100), true, 0.9f, 0.40));
            Assert.False(detector.AddSample(new PettingSample(new Vector2(160, 100), true, 0.9f, 0.60)));
        }

        [Test]
        public void AddSample_RejectsLowConfidenceAndExpiredWindow()
        {
            var detector = new PettingDetector(0.6f, 70f, 18f, 1.2);
            detector.AddSample(new PettingSample(Vector2.zero, true, 0.5f, 0.0));
            detector.AddSample(new PettingSample(new Vector2(100, 0), true, 0.9f, 2.0));
            detector.AddSample(new PettingSample(Vector2.zero, true, 0.9f, 2.2));
            Assert.False(detector.AddSample(new PettingSample(new Vector2(100, 0), true, 0.9f, 2.4)));
        }
    }
}
