using System;
using System.Collections.Generic;
using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Swaps the visible pet under the companion wrapper at runtime: destroys the
    /// old "Pet Visual" child, spawns the chosen model, assigns its generated
    /// smooth override controller, re-materials Dog-Kit pets (their built-in
    /// material speckles under URP), normalises the model to its target on-screen
    /// height and grounds it. Wrapper transform / placement state is untouched, so
    /// you can swap pets any time. The choice is remembered in PlayerPrefs.
    /// Swapping is blocked while the pet is eating.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetBinder : MonoBehaviour
    {
        [Serializable]
        public struct Binding
        {
            public string Id;
            public string DisplayName;
            public PetFamily Family;
            public GameObject Prefab;
            public AnimatorOverrideController Controller;
            public Sprite Thumbnail;
            public float Scale;
        }

        private static readonly string[] DogKitCarryBones = { "DEF-jaw_master", "DEF-jaw", "DEF-spine.011" };
        private static readonly string[] UaaCarryBones = { "Head", "Neck3", "Neck2" };

        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();
        [SerializeField] private DogAnimatorAdapter animatorAdapter;
        [SerializeField] private DogGroundAligner groundAligner;
        [SerializeField] private DogFeedingController feeding;
        [SerializeField] private PetHeadLook headLook;
        [SerializeField] private ToyFetchController toyFetch;
        [Tooltip("URP/Simple Lit material for Dog-Kit pets only (their built-in material flashes under URP).")]
        [SerializeField] private Material dogKitMaterial;
        [SerializeField] private string visualChildName = "Pet Visual";

        public event Action<string> PetChanged;
        public string CurrentId { get; private set; }
        public IReadOnlyList<Binding> Bindings => bindings;

#if UNITY_EDITOR
        /// <summary>Editor-only: populate the roster from the setup generator.</summary>
        public void EditorSetBindings(Binding[] value) => bindings = value ?? Array.Empty<Binding>();
#endif

        public bool CanSwap =>
            (feeding == null || !feeding.IsEating) &&
            (toyFetch == null || !toyFetch.IsBusy);

        private void Awake()
        {
            if (animatorAdapter == null) animatorAdapter = GetComponent<DogAnimatorAdapter>();
            if (groundAligner == null) groundAligner = GetComponent<DogGroundAligner>();
            if (feeding == null) feeding = GetComponent<DogFeedingController>();
            if (headLook == null) headLook = GetComponent<PetHeadLook>();
            if (toyFetch == null) toyFetch = GetComponent<ToyFetchController>();
        }

        private void Start()
        {
            string saved = PlayerPrefs.GetString(PetCatalog.PrefKey, PetCatalog.DefaultId);
            Bind(Has(saved) ? saved : (bindings.Length > 0 ? bindings[0].Id : PetCatalog.DefaultId));
        }

        public bool Has(string id) => TryFind(id, out _);

        public void Bind(string id)
        {
            if (!CanSwap)
                return;
            if (!TryFind(id, out Binding binding) || binding.Prefab == null)
                return;

            Transform old = transform.Find(visualChildName);
            if (old != null)
            {
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }

            GameObject visual = Instantiate(binding.Prefab, transform);
            visual.name = visualChildName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            // Start from the model's authored (native) scale, then apply the per-family
            // multiplier — the Dog Kit uses 1, the Ultimate Animated Animals ~0.16.
            float scale = binding.Scale > 0f ? binding.Scale : 1f;
            visual.transform.localScale *= scale;

            if (binding.Family == PetFamily.DogKit && dogKitMaterial != null)
            {
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = dogKitMaterial;
                    r.sharedMaterials = mats;
                }
            }

            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (binding.Controller != null)
                    animator.runtimeAnimatorController = binding.Controller;
                animator.applyRootMotion = false;
            }
            animatorAdapter?.Bind(animator);
            feeding?.RebindMouth(visual.transform);

            string[] carryCandidates = binding.Family == PetFamily.DogKit
                ? DogKitCarryBones : UaaCarryBones;
            Transform carryBone = BoneResolver.Resolve(carryCandidates, visual.transform, "jaw");
            if (feeding != null) feeding.RebindCarry(carryBone);
            if (toyFetch != null) toyFetch.RebindCarry(carryBone);
            if (headLook != null) headLook.Rebind(visual.transform, binding.Family);

            if (groundAligner != null)
            {
                groundAligner.Rebind(visual.transform);
                groundAligner.Align();
            }

            CurrentId = id;
            PlayerPrefs.SetString(PetCatalog.PrefKey, id);
            PlayerPrefs.Save();
            PetChanged?.Invoke(id);
        }

        private bool TryFind(string id, out Binding binding)
        {
            foreach (Binding candidate in bindings)
            {
                if (candidate.Id == id)
                {
                    binding = candidate;
                    return true;
                }
            }
            binding = default;
            return false;
        }
    }
}
