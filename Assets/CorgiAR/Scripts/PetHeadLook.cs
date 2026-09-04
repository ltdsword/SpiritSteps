using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Procedural "notices you" head turn: after the Animator poses the rig each
    /// frame, nudge the pet's head bone toward the camera by a clamped amount. The
    /// look weight eases out while the pet is running or sitting so it never fights
    /// the locomotion clips. Rig-agnostic — the head bone is resolved by name via
    /// <see cref="BoneResolver"/> and re-resolved whenever the pet is swapped.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class PetHeadLook : MonoBehaviour
    {
        private static readonly string[] DogKitHeadBones = { "DEF-spine.011", "DEF-spine.010", "DEF-spine.009" };
        private static readonly string[] UaaHeadBones = { "Head", "Neck3", "Neck2" };

        [SerializeField] private DogCompanionController companion;
        [SerializeField] private Camera lookCamera;

        [Header("Feel")]
        [SerializeField, Range(0f, 90f)] private float maxYaw = 55f;
        [SerializeField, Range(0f, 70f)] private float maxPitch = 32f;
        [SerializeField, Range(0f, 1f)] private float weight = 0.6f;
        [SerializeField, Min(0.1f)] private float damping = 6f;

        private Transform headBone;
        private float currentWeight;

        public void SetCamera(Camera camera) => lookCamera = camera;

        private void Awake()
        {
            if (companion == null) companion = GetComponent<DogCompanionController>();
        }

        /// <summary>Re-resolve the head bone after the visible pet was swapped.</summary>
        public void Rebind(Transform visualRoot, PetFamily family)
        {
            string[] candidates = family == PetFamily.DogKit ? DogKitHeadBones : UaaHeadBones;
            headBone = BoneResolver.Resolve(candidates, visualRoot, "head");
        }

        private void LateUpdate()
        {
            if (headBone == null)
                return;

            Camera cam = lookCamera != null ? lookCamera : Camera.main;
            float target = cam != null && CanLook() ? weight : 0f;
            currentWeight = Mathf.MoveTowards(currentWeight, target, damping * Time.deltaTime);
            if (currentWeight <= 0.001f || cam == null)
                return;

            Vector3 toCamera = cam.transform.position - headBone.position;
            Quaternion desired = HeadLookMath.ClampedLookRotation(
                headBone.rotation, toCamera, Vector3.up, maxYaw, maxPitch, currentWeight);
            headBone.rotation = desired;
        }

        private bool CanLook()
        {
            if (companion == null)
                return true;
            if (companion.IsSitting || companion.IsInteracting)
                return false;
            return companion.DesiredAnimation != DogAnimationState.Running;
        }
    }
}
