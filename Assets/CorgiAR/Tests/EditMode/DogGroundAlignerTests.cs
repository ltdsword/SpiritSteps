using NUnit.Framework;
using UnityEngine;

namespace CorgiAR.Tests
{
    public sealed class DogGroundAlignerTests
    {
        private const float Clearance = 0.01f;

        private static (Transform wrapper, Transform visual, CapsuleCollider capsule, GameObject root)
            BuildRig(float cubeHeight, float wrapperY)
        {
            var root = new GameObject("wrapper");
            root.transform.position = new Vector3(0f, wrapperY, 0f);
            var capsule = root.AddComponent<CapsuleCollider>();

            var visual = new GameObject("visual");
            visual.transform.SetParent(root.transform, false);

            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(mesh.GetComponent<BoxCollider>());
            mesh.transform.SetParent(visual.transform, false);
            mesh.transform.localScale = new Vector3(0.4f, cubeHeight, 0.4f);
            mesh.transform.localPosition = new Vector3(0f, cubeHeight * 0.5f + 3f, 0f); // start floating

            return (root.transform, visual.transform, capsule, root);
        }

        [Test]
        public void Align_DropsLowestPointToGroundWithoutRescaling()
        {
            var (wrapper, visual, capsule, root) = BuildRig(cubeHeight: 1.4f, wrapperY: 5f);
            try
            {
                Vector3 scaleBefore = visual.localScale;
                Bounds world = DogGroundAligner.Align(wrapper, visual, capsule, Clearance);

                Assert.That(world.min.y, Is.EqualTo(wrapper.position.y + Clearance).Within(0.003f),
                    "lowest point sits on the ground");
                Assert.That(visual.localScale, Is.EqualTo(scaleBefore), "scale is left untouched");
                Assert.That(world.size.y, Is.EqualTo(1.4f).Within(0.05f), "native size is preserved");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Align_IsIdempotent()
        {
            var (wrapper, visual, capsule, root) = BuildRig(cubeHeight: 0.3f, wrapperY: -0.08f);
            try
            {
                DogGroundAligner.Align(wrapper, visual, capsule, Clearance);
                Vector3 after1 = visual.position;
                DogGroundAligner.Align(wrapper, visual, capsule, Clearance);
                DogGroundAligner.Align(wrapper, visual, capsule, Clearance);
                Assert.That(Vector3.Distance(visual.position, after1), Is.LessThan(0.003f), "no drift");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Align_FitsCapsule()
        {
            var (wrapper, visual, capsule, root) = BuildRig(cubeHeight: 0.6f, wrapperY: 0f);
            try
            {
                DogGroundAligner.Align(wrapper, visual, capsule, Clearance);
                Assert.That(capsule.height, Is.GreaterThan(0f));
                Assert.That(capsule.radius, Is.GreaterThan(0f));
                Assert.That(capsule.direction, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
