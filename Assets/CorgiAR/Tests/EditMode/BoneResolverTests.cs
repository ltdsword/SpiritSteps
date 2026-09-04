using NUnit.Framework;
using UnityEngine;

namespace CorgiAR.Tests
{
    public sealed class BoneResolverTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("rig");
            Child(root.transform, "Hips");
            Transform spine = Child(root.transform, "DEF-spine.010");
            Child(spine, "DEF-spine.011");
            Child(root.transform, "TailBone");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Resolve_PrefersEarlierCandidate()
        {
            Transform hit = BoneResolver.Resolve(
                new[] { "DEF-spine.011", "DEF-spine.010" }, root.transform);
            Assert.AreEqual("DEF-spine.011", hit.name);
        }

        [Test]
        public void Resolve_FallsBackToSecondCandidateWhenFirstMissing()
        {
            Transform hit = BoneResolver.Resolve(
                new[] { "DEF-spine.099", "DEF-spine.010" }, root.transform);
            Assert.AreEqual("DEF-spine.010", hit.name);
        }

        [Test]
        public void Resolve_UsesContainsFallbackCaseInsensitive()
        {
            Transform hit = BoneResolver.Resolve(
                new[] { "NoSuchBone" }, root.transform, "spine");
            Assert.IsNotNull(hit);
            StringAssert.Contains("spine", hit.name.ToLower());
        }

        [Test]
        public void Resolve_ReturnsNullWhenNothingMatches()
        {
            Assert.IsNull(BoneResolver.Resolve(new[] { "Nope" }, root.transform, "zzz"));
            Assert.IsNull(BoneResolver.Resolve(new[] { "Nope" }, null));
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
