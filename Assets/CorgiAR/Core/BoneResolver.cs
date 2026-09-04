using System;
using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Finds a named bone under a rig root: first an exact match against an ordered
    /// candidate list, then a case-insensitive "name contains" fallback. Used to
    /// locate the head / carry bone on either pet rig (Dog Kit <c>DEF-*</c> or the
    /// Ultimate Animated Animals <c>AnimalArmature</c>) after a pet swap.
    /// </summary>
    public static class BoneResolver
    {
        public static Transform Resolve(string[] candidates, Transform root, string containsFallback = null)
        {
            if (root == null)
                return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);

            if (candidates != null)
                foreach (string wanted in candidates)
                {
                    if (string.IsNullOrEmpty(wanted))
                        continue;
                    foreach (Transform t in all)
                        if (t.name == wanted)
                            return t;
                }

            if (!string.IsNullOrEmpty(containsFallback))
            {
                Transform best = null;
                foreach (Transform t in all)
                    if (t.name.IndexOf(containsFallback, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (best == null || GetDepth(t) < GetDepth(best)))
                        best = t;
                if (best != null)
                    return best;
            }

            return null;
        }

        private static int GetDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }
    }
}
