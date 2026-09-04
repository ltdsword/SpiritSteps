using UnityEngine;
using ARWalking.UI;

namespace CorgiAR
{
    /// <summary>
    /// Reads <see cref="PetArSceneContext"/> (set by <c>UiPrototypeRuntime.EnterPetAr</c> right
    /// before this scene loaded) and applies it to the already-built CorgiAR companion + HUD:
    /// which pet to bind, whether the Capture button should be visible, and whether to nudge the
    /// player toward feeding. This is the one place CorgiAR reaches into ARWalking - see the
    /// assembly-direction note in docs/AR-3D-INTEGRATION-CONTRACT.md.
    /// Runs after <see cref="PetBinder"/>'s own Start() (which restores the last PlayerPrefs
    /// pick) so the incoming context always wins.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class PetArContextBinder : MonoBehaviour
    {
        [SerializeField] private PetBinder binder;
        [SerializeField] private CorgiArHud hud;

        private void Awake()
        {
            if (binder == null) binder = GetComponent<PetBinder>();
            if (hud == null) hud = FindFirstObjectByType<CorgiArHud>();
        }

        private void Start()
        {
            var petId = PetArSceneContext.PetId;
            // PetBinder.Start() (execution order 0, runs before this) already bound the
            // PlayerPrefs-remembered pet. Skip a redundant second destroy+instantiate pass
            // when the incoming context asks for that same pet - only rebind when it differs.
            if (!string.IsNullOrEmpty(petId) && binder != null && binder.CurrentId != petId && binder.Has(petId))
                binder.Bind(petId);

            hud?.SetPhotoModeEnabled(PetArSceneContext.IsPhotoMode);

            if (PetArSceneContext.Interaction == PendingPetInteraction.Feed)
                hud?.ShowToast("Kéo thức ăn xuống để cho ăn", 4f);
        }
    }
}
