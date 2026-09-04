using UnityEngine;
using UnityEngine.UI;

namespace CorgiAR
{
    /// <summary>
    /// The "đổi thú" full-screen modal: a scrim that blocks the HUD plus a card
    /// with a 3-column scrolling grid of pets (thumbnail + name, current one
    /// highlighted) and a close button. The GameObject stays active; visibility
    /// and input-blocking are driven through a <see cref="CanvasGroup"/> so the
    /// modal always renders above every other HUD control. Rows are built once
    /// from <see cref="PetBinder.Bindings"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PetMenuPanel : MonoBehaviour
    {
        [SerializeField] private PetBinder binder;
        [SerializeField] private RectTransform row;          // grid content
        [SerializeField] private GameObject entryTemplate;   // inactive: Button + child Image "Thumb" + child Text "Name"
        [SerializeField] private Button closeButton;
        [SerializeField] private Color normalTint = new(1f, 1f, 1f, 0.28f);
        [SerializeField] private Color selectedTint = new(1f, 0.78f, 0.32f, 0.95f);

        private CanvasGroup group;
        private bool built;

        public bool IsOpen => group != null && group.blocksRaycasts;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (binder == null)
                binder = FindFirstObjectByType<PetBinder>();
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            SetOpen(false);
        }

        private void OnEnable()
        {
            if (!built)
                Rebuild();
            if (binder != null)
                binder.PetChanged += OnPetChanged;
        }

        private void OnDisable()
        {
            if (binder != null)
                binder.PetChanged -= OnPetChanged;
        }

        /// <summary>Wired to the "ĐỔI THÚ" HUD button.</summary>
        public void Toggle() => SetOpen(!IsOpen);

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (group == null)
                group = GetComponent<CanvasGroup>();
            group.alpha = open ? 1f : 0f;
            group.interactable = open;
            group.blocksRaycasts = open;
            if (open)
            {
                transform.SetAsLastSibling();   // render above every HUD control
                if (!built)
                    Rebuild();
                OnPetChanged(binder != null ? binder.CurrentId : null);
            }
        }

        private void Rebuild()
        {
            if (binder == null || row == null || entryTemplate == null)
                return;

            for (int i = row.childCount - 1; i >= 0; i--)
            {
                GameObject child = row.GetChild(i).gameObject;
                if (child == entryTemplate)
                    continue;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            foreach (PetBinder.Binding binding in binder.Bindings)
            {
                GameObject entry = Instantiate(entryTemplate, row);
                entry.name = "Pet_" + binding.Id;
                entry.SetActive(true);

                Transform thumb = entry.transform.Find("Thumb");
                if (thumb != null && thumb.TryGetComponent(out Image thumbImage))
                {
                    thumbImage.sprite = binding.Thumbnail;
                    thumbImage.enabled = binding.Thumbnail != null;
                }

                Transform name = entry.transform.Find("Name");
                if (name != null && name.TryGetComponent(out Text nameText))
                    nameText.text = binding.DisplayName;

                string id = binding.Id;
                if (entry.TryGetComponent(out Button button))
                    button.onClick.AddListener(() => binder.Bind(id));
            }

            built = true;
            OnPetChanged(binder.CurrentId);
        }

        private void OnPetChanged(string id)
        {
            if (!built || row == null)
                return;
            foreach (Transform child in row)
            {
                if (child.gameObject == entryTemplate)
                    continue;
                if (child.TryGetComponent(out Image bg))
                    bg.color = child.name == "Pet_" + id ? selectedTint : normalTint;
            }
        }
    }
}
