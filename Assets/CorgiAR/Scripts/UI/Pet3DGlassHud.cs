using System;
using System.Collections.Generic;
using ARWalking.UI;
using ShibaFeeding;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.VectorGraphics;
using AppPanel = Unity.AppUI.UI.Panel;
using UiButton = UnityEngine.UIElements.Button;
using UiImage = UnityEngine.UIElements.Image;

namespace CorgiAR.UI
{
    /// <summary>
    /// SampleScene HUD built from the same UI Toolkit controls and ARWalking.uss classes as
    /// CorgiArGlassHud. The legacy uGUI canvas remains only as a host for the existing food and
    /// ball gameplay components; it is not rendered while this HUD is active.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class Pet3DGlassHud : MonoBehaviour
    {
        private UIDocument document;
        private AppPanel panel;
        private VisualElement safeRoot;
        private Label statusLabel;
        private UiImage changePetThumb;
        private VisualElement interactionRow;
        private VisualElement comeItem;
        private VisualElement foodItem;
        private VisualElement ballItem;
        private UiImage foodIcon;
        private Label foodQuantity;
        private VisualElement petPicker;

        private DogARPlacementController placement;
        private DogCompanionController companion;
        private DogFeedingController feeding;
        private PetMoodController mood;
        private PetBinder binder;
        private FoodDragThrowUI foodDrag;
        private ToyDragThrowUI ballDrag;
        private UnityEngine.Canvas legacyCanvas;
        private CorgiArHud legacyHud;
        private Camera worldCamera;
        private bool isReturningToApp;
        private bool isSwitchingToAr;

        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private readonly Dictionary<string, VectorImage> icons = new();

        private void Awake()
        {
            placement = FindFirstObjectByType<DogARPlacementController>();
            companion = FindFirstObjectByType<DogCompanionController>();
            feeding = FindFirstObjectByType<DogFeedingController>();
            mood = FindFirstObjectByType<PetMoodController>();
            binder = FindFirstObjectByType<PetBinder>();
            foodDrag = FindFirstObjectByType<FoodDragThrowUI>();
            ballDrag = FindFirstObjectByType<ToyDragThrowUI>();
            legacyHud = FindFirstObjectByType<CorgiArHud>();
            legacyCanvas = legacyHud != null ? legacyHud.GetComponent<UnityEngine.Canvas>() : null;
            worldCamera = placement != null && placement.ArCamera != null ? placement.ArCamera : Camera.main;
        }

        private void Start()
        {
            if (legacyHud != null) legacyHud.enabled = false;
            if (legacyCanvas != null) legacyCanvas.enabled = false;

            // Pet3DModeController can replace the AR camera with the meadow camera in Start,
            // after this component's Awake has already run. Resolve it again before wiring drag rays.
            worldCamera = placement != null && placement.ArCamera != null ? placement.ArCamera : Camera.main;
            if (foodDrag != null) foodDrag.SetCamera(worldCamera);
            if (ballDrag != null) ballDrag.SetCamera(worldCamera);

            document = GetComponent<UIDocument>();
            // SampleScene serializes the same PanelSettings as PetAr. Keep this fallback only
            // for older/generated scenes that do not yet contain the preconfigured document.
            if (document.panelSettings == null)
                document.panelSettings = Resources.Load<PanelSettings>("UI/ARWalkingArPanelSettings");
            VisualElement root = document.rootVisualElement;
            root.Clear();
            StyleSheet sheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (sheet != null) root.styleSheets.Add(sheet);
            root.style.backgroundColor = new StyleColor(Color.clear);
            root.pickingMode = PickingMode.Ignore;

            panel = new AppPanel { name = "pet-3d-glass-hud", theme = "light", scale = "medium" };
            panel.AddToClassList("app-root");
            panel.AddToClassList("ar-panel-root");
            panel.style.backgroundColor = new StyleColor(Color.clear);
            panel.pickingMode = PickingMode.Ignore;
            root.Add(panel);

            safeRoot = Element("pet-3d-safe-area");
            safeRoot.style.position = Position.Absolute;
            safeRoot.style.left = 0;
            safeRoot.style.right = 0;
            safeRoot.style.top = 0;
            safeRoot.style.bottom = 0;
            safeRoot.pickingMode = PickingMode.Ignore;
            panel.Add(safeRoot);

            BuildTopControls();
            BuildInteractionRow();
            BuildPetPicker();

            if (binder != null) binder.PetChanged += RefreshPet;
            if (foodDrag != null) foodDrag.FoodVisualChanged += RefreshFood;
            RefreshPet(binder != null ? binder.CurrentId : null);
            RefreshFood();

            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
            ApplySafeArea();
        }

        private void OnDisable()
        {
            if (binder != null) binder.PetChanged -= RefreshPet;
            if (foodDrag != null) foodDrag.FoodVisualChanged -= RefreshFood;
        }

        private void Update()
        {
            Vector2Int size = new(Screen.width, Screen.height);
            if (Screen.safeArea != lastSafeArea || size != lastScreenSize) ApplySafeArea();

            bool placed = placement != null && placement.IsPlaced;
            bool eating = feeding != null && feeding.IsEating;
            bool sitting = companion != null && companion.IsSitting;
            SetDisplay(interactionRow, placed);
            SetDisplay(comeItem, placed && !eating && !sitting);
            SetDisplay(foodItem, placed);
            SetDisplay(ballItem, placed && !eating);
            if (foodDrag != null && foodDrag.gameObject.activeSelf != placed)
                foodDrag.gameObject.SetActive(placed);
            if (ballDrag != null && ballDrag.gameObject.activeSelf != (placed && !eating))
                ballDrag.gameObject.SetActive(placed && !eating);

            if (statusLabel != null)
                statusLabel.text = StatusFor(placed, eating, sitting);
        }

        private void BuildTopControls()
        {
            if (Pet3DSceneContext.IsActive)
            {
                // Match WalkUiController's AR Back hierarchy exactly so the shared USS sizes
                // and positions this control through the same layout context on every device.
                VisualElement navigationPage = Element("pet-3d-top-bar-screen", "ar-page");
                navigationPage.pickingMode = PickingMode.Ignore;
                VisualElement topBar = Element("pet-3d-top-bar", "ar-top-bar");
                topBar.pickingMode = PickingMode.Ignore;
                var back = new UiButton(ReturnToApp) { name = "pet-3d-exit" };
                back.AddToClassList("icon-button");
                back.AddToClassList("dark-round-control");
                back.Add(Icon("chevron-left", "icon-image", Color.white));
                topBar.Add(back);

                var openAr = new UiButton(SwitchToAr) { name = "pet-3d-open-ar" };
                openAr.AddToClassList("icon-button");
                openAr.AddToClassList("dark-round-control");
                openAr.AddToClassList("pet3d-ar-mode-button");
                var arLabel = new Label("AR") { pickingMode = PickingMode.Ignore };
                arLabel.AddToClassList("pet3d-ar-mode-label");
                arLabel.AddToClassList("font-display");
                openAr.Add(arLabel);
                topBar.Add(openAr);
                navigationPage.Add(topBar);
                safeRoot.Add(navigationPage);
            }

            VisualElement topStack = Element(null, "ar-top-stack");
            // This full-width layout overlaps the lower part of the Back button. Only its
            // interactive children should receive pointer events, otherwise Back needs a tap
            // in the small uncovered strip at the top.
            topStack.pickingMode = PickingMode.Ignore;
            VisualElement statusRow = Element(null, "ar-status-row");
            statusRow.pickingMode = PickingMode.Ignore;
            if (Pet3DSceneContext.IsActive)
                statusRow.AddToClassList("pet3d-status-row-with-mode-switch");
            else
                statusRow.AddToClassList("pet3d-status-row-no-back");
            VisualElement statusPill = Element(null, "ar-status-pill");
            statusPill.pickingMode = PickingMode.Ignore;
            statusLabel = new Label();
            statusLabel.AddToClassList("ar-status-label");
            statusLabel.AddToClassList("font-display");
            statusPill.Add(statusLabel);
            statusRow.Add(statusPill);
            topStack.Add(statusRow);

            var card = new UiButton(TogglePetPicker) { name = "pet-3d-change-pet" };
            card.AddToClassList("ar-change-pet-card");
            changePetThumb = new UiImage { name = "change-pet-thumb", scaleMode = ScaleMode.ScaleAndCrop };
            changePetThumb.AddToClassList("ar-change-pet-thumb");
            card.Add(changePetThumb);
            VisualElement copy = Element(null);
            var kicker = new Label("ĐANG CHỌN");
            kicker.AddToClassList("ar-change-pet-kicker");
            kicker.AddToClassList("font-display");
            var title = new Label("Đổi thú");
            title.AddToClassList("ar-change-pet-title");
            title.AddToClassList("font-display");
            copy.Add(kicker);
            copy.Add(title);
            card.Add(copy);
            card.Add(Icon("swap-horizontal", "ar-change-pet-swap", Color.white));
            topStack.Add(card);
            safeRoot.Add(topStack);
        }

        private void BuildInteractionRow()
        {
            interactionRow = Element("pet-3d-interaction-row", "ar-interaction-row", "pet3d-interaction-row");
            safeRoot.Add(interactionRow);

            VisualElement comeCircle = BuildCircleItem(out comeItem, "GỌI VỀ", true, () => companion?.ComeHere());
            comeCircle.Add(SpriteImage(Resources.Load<Sprite>("UI/Icons/whistle3d"), "ar-interaction-icon-food"));

            VisualElement foodCircle = BuildCircleItem(out foodItem, "CHO ĂN", false, null);
            foodCircle.AddToClassList("pet3d-food-circle");
            foodIcon = SpriteImage(null, "ar-interaction-icon-food");
            foodCircle.Add(foodIcon);
            VisualElement quantityBadge = Element("pet-3d-food-quantity", "pet3d-food-quantity");
            foodQuantity = new Label("0");
            foodQuantity.AddToClassList("pet3d-food-quantity-label");
            quantityBadge.Add(foodQuantity);
            foodCircle.Add(quantityBadge);
            var switchFood = new UiButton(() => foodDrag?.SelectNextFood()) { name = "pet-3d-switch-food" };
            switchFood.AddToClassList("pet3d-switch-food");
            switchFood.Add(Icon("swap-horizontal", "pet3d-switch-food-icon", new Color32(42, 63, 49, 255)));
            switchFood.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            foodCircle.Add(switchFood);
            if (foodDrag != null)
                WireDrag(foodCircle, foodDrag.BeginExternalDrag, foodDrag.ContinueExternalDrag, foodDrag.EndExternalDrag);

            VisualElement ballCircle = BuildCircleItem(out ballItem, "NÉM BÓNG", false, null);
            ballCircle.Add(SpriteImage(Resources.Load<Sprite>("UI/Icons/ball"), "ar-interaction-icon-food"));
            if (ballDrag != null)
                WireDrag(ballCircle, ballDrag.BeginExternalDrag, ballDrag.ContinueExternalDrag, ballDrag.EndExternalDrag);
        }

        private VisualElement BuildCircleItem(out VisualElement item, string labelText, bool button, Action action)
        {
            item = Element(null, "ar-interaction-item");
            VisualElement circle = button ? new UiButton(action) : new VisualElement();
            circle.AddToClassList("ar-interaction-circle");
            item.Add(circle);
            var label = new Label(labelText);
            label.AddToClassList("ar-interaction-label");
            label.AddToClassList("font-display");
            item.Add(label);
            interactionRow.Add(item);
            return circle;
        }

        private void BuildPetPicker()
        {
            petPicker = Element("pet-3d-pet-picker", "overlay-scrim", "pet3d-picker-scrim");
            petPicker.style.display = DisplayStyle.None;
            var modal = Element(null, "pet3d-picker-card");
            var header = Element(null, "pet3d-picker-header");
            var title = new Label("Chọn thú cưng");
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
            if (binder != null)
            {
                UiPrototypeRuntime runtime = UiPrototypeRuntime.Instance;
                foreach (PetBinder.Binding binding in binder.Bindings)
                {
                    // App entry respects saved unlocks. Direct SampleScene testing has no app
                    // profile, so keep the complete source-project picker available there.
                    // "Owned", not just distance-Unlocked - see PetBinder.IsUnlocked.
                    CompanionProgressData progress = runtime != null ? runtime.Companion(binding.Id) : null;
                    if (Pet3DSceneContext.IsActive && (progress == null || !progress.owned)) continue;
                    PetBinder.Binding captured = binding;
                    var choice = new UiButton(() => SelectPet(captured.Id));
                    choice.AddToClassList("pet3d-picker-choice");
                    choice.Add(SpriteImage(captured.Thumbnail, "pet3d-picker-thumb"));
                    var name = new Label(captured.DisplayName);
                    name.AddToClassList("pet3d-picker-name");
                    choice.Add(name);
                    grid.Add(choice);
                }
            }
            scroll.Add(grid);
            modal.Add(scroll);
            petPicker.Add(modal);
            safeRoot.Add(petPicker);
        }

        private void WireDrag(VisualElement element, Action<Vector2> begin,
            Action<Vector2> move, Action<Vector2> end)
        {
            int pointerId = -1;
            Vector2 lastScreenPosition = Vector2.zero;
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                pointerId = evt.pointerId;
                element.CapturePointer(pointerId);
                lastScreenPosition = PointerScreenPosition(evt.position);
                begin(lastScreenPosition);
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId != pointerId) return;
                lastScreenPosition = PointerScreenPosition(evt.position);
                move(lastScreenPosition);
            });
            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.pointerId != pointerId) return;
                int releasedPointer = pointerId;
                pointerId = -1;
                lastScreenPosition = PointerScreenPosition(evt.position);
                end(lastScreenPosition);
                element.ReleasePointer(releasedPointer);
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (pointerId < 0) return;
                pointerId = -1;
                end(lastScreenPosition);
            });
        }

        private Vector2 PointerScreenPosition(Vector2 panelPosition)
        {
            if (Pointer.current != null) return Pointer.current.position.ReadValue();
            float width = document.rootVisualElement.resolvedStyle.width;
            float height = document.rootVisualElement.resolvedStyle.height;
            if (width <= 0f || height <= 0f) return panelPosition;
            return new Vector2(panelPosition.x / width * Screen.width,
                Screen.height - panelPosition.y / height * Screen.height);
        }

        private void RefreshPet(string unused)
        {
            if (changePetThumb == null || binder == null) return;
            foreach (PetBinder.Binding binding in binder.Bindings)
            {
                if (binding.Id != binder.CurrentId) continue;
                changePetThumb.sprite = binding.Thumbnail;
                return;
            }
        }

        private void RefreshFood()
        {
            if (foodIcon == null || foodDrag == null) return;
            foodIcon.sprite = foodDrag.SelectedFoodIcon;
            foodIcon.style.opacity = foodDrag.SelectedFoodQuantity > 0 ? 1f : 0.35f;
            if (foodQuantity != null) foodQuantity.text = foodDrag.SelectedFoodQuantity.ToString();
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

        private static string StatusFor(bool placed, bool eating, bool sitting)
        {
            if (!placed) return "Chạm màn hình để đặt thú cưng";
            if (eating) return "Đang ăn… ngon quá!";
            if (sitting) return "Thú đang ngồi ngoan";
            return "Chạm thú để vuốt • ném bóng, cho ăn, gọi lại đây";
        }

        private void ReturnToApp()
        {
            if (isReturningToApp) return;
            isReturningToApp = true;
            UiPrototypeRuntime runtime = UiPrototypeRuntime.Instance;
            if (runtime != null)
            {
                runtime.ReturnFromPet3D();
                return;
            }

            // Defensive fallback for a scene opened with an active context before the app
            // runtime has finished initializing. A Back tap must never silently do nothing.
            Pet3DSceneContext.Clear();
            SceneManager.LoadScene("Home");
        }

        private void SwitchToAr()
        {
            if (isSwitchingToAr || isReturningToApp) return;
            UiPrototypeRuntime runtime = UiPrototypeRuntime.Instance;
            if (runtime == null) return;

            string selectedPetId = binder != null ? binder.CurrentId : Pet3DSceneContext.PetId;
            if (string.IsNullOrEmpty(selectedPetId)) return;

            isSwitchingToAr = true;
            runtime.SwitchPet3DToAr(selectedPetId);
        }

        private void ApplySafeArea()
        {
            if (safeRoot == null || document == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = UiSafeAreaSimulation.Resolve(Screen.safeArea);
            float panelHeight = document.rootVisualElement.resolvedStyle.height;
            float scale = float.IsNaN(panelHeight) || panelHeight <= 0f ? 1f : panelHeight / Screen.height;
            safeRoot.style.left = safe.xMin * scale;
            safeRoot.style.right = (Screen.width - safe.xMax) * scale;
            safeRoot.style.top = (Screen.height - safe.yMax) * scale;
            safeRoot.style.bottom = safe.yMin * scale;
            lastSafeArea = safe;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }

        private UiImage Icon(string key, string className, Color tint)
        {
            if (!icons.TryGetValue(key, out VectorImage vector))
            {
                vector = Resources.Load<VectorImage>("UI/Icons/" + key);
                icons[key] = vector;
            }
            var image = new UiImage
            {
                // Match WalkUiController.Icon(): ARWalking.uss sizes shared controls through
                // ID selectors such as `.icon-button #icon-image`, not the CSS class alone.
                name = className,
                vectorImage = vector,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
                tintColor = tint
            };
            image.AddToClassList(className);
            return image;
        }

        private static UiImage SpriteImage(Sprite sprite, string className)
        {
            var image = new UiImage
            {
                sprite = sprite,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            image.AddToClassList(className);
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
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
