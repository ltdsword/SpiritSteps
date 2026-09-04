using NUnit.Framework;
using UnityEngine;

namespace CorgiAR.Tests
{
    public sealed class HeadLookMathTests
    {
        [Test]
        public void ZeroWeight_ReturnsPosedRotation()
        {
            Quaternion posed = Quaternion.Euler(0f, 30f, 0f);
            Quaternion result = HeadLookMath.ClampedLookRotation(
                posed, Vector3.left, Vector3.up, 55f, 30f, 0f);
            Assert.That(Quaternion.Angle(posed, result), Is.LessThan(0.01f));
        }

        [Test]
        public void FullWeight_ClampsYawToLimit()
        {
            // Posed forward is +Z; target is directly behind (-Z) => wants 180° yaw.
            Quaternion posed = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Quaternion result = HeadLookMath.ClampedLookRotation(
                posed, Vector3.back, Vector3.up, 55f, 30f, 1f);

            float yaw = Mathf.DeltaAngle(0f, (Quaternion.Inverse(posed) * result).eulerAngles.y);
            Assert.That(Mathf.Abs(yaw), Is.LessThanOrEqualTo(55f + 0.5f));
            Assert.That(Mathf.Abs(yaw), Is.GreaterThan(50f));
        }

        [Test]
        public void SmallOffset_WithinLimits_TracksTarget()
        {
            Quaternion posed = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            // 20° to the right, within the 55° yaw limit.
            Vector3 dir = Quaternion.Euler(0f, 20f, 0f) * Vector3.forward;
            Quaternion result = HeadLookMath.ClampedLookRotation(
                posed, dir, Vector3.up, 55f, 30f, 1f);

            float yaw = Mathf.DeltaAngle(0f, (Quaternion.Inverse(posed) * result).eulerAngles.y);
            Assert.That(yaw, Is.EqualTo(20f).Within(1f));
        }
    }
}
