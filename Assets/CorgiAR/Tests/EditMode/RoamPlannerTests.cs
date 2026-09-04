using System;
using NUnit.Framework;
using UnityEngine;

namespace CorgiAR.Tests
{
    public sealed class RoamPlannerTests
    {
        [Test]
        public void PickTarget_StaysWithinRadiusAndKeepsHeight()
        {
            var rng = new System.Random(12345);
            var anchor = new Vector3(3f, 1.2f, -4f);
            const float radius = 1.5f;

            for (int i = 0; i < 200; i++)
            {
                Vector3 target = RoamPlanner.PickTarget(anchor, radius, rng);
                float planar = Vector2.Distance(
                    new Vector2(anchor.x, anchor.z), new Vector2(target.x, target.z));
                Assert.LessOrEqual(planar, radius + 0.0001f);
                Assert.AreEqual(anchor.y, target.y, 0.0001f);
            }
        }

        [Test]
        public void HasArrived_RespectsThreshold()
        {
            var target = new Vector3(0f, 0f, 0f);
            Assert.IsTrue(RoamPlanner.HasArrived(new Vector3(0.05f, 5f, 0f), target, 0.1f));
            Assert.IsFalse(RoamPlanner.HasArrived(new Vector3(0.5f, 0f, 0f), target, 0.1f));
        }

        [Test]
        public void TargetOutOfRange_TrueWhenAnchorDriftsPastRadiusPlusSlack()
        {
            var target = new Vector3(1f, 0f, 0f);
            Assert.IsFalse(RoamPlanner.TargetOutOfRange(Vector3.zero, target, 1.5f, 0.5f));
            // anchor walked 3 m away from the target
            Assert.IsTrue(RoamPlanner.TargetOutOfRange(new Vector3(4f, 0f, 0f), target, 1.5f, 0.5f));
        }
    }
}
