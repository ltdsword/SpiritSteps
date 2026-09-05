using System;
using System.Collections.Generic;
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
    /// UI Toolkit rebuild of the in-AR gameplay HUD (status pill, "Đổi thú" card, Gọi về/Cho
    /// ăn/Ném bóng row, camera cluster, photo viewer) - replaces the old uGUI
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
        [SerializeField] private GameObject foodPrefab;
        [SerializeField] private GameObject ballPrefab;
        [SerializeField] private Sprite foodIconSprite;
        [SerializeField] private Sprite ballIconSprite;

        private static readonly Color White = Color.white;

        private UIDocument document;
        private AppPanel panel;
        private Label statusLabelRef;
        private UiImage changePetThumb;
        private VisualElement comeCircle;
        private VisualElement foodCircle;
        private VisualElement ballCircle;
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

            if (binder != null) binder.PetChanged += OnPetChanged;
            if (photo != null)
            {
                photo.SetHudDocument(document);
                photo.PhotosChanged += RefreshGallery;
                photo.FlashRequested += PlayFlash;
                photo.ToastRequested += ShowToast;
            }
            RefreshChangePetThumb();
            RefreshGallery();
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
                    ? "Đã tìm thấy mặt phẳng — đang đặt thú…"
                    : "Đưa camera quét từ từ quanh sàn/bàn để tìm mặt phẳng…";
            if (eating) return "Đang ăn… ngon quá!";
            if (sitting) return "Thú đang ngồi ngoan";

            PetMood m = mood != null ? mood.Mood : PetMood.Happy;
            if (m == PetMood.Starving) return "Thú đang rất đói — cho ăn ngay đi!";
            if (m == PetMood.Hungry) return "Thú hơi đói rồi — ném cho miếng ăn nhé";
            return "Thú tự đi quanh bạn • ném bóng, cho ăn, gọi lại đây";
        }

        // ---- building blocks ----

        private void BuildStatusRow(VisualElement parent)
        {
            VisualElement row = Element(null, "ar-status-row");
            VisualElement pill = Element(null, "ar-status-pill");
            var label = new Label("Đưa camera quét từ từ quanh sàn/bàn để tìm mặt phẳng…");
            label.AddToClassList("ar-status-label");
            label.AddToClassList("font-display");
            pill.Add(label);
            row.Add(pill);
            parent.Add(row);
            statusLabelRef = label;
        }

        private void BuildChangePetCard(VisualElement parent)
        {
            var card = new UiButton(() => binder?.CycleNext());
            card.AddToClassList("ar-change-pet-card");
            var thumb = new UiImage { name = "change-pet-thumb", scaleMode = ScaleMode.ScaleAndCrop };
            thumb.AddToClassList("ar-change-pet-thumb");
            card.Add(thumb);

            VisualElement textColumn = Element(null);
            var kicker = new Label("ĐANG CHỌN");
            kicker.AddToClassList("ar-change-pet-kicker");
            kicker.AddToClassList("font-display");
            var title = new Label("Đổi thú");
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

        private void BuildInteractionRow()
        {
            VisualElement row = Element(null, "ar-interaction-row");
            panel.Add(row);

            comeCircle = BuildCircleItem(row, "GỌI VỀ", asButton: true, onClick: () => companion?.ComeHere());
            comeCircle.Add(Icon("whistle", "ar-interaction-icon", White));

            foodCircle = BuildCircleItem(row, "CHO ĂN", asButton: false);
            var foodIcon = new UiImage { sprite = foodIconSprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            foodIcon.AddToClassList("ar-interaction-icon");
            foodIcon.style.width = 64;
            foodIcon.style.height = 64;
            foodCircle.Add(foodIcon);

            ballCircle = BuildCircleItem(row, "NÉM BÓNG", asButton: false);
            var ballIcon = new UiImage
            {
                sprite = ballIconSprite,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            ballIcon.AddToClassList("ar-interaction-icon");
            ballIcon.style.width = 64;
            ballIcon.style.height = 64;
            ballCircle.Add(ballIcon);

            if (feeding != null && foodPrefab != null)
                foodDrag = new ArFoodDragController(foodCircle, hudCamera, feeding, foodPrefab);
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

            // Camera flip is intentionally decorative: AR passthrough only ever uses the rear
            // camera for plane tracking, so there is no real front/back switch to perform.
            var flip = new UiButton(() => { });
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
            var galleryImg = new UiImage { scaleMode = ScaleMode.ScaleAndCrop };
            galleryImg.AddToClassList("ar-gallery-thumb");
            gallery.Add(galleryImg);
            galleryThumb = galleryImg;
            galleryIconElement = Icon("gallery", "ar-gallery-icon", White);
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
    }
}
