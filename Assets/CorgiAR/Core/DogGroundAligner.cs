using System;
using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Drops a pet visual onto the ground and fits the wrapper's CapsuleCollider
    /// to it. Rig-agnostic (works for the Dog Kit's Rigify rig and Ultimate
    /// Animated Animals' AnimalArmature) — it measures the posed
    /// <c>SkinnedMeshRenderer</c> bounds (<c>updateWhenOffscreen</c> on + Animator
    /// sampled), never rig-specific bone names. Does NOT change the visual's scale
    /// — each model keeps its native size. Re-runnable without drift.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class DogGroundAligner : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField, Min(0f)] private float groundClearance = 0.004f;

        public Transform Visual => visual;
        public float GroundClearance => groundClearance;

        /// <summary>Point at a freshly-swapped pet visual before re-grounding.</summary>
        public void Rebind(Transform newVisual)
        {
            visual = newVisual;
        }

        public Bounds Align() =>
            Align(transform, visual, GetComponent<CapsuleCollider>(), groundClearance);

        public static Bounds Align(Transform wrapper, Transform visual, CapsuleCollider capsule, float clearance)
        {
            if (visual == null)
                throw new InvalidOperationException("DogGroundAligner: visual transform is not assigned.");

            foreach (SkinnedMeshRenderer s in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                s.updateWhenOffscreen = true;
            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            // reset any prior grounding offset, then drop the *true* lowest posed
            // vertex to the floor. SkinnedMeshRenderer.bounds is an inflated AABB
            // (often several cm below the real paws), so measuring it leaves the pet
            // visibly floating — bake the posed meshes and read their vertices.
            visual.localPosition = new Vector3(visual.localPosition.x, 0f, visual.localPosition.z);
            Physics.SyncTransforms();
            float lowest = LowestPosedY(visual);
            visual.position += Vector3.up * (wrapper.position.y + clearance - lowest);
            Physics.SyncTransforms();

            Bounds world = WorldBounds(visual);
            FitCapsule(wrapper, capsule, world, clearance);
            return world;
        }

        /// <summary>The lowest world-space vertex of every posed mesh under the visual.</summary>
        private static float LowestPosedY(Transform visual)
        {
            float min = float.PositiveInfinity;

            var baked = new Mesh();
            foreach (SkinnedMeshRenderer s in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                s.BakeMesh(baked, false);
                Matrix4x4 l2w = s.transform.localToWorldMatrix;
                foreach (Vector3 v in baked.vertices)
                {
                    float y = l2w.MultiplyPoint3x4(v).y;
                    if (y < min) min = y;
                }
            }
            if (Application.isPlaying) UnityEngine.Object.Destroy(baked);
            else UnityEngine.Object.DestroyImmediate(baked);

            foreach (MeshFilter mf in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer r = mf.GetComponent<Renderer>();
                if (mf.sharedMesh == null || r == null ||
                    r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;
                Matrix4x4 l2w = mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    float y = l2w.MultiplyPoint3x4(v).y;
                    if (y < min) min = y;
                }
            }

            if (float.IsInfinity(min))
                return WorldBounds(visual).min.y;
            return min;
        }

        private static Bounds WorldBounds(Transform visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            Bounds bounds = default;
            foreach (Renderer r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!has)
                throw new InvalidOperationException("DogGroundAligner: pet visual has no mesh renderer to measure.");
            return bounds;
        }

        private static void FitCapsule(Transform wrapper, CapsuleCollider capsule, Bounds world, float clearance)
        {
            capsule.direction = 1; // Y
            Vector3 localCenter = wrapper.InverseTransformPoint(world.center);
            float radius = Mathf.Clamp(
                Mathf.Max(world.extents.x, world.extents.z),
                0.02f, Mathf.Max(0.04f, world.size.y * 0.5f));
            float height = Mathf.Max(world.size.y, radius * 2f);
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = new Vector3(localCenter.x, height * 0.5f + clearance, localCenter.z);
        }
    }
}
