using System;
using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Pure helpers for the Automatic "wander around the player" behaviour: pick a
    /// random ground target on a disc around a moving anchor (the AR camera
    /// projected down), and decide when the pet has arrived or the anchor has
    /// drifted far enough that the current target should be abandoned.
    /// </summary>
    public static class RoamPlanner
    {
        /// <summary>
        /// A uniformly-distributed random point on the horizontal disc of the
        /// given radius centred on <paramref name="anchor"/>. Y is kept.
        /// </summary>
        public static Vector3 PickTarget(Vector3 anchor, float radius, System.Random rng)
        {
            double angle = rng.NextDouble() * Math.PI * 2.0;
            double distance = Math.Max(0f, radius) * Math.Sqrt(rng.NextDouble());
            return new Vector3(
                anchor.x + (float)(Math.Cos(angle) * distance),
                anchor.y,
                anchor.z + (float)(Math.Sin(angle) * distance));
        }

        /// <summary>True when the pet is within <paramref name="threshold"/> (planar) of the target.</summary>
        public static bool HasArrived(Vector3 position, Vector3 target, float threshold)
        {
            float dx = position.x - target.x;
            float dz = position.z - target.z;
            return dx * dx + dz * dz <= threshold * threshold;
        }

        /// <summary>
        /// True when the anchor moved so far that <paramref name="target"/> now sits
        /// outside the roam disc plus a little slack — time to re-target toward the
        /// player's new position.
        /// </summary>
        public static bool TargetOutOfRange(Vector3 anchor, Vector3 target, float radius, float slack)
        {
            float dx = anchor.x - target.x;
            float dz = anchor.z - target.z;
            float limit = Mathf.Max(0f, radius) + Mathf.Max(0f, slack);
            return dx * dx + dz * dz > limit * limit;
        }
    }
}
