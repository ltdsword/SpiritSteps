using System;
using System.Collections.Generic;
using ARWalking.UI;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.VectorGraphics;
using ShibaFeeding;
using AppPanel = Unity.AppUI.UI.Panel;
using UiButton = UnityEngine.UIElements.Button;
using UiImage = UnityEngine.UIElements.Image;

namespace CorgiAR.UI
{
    /// <summary>
    /// UI Toolkit rebuild of the in-AR gameplay HUD (status pill, "Switch pet" card, Come
    /// here/Feed/Throw ball row, camera cluster, photo viewer) - replaces the old uGUI
    /// "Corgi AR HUD"/<c>PetArHudGenerator</c>. Built at runtime in <see cref="Start"/>, same
    /// pattern as <c>WalkUiController</c>/<c>HomeUiController</c> (own UIDocument/PanelSettings,
    /// reuses the shared <c>ARWalking.uss</c> stylesheet/design tokens). Lives in
    /// Assembly-CSharp (not <c>ARWalking.Runtime</c>) because it must reference CorgiAR/
    /// ShibaFeeding types that <c>ARWalking.Runtime</c> cannot see.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class CorgiArGlassHud : MonoBehaviour
    {
        [SerializeField] private DogARPlacementController placement;
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private DogFeedingController feeding;
        [SerializeField] private PetMoodController mood;
        [SerializeField] private ToyFetchController toyFetch;
        [SerializeField] private PetBinder binder;
        [SerializeField] private ArPhotoCapture photo;
        [SerializeField] private Camera hudCamera;
        [SerializeField] private FoodDragThrowUI.FoodChoice[] foodChoices = Array.Empty<FoodDragThrowUI.FoodChoice>();
        [SerializeField] private GameObject ballPrefab;
        [SerializeField] private Sprite ballIconSprite;
        [SerializeField] private Sprite whistleIconSprite;

        private static readonly Color White = Color.white;

        private UIDocument document;
        private AppPanel panel;
        private Label statusLabelRef;
        private UiImage changePetThumb;
        private VisualElement comeCircle;
        private VisualElement foodCircle;
        private VisualElement ballCircle;
        private UiImage foodIconImage;
        private Label foodQuantityLabel;
        private VisualElement petPicker;
        private UiImage galleryThumb;
        private VisualElement galleryIconElement;
        private VisualElement galleryBadge;
        private Label galleryBadgeLabel;
        private VisualElement photoViewer;
        private UiImage photoViewerImage;
        private ScrollView photoStrip;
        private VisualElement flashOverlay;

        private ArFoodDragController foodDrag;
        private ArBallDragController ballDrag;
        private int viewerIndex;

        private readonly Dictionary<string, VectorImage> icons = new();

        private void Awake()
        {
            if (placement == null) placement = FindFirstObjectByType<DogARPlacementController>();
            if (companion == null) companion = FindFirstObjectByType<DogCompanionController>();
            if (feeding == null) feeding = FindFirstObjectByType<DogFeedingController>();
            if (mood == null) mood = FindFirstObjectByType<PetMoodController>();
            if (toyFetch == null) toyFetch = FindFirstObjectByType<ToyFetchController>();
            if (binder == null) binder = FindFirstObjectByType<PetBinder>();
            if (photo == null) photo = FindFirstObjectByType<ArPhotoCapture>();
            if (hudCamera == null) hudCamera = Camera.main;
        }

        private void Start()
        {
            // DogARModeController (order -1000) has already run Awake/Start by now and
            // picked the live camera - the real AR camera in AR mode, or the desktop
            // preview camera (via DogARPlacementController.ConfigureForPreview). Re-sync
            // here rather than in Awake, since the desktop-preview swap only happens in
            // DogARModeController.Start, which runs after every script's Awake.
            if (placement != null && placement.ArCamera != null)
                hudCamera = placement.ArCamera;

            document = GetComponent<UIDocument>();
            if (document.panelSettings == null)
                document.panelSettings = Resources.Load<PanelSettings>("UI/CorgiArHudPanelSettings");
            VisualElement root = document.rootVisualElement;
            root.Clear();
            StyleSheet styleSheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            panel = new AppPanel { name = "corgi-ar-glass-hud", theme = "light", scale = "medium" };
            panel.style.backgroundColor = new StyleColor(Color.clear);
            panel.pickingMode = PickingMode.Ignore;
            root.style.backgroundColor = new StyleColor(Color.clear);
            root.pickingMode = PickingMode.Ignore;
            root.Add(panel);

            VisualElement topStack = Element(null, "ar-top-stack");
            panel.Add(topStack);
            BuildStatusRow(topStack);
            BuildChangePetCard(topStack);
            BuildInteractionRow();
            BuildCamCluster();
            BuildPhotoViewer();
            BuildPetPicker();

            if (binder != null) binder.PetChanged += OnPetChanged;
            if (photo != null)
            {
                photo.SetHudDocument(document);
                photo.PhotosChanged += RefreshGallery;
                photo.FlashRequested += PlayFlash;
                photo.ToastRequested += ShowToast;
            }
            if (foodDrag != null) foodDrag.FoodVisualChanged += RefreshFood;
            RefreshChangePetThumb();
            RefreshGallery();
            RefreshFood();
        }

        private void OnDisable()
        {
            if (binder != null) binder.PetChanged -= OnPetChanged;
            if (photo != null)
            {
                photo.PhotosChanged -= RefreshGallery;
                photo.FlashRequested -= PlayFlash;
                photo.ToastRequested -= ShowToast;
            }
            if (foodDrag != null) foodDrag.FoodVisualChanged -= RefreshFood;
        }

        private void Update()
        {
            bool placed = placement != null && placement.IsPlaced;
            bool eating = feeding != null && feeding.IsEating;
            bool sitting = companion != null && companion.IsSitting;
            bool planeDetected = placement != null && placement.HasDetectedPlane;

            if (statusLabelRef != null)
                statusLabelRef.text = StatusFor(placed, eating, sitting, planeDetected);

            SetDisplay(foodCircle?.parent, placed);
            SetDisplay(ballCircle?.parent, placed && !eating);
            SetDisplay(comeCircle?.parent, placed && !eating && !sitting);
        }

        private string StatusFor(bool placed, bool eating, bool sitting, bool planeDetected)
        {
            if (!placed)
                return planeDetected
                    ? "Surface found — placing your pet…"
                    : "Slowly move the camera around the floor/table to find a surface…";
            if (eating) return "Eating… yum!";
            if (sitting) return "Your pet is sitting nicely";

            PetMood m = mood != null ? mood.Mood : PetMood.Happy;
            if (m == PetMood.Starving) return "Your pet is starving — feed it now!";
            if (m == PetMood.Hungry) return "Your pet is getting hungry — toss it a snack";
            return "Your pet wanders around you • throw the ball, feed it, or call it back";
        }

        // ---- building blocks ----

        private void BuildStatusRow(VisualElement parent)
        {
            VisualElement row = Element(null, "ar-status-row");
            VisualElement pill = Element(null, "ar-status-pill");
            var label = new Label("Slowly move the camera around the floor/table to find a surface…");
            label.AddToClassList("ar-status-label");
            label.AddToClassList("font-display");
            pill.Add(label);
            row.Add(pill);
            parent.Add(row);
            statusLabelRef = label;
        }

        private void BuildChangePetCard(VisualElement parent)
        {
            var card = new UiButton(TogglePetPicker);
            card.AddToClassList("ar-change-pet-card");
            var thumb = new UiImage { name = "change-pet-thumb", scaleMode = ScaleMode.ScaleAndCrop };
            thumb.AddToClassList("ar-change-pet-thumb");
            card.Add(thumb);

            VisualElement textColumn = Element(null);
            var kicker = new Label("CURRENTLY WALKING");
            kicker.AddToClassList("ar-change-pet-kicker");
            kicker.AddToClassList("font-display");
            var title = new Label("Switch Pet");
            title.AddToClassList("ar-change-pet-title");
            title.AddToClassList("font-display");
            textColumn.Add(kicker);
            textColumn.Add(title);
            card.Add(textColumn);
            card.Add(Icon("swap-horizontal", "ar-change-pet-swap", White));

            parent.Add(card);
            changePetThumb = thumb;
        }

        private void OnPetChanged(string id) => RefreshChangePetThumb();

        private void RefreshChangePetThumb()
        {
            if (changePetThumb == null || binder == null)
                return;
            foreach (PetBinder.Binding candidate in binder.Bindings)
            {
                if (candidate.Id != binder.CurrentId)
                    continue;
                changePetThumb.sprite = candidate.Thumbnail;
                return;
            }
        }

        private void RefreshFood()
        {
            if (foodIconImage == null || foodDrag == null)
                return;
            foodIconImage.sprite = foodDrag.SelectedFoodIcon;
            foodIconImage.style.opacity = foodDrag.SelectedFoodQuantity > 0 ? 1f : 0.35f;
            if (foodQuantityLabel != null) foodQuantityLabel.text = foodDrag.SelectedFoodQuantity.ToString();
        }

        private void BuildPetPicker()
        {
            petPicker = Element(null, "overlay-scrim", "pet3d-picker-scrim");
            petPicker.style.display = DisplayStyle.None;
            var modal = Element(null, "pet3d-picker-card");
            var header = Element(null, "pet3d-picker-header");
            var title = new Label("Choose a Pet");
            title.AddToClassList("pet3d-picker-title");
            title.AddToClassList("font-display");
            header.Add(title);
            var close = new UiButton(ClosePetPicker);
            close.AddToClassList("icon-button");
            close.AddToClassList("small-round-control");
            close.Add(Icon("x", "icon-image", new Color32(42, 63, 49, 255)));
            header.Add(close);
            modal.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("pet3d-picker-scroll");
            VisualElement grid = Element(null, "pet3d-picker-grid");
            var choiceCount = 0;
            if (binder != null)
            {
                UiPrototypeRuntime runtime = UiPrototypeRuntime.Instance;
                foreach (PetBinder.Binding binding in binder.Bindings)
                {
                    CompanionProgressData progress = runtime != null ? runtime.Companion(binding.Id) : null;
                    // "Owned", not just distance-Unlocked - see PetBinder.IsUnlocked.
                    if (progress == null || !progress.owned) continue;
                    choiceCount++;
                    PetBinder.Binding captured = binding;
                    var choice = new UiButton(() => SelectPet(captured.Id));
                    choice.AddToClassList("pet3d-picker-choice");
                    var thumb = new UiImage
                    {
                        sprite = captured.Thumbnail,
                        scaleMode = ScaleMode.ScaleAndCrop,
                        pickingMode = PickingMode.Ignore
                    };
                    thumb.AddToClassList("pet3d-picker-thumb");
                    choice.Add(thumb);
                    var name = new Label(captured.DisplayName);
                    name.AddToClassList("pet3d-picker-name");
                    choice.Add(name);
                    grid.Add(choice);
                }
            }
            PadPickerGridRow(grid, choiceCount);
            scroll.Add(grid);
            modal.Add(scroll);
            petPicker.Add(modal);
            panel.Add(petPicker);
        }

        private void TogglePetPicker()
        {
            if (binder == null || !binder.CanSwap) return;
            petPicker.style.display = petPicker.style.display == DisplayStyle.Flex
                ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void ClosePetPicker() => petPicker.style.display = DisplayStyle.None;

        private void SelectPet(string id)
        {
            binder?.Bind(id);
            UiPrototypeRuntime.Instance?.SetLeadCompanion(id);
            ClosePetPicker();
        }

        private void BuildInteractionRow()
        {
            VisualElement row = Element(null, "ar-interaction-row");
            panel.Add(row);

            comeCircle = BuildCircleItem(row, "COME HERE", asButton: true, onClick: () => companion?.ComeHere());
            if (whistleIconSprite != null)
            {
                var whistleIcon = new UiImage
                {
                    sprite = whistleIconSprite,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                whistleIcon.AddToClassList("ar-interaction-icon-food");
                whistleIcon.style.width = 100;
                whistleIcon.style.height = 100;
                comeCircle.Add(whistleIcon);
            }
            else
            {
                comeCircle.Add(Icon("whistle", "ar-interaction-icon", White));
            }

            foodCircle = BuildCircleItem(row, "FEED", asButton: false);
            foodCircle.AddToClassList("pet3d-food-circle");
            foodIconImage = new UiImage { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            foodIconImage.AddToClassList("ar-interaction-icon-food");
            foodIconImage.style.width = 100;
            foodIconImage.style.height = 100;
            foodCircle.Add(foodIconImage);
            VisualElement foodQuantityBadge = Element(null, "pet3d-food-quantity");
            foodQuantityLabel = new Label("0");
            foodQuantityLabel.AddToClassList("pet3d-food-quantity-label");
            foodQuantityBadge.Add(foodQuantityLabel);
            foodCircle.Add(foodQuantityBadge);
            var switchFood = new UiButton(() => foodDrag?.SelectNextFood()) { name = "ar-switch-food" };
            switchFood.AddToClassList("pet3d-switch-food");
            switchFood.Add(Icon("swap-horizontal", "pet3d-switch-food-icon", new Color32(42, 63, 49, 255)));
            switchFood.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            foodCircle.Add(switchFood);

            ballCircle = BuildCircleItem(row, "THROW BALL", asButton: false);
            var ballIcon = new UiImage
            {
                sprite = ballIconSprite,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            ballIcon.AddToClassList("ar-interaction-icon-food");
            ballIcon.style.width = 100;
            ballIcon.style.height = 100;
            ballCircle.Add(ballIcon);

            if (feeding != null)
                foodDrag = new ArFoodDragController(foodCircle, hudCamera, feeding, foodChoices);
            if (toyFetch != null && ballPrefab != null)
                ballDrag = new ArBallDragController(ballCircle, hudCamera, toyFetch, ballPrefab);
        }

        private VisualElement BuildCircleItem(VisualElement parent, string labelText, bool asButton, Action onClick = null)
        {
            VisualElement item = Element(null, "ar-interaction-item");
            VisualElement circle = asButton ? new UiButton(onClick) : new VisualElement();
            circle.AddToClassList("ar-interaction-circle");
            item.Add(circle);
            var label = new Label(labelText);
            label.AddToClassList("ar-interaction-label");
            label.AddToClassList("font-display");
            item.Add(label);
            parent.Add(item);
            return circle;
        }

        private void BuildCamCluster()
        {
            VisualElement cluster = Element(null, "ar-cam-cluster");
            panel.Add(cluster);

            // Repurposed from a decorative front/back camera flip (AR passthrough only ever
            // uses the rear camera, so there was nothing real to switch) into a respawn: hides
            // the pet and clears its placement so the player can walk it to a new spot and have
            // it reappear on the next plane found there.
            var flip = new UiButton(() => placement?.Respawn());
            flip.AddToClassList("ar-cam-flip");
            flip.Add(Icon("cameraswitch", "ar-cam-flip-icon", White));
            cluster.Add(flip);

            VisualElement shutterOuter = Element(null, "ar-shutter-outer");
            var shutter = new UiButton(() => photo?.Capture());
            shutter.AddToClassList("ar-shutter-inner");
            shutter.Add(Icon("camera", "ar-shutter-icon", new Color(0.13f, 0.16f, 0.14f)));
            shutterOuter.Add(shutter);
            cluster.Add(shutterOuter);

            var gallery = new UiButton(OpenPhotoViewer);
            gallery.AddToClassList("ar-gallery-button");
            // The photo thumbnail must stay clipped to the circular button, but the fallback
            // "no photos yet" icon should be free to render larger than the button itself - so
            // only the thumbnail sits inside a clipped wrapper; the icon is a free sibling.
            var galleryClip = Element(null, "ar-gallery-clip");
            var galleryImg = new UiImage { scaleMode = ScaleMode.ScaleAndCrop };
            galleryImg.AddToClassList("ar-gallery-thumb");
            galleryClip.Add(galleryImg);
            gallery.Add(galleryClip);
            galleryThumb = galleryImg;
            galleryIconElement = Icon("gallery", "ar-gallery-icon", White);
            galleryIconElement.style.width = 98;
            galleryIconElement.style.height = 98;
            gallery.Add(galleryIconElement);
            VisualElement badge = Element(null, "ar-gallery-badge");
            var badgeLabel = new Label("0");
            badgeLabel.AddToClassList("ar-gallery-badge-label");
            badgeLabel.AddToClassList("font-display");
            badge.Add(badgeLabel);
            gallery.Add(badge);
            galleryBadge = badge;
            galleryBadgeLabel = badgeLabel;
            cluster.Add(gallery);
        }

        private void RefreshGallery()
        {
            if (galleryThumb == null || photo == null)
                return;
            IReadOnlyList<Texture2D> photos = photo.SessionPhotos;
            int count = photos.Count;
            SetDisplay(galleryBadge, count > 0);
            galleryBadgeLabel.text = count.ToString();
            galleryThumb.image = count > 0 ? photos[count - 1] : null;
            SetDisplay(galleryIconElement, count == 0);
            if (photoViewer != null && photoViewer.style.display == DisplayStyle.Flex)
                RefreshViewerStrip();
        }

        private void BuildPhotoViewer()
        {
            photoViewer = Element(null, "ar-photo-viewer");
            photoViewer.style.display = DisplayStyle.None;
            panel.Add(photoViewer);

            var close = new UiButton(ClosePhotoViewer);
            close.AddToClassList("ar-photo-viewer-close");
            close.Add(Icon("x", "ar-photo-viewer-close-icon", White));
            photoViewer.Add(close);

            photoViewerImage = new UiImage { scaleMode = ScaleMode.ScaleToFit };
            photoViewerImage.AddToClassList("ar-photo-viewer-image");
            photoViewer.Add(photoViewerImage);

            photoStrip = new ScrollView(ScrollViewMode.Horizontal);
            photoStrip.AddToClassList("ar-photo-strip");
            photoViewer.Add(photoStrip);
        }

        private void OpenPhotoViewer()
        {
            if (photo == null || photo.SessionPhotos.Count == 0)
                return;
            viewerIndex = photo.SessionPhotos.Count - 1;
            photoViewer.style.display = DisplayStyle.Flex;
            RefreshViewerStrip();
        }

        private void ClosePhotoViewer() => photoViewer.style.display = DisplayStyle.None;

        private void RefreshViewerStrip()
        {
            IReadOnlyList<Texture2D> photos = photo.SessionPhotos;
            if (photos.Count == 0)
            {
                ClosePhotoViewer();
                return;
            }
            viewerIndex = Mathf.Clamp(viewerIndex, 0, photos.Count - 1);
            photoViewerImage.image = photos[viewerIndex];

            photoStrip.Clear();
            for (int i = 0; i < photos.Count; i++)
            {
                int index = i;
                var thumb = new UiImage { image = photos[i], scaleMode = ScaleMode.ScaleAndCrop };
                thumb.AddToClassList("ar-photo-strip-item");
                if (index == viewerIndex) thumb.AddToClassList("ar-photo-strip-item-selected");
                thumb.RegisterCallback<PointerDownEvent>(_ => { viewerIndex = index; RefreshViewerStrip(); });
                photoStrip.Add(thumb);
            }
        }

        private void PlayFlash()
        {
            if (flashOverlay == null)
            {
                flashOverlay = new VisualElement { pickingMode = PickingMode.Ignore };
                flashOverlay.style.position = Position.Absolute;
                flashOverlay.style.left = 0; flashOverlay.style.right = 0;
                flashOverlay.style.top = 0; flashOverlay.style.bottom = 0;
                flashOverlay.style.backgroundColor = Color.white;
                panel.Add(flashOverlay);
            }
            flashOverlay.style.opacity = 1f;
            flashOverlay.experimental.animation.Start(1f, 0f, 250, (element, value) => element.style.opacity = value);
        }

        private void ShowToast(string message)
        {
            var toast = new Label(message);
            toast.AddToClassList("toast");
            panel.notificationContainer.Add(toast);
            toast.schedule.Execute(toast.RemoveFromHierarchy).StartingIn(2200);
        }

        // ---- shared UI Toolkit helpers (mirrors WalkUiController's own copy of this pattern) ----

        private UiImage Icon(string key, string className, Color tint)
        {
            if (!icons.TryGetValue(key, out VectorImage vector))
            {
                vector = Resources.Load<VectorImage>("UI/Icons/" + key);
                icons[key] = vector;
            }
            var image = new UiImage { vectorImage = vector, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore, tintColor = tint };
            if (!string.IsNullOrEmpty(className)) image.AddToClassList(className);
            return image;
        }

        private static VisualElement Element(string name, params string[] classes)
        {
            var element = new VisualElement { name = name };
            foreach (string item in classes)
                if (!string.IsNullOrEmpty(item)) element.AddToClassList(item);
            return element;
        }

        private static void SetDisplay(VisualElement element, bool visible)
        {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The picker grid uses <c>justify-content: space-between</c> so full rows of 3
        /// space their cards evenly, but that same rule stretches an incomplete last row's cards
        /// out to the container's edges instead of packing them to the left. Padding the row out
        /// to a full 3 with invisible, same-width filler elements keeps real cards left-aligned
        /// without needing CSS Grid or :nth-child (neither available in UI Toolkit's USS subset).</summary>
        private static void PadPickerGridRow(VisualElement grid, int itemCount)
        {
            var remainder = itemCount % 3;
            if (remainder == 0) return;
            for (var i = 0; i < 3 - remainder; i++)
            {
                var filler = new VisualElement { pickingMode = PickingMode.Ignore };
                filler.AddToClassList("pet3d-picker-choice");
                filler.style.visibility = Visibility.Hidden;
                grid.Add(filler);
            }
        }
    }
}
