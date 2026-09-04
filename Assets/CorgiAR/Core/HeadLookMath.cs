using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Pure math for the procedural head-look: blend the animator's posed head
    /// rotation toward a clamped "look at target" rotation. Kept separate from the
    /// MonoBehaviour so the clamping is unit-testable.
    /// </summary>
    public static class HeadLookMath
    {
        /// <summary>
        /// Given the head bone's currently posed world rotation and a world-space
        /// direction to the target, returns the world rotation the bone should take:
        /// a look rotation whose yaw/pitch offset from the posed forward is clamped
        /// to <paramref name="maxYaw"/> / <paramref name="maxPitch"/> degrees, then
        /// blended from the posed rotation by <paramref name="weight"/> (0..1).
        /// </summary>
        public static Quaternion ClampedLookRotation(
            Quaternion posedWorld, Vector3 worldToTarget, Vector3 worldUp,
            float maxYaw, float maxPitch, float weight)
        {
            weight = Mathf.Clamp01(weight);
            if (worldToTarget.sqrMagnitude < 1e-6f || weight <= 0f)
                return posedWorld;

            Quaternion look = Quaternion.LookRotation(worldToTarget.normalized, worldUp);

            // Offset of the look rotation relative to the posed rotation, in the
            // posed bone's local frame.
            Quaternion offset = Quaternion.Inverse(posedWorld) * look;
            Vector3 e = NormalizeEuler(offset.eulerAngles);
            e.x = Mathf.Clamp(e.x, -maxPitch, maxPitch);
            e.y = Mathf.Clamp(e.y, -maxYaw, maxYaw);
            e.z = 0f;

            Quaternion clamped = posedWorld * Quaternion.Euler(e);
            return Quaternion.Slerp(posedWorld, clamped, weight);
        }

        private static Vector3 NormalizeEuler(Vector3 e) => new(
            NormalizeAngle(e.x), NormalizeAngle(e.y), NormalizeAngle(e.z));

        private static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            if (a < -180f) a += 360f;
            return a;
        }
    }
}
