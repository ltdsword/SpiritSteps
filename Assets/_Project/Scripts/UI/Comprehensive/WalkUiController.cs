using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using AppPanel = Unity.AppUI.UI.Panel;
using UiButton = UnityEngine.UIElements.Button;
using UiImage = UnityEngine.UIElements.Image;

namespace ARWalking.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class WalkUiController : MonoBehaviour
    {
        UIDocument _document;
        UiPrototypeRuntime _runtime;
        AppPanel _panel;
        VisualElement _safeRoot;
        Rect _lastSafeArea;
        Vector2Int _lastScreenSize;

        public UiRoute CurrentRoute => _runtime.Navigator.CurrentRoute;

        void Start()
        {
            _runtime = UiPrototypeRuntime.EnsureExists();
            _document = GetComponent<UIDocument>();
            if (_document.panelSettings == null)
                _document.panelSettings = Resources.Load<PanelSettings>("UI/ARWalkingPanelSettings");

            var root = _document.rootVisualElement;
            root.Clear();
            var styleSheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            _panel = new AppPanel { name = "ar-walking-ar-panel", theme = "light", scale = "medium" };
            _panel.AddToClassList("app-root");
            root.Add(_panel);
            _safeRoot = new VisualElement { name = "safe-area" };
            _safeRoot.AddToClassList("safe-area");
            _panel.Add(_safeRoot);
            _document.rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
            ApplySafeArea();
            _runtime.Navigator.Changed += Render;
            if (_runtime.Navigator.CurrentRoot != UiRootTab.WalkAr)
                _runtime.Navigator.SwitchRoot(UiRootTab.WalkAr);
            else
                Render();
        }

        void OnDisable()
        {
            if (_runtime != null)
                _runtime.Navigator.Changed -= Render;
        }

        void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != _lastSafeArea || screenSize != _lastScreenSize)
                ApplySafeArea();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                BackOrExit();
        }

        public void OpenPhoto() => _runtime.Navigator.Push(UiRoute.ArPhoto);
        public void ExitToHome() => _runtime.ReturnHome();

        public void BackOrExit()
        {
            if (!_runtime.Navigator.Back())
                ExitToHome();
        }

        void Render()
        {
            if (_safeRoot == null) return;
            _safeRoot.Clear();
            if (_runtime.Navigator.CurrentRoute == UiRoute.ArPhoto)
                BuildPhotoPreview();
            else
                BuildArCompanion();
        }

        void BuildArCompanion()
        {
            var page = new VisualElement { name = "ar-companion-screen" };
            page.AddToClassList("ar-page");
            var backdrop = new UiImage { image = _runtime.Assets.arScene, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            backdrop.AddToClassList("ar-backdrop");
            page.Add(backdrop);

            var top = new VisualElement(); top.AddToClassList("ar-top-bar");
            top.Add(IconButton("‹", ExitToHome, "ar-exit"));
            var copy = new VisualElement(); copy.AddToClassList("column");
            copy.Add(Label("Explore together", "title"));
            copy.Add(Label("Keep aware of your surroundings", "ar-alert"));
            top.Add(copy);
            top.Add(IconButton("?", ShowSafetyToast, "ar-help"));
            page.Add(top);

            var guide = new VisualElement(); guide.AddToClassList("ar-guide");
            guide.Add(Label("Move slowly to find a flat surface", "body"));
            page.Add(guide);

            var spirit = new UiImage { image = _runtime.Assets.Spirit(_runtime.SelectedSpiritIndex), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            spirit.AddToClassList("ar-spirit");
            page.Add(spirit);

            var bottom = new VisualElement(); bottom.AddToClassList("ar-bottom-controls");
            bottom.Add(IconButton("✦", () => ShowToast("A memory fragment is nearby"), "ar-discovery"));
            var shutter = IconButton("●", OpenPhoto, "camera-shutter"); shutter.tooltip = "Take AR photo"; bottom.Add(shutter);
            bottom.Add(IconButton("↻", () => ShowToast("Companion recentered"), "ar-recenter"));
            page.Add(bottom);
            _safeRoot.Add(page);
        }

        void BuildPhotoPreview()
        {
            var page = new VisualElement { name = "ar-photo-preview-screen" };
            page.AddToClassList("ar-page");
            var preview = new UiImage { image = _runtime.Assets.arScene, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            preview.AddToClassList("photo-preview"); page.Add(preview);
            var spirit = new UiImage { image = _runtime.Assets.Spirit(_runtime.SelectedSpiritIndex), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            spirit.AddToClassList("photo-spirit"); page.Add(spirit);

            var top = new VisualElement(); top.AddToClassList("ar-top-bar");
            top.Add(IconButton("‹", () => _runtime.Navigator.Back(), "photo-back"));
            top.Add(Label("Photo preview", "title"));
            page.Add(top);

            var card = new VisualElement(); card.AddToClassList("photo-caption-card");
            card.Add(Label("A gentle afternoon with Linh Hồn Lá", "subtitle"));
            card.Add(Label("Quận 1 · Thành phố Hồ Chí Minh", "body"));
            page.Add(card);

            var actions = new VisualElement(); actions.AddToClassList("photo-actions");
            actions.Add(Button(UiStrings.Get("action.retake"), () => _runtime.Navigator.Back(), "secondary-action"));
            actions.Add(Button(UiStrings.Get("action.savePhoto"), SavePhoto, "primary-action"));
            page.Add(actions);
            _safeRoot.Add(page);
        }

        public void SavePhoto()
        {
            _runtime.SavedPhotoCount++;
            ShowToast("Photo saved to your journey");
            _runtime.Navigator.Back();
        }

        void ShowSafetyToast() => ShowToast("Stay aware. Stop walking before using the camera.");

        void ShowToast(string message)
        {
            var toast = Label(message, "toast");
            _panel.notificationContainer.Add(toast);
            toast.schedule.Execute(toast.RemoveFromHierarchy).StartingIn(2000);
        }

        static Label Label(string text, string className)
        {
            var label = new Label(text); label.AddToClassList(className); return label;
        }

        static UiButton IconButton(string glyph, System.Action action, string name)
        {
            var button = new UiButton(action) { text = glyph, name = name }; button.AddToClassList("icon-button"); return button;
        }

        static UiButton Button(string text, System.Action action, string className)
        {
            var button = new UiButton(action) { text = text }; button.AddToClassList("action-button"); button.AddToClassList(className); return button;
        }

        void ApplySafeArea()
        {
            if (_safeRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            var safe = UiSafeAreaSimulation.Resolve(Screen.safeArea);
            var panelHeight = _document.rootVisualElement.resolvedStyle.height;
            var scale = float.IsNaN(panelHeight) || panelHeight <= 0f ? 1f : panelHeight / Screen.height;
            // AR uses absolutely positioned chrome, so inset the containing box itself.
            // Padding is not sufficient for absolute descendants in UI Toolkit.
            _safeRoot.style.left = safe.xMin * scale;
            _safeRoot.style.right = (Screen.width - safe.xMax) * scale;
            _safeRoot.style.top = (Screen.height - safe.yMax) * scale;
            _safeRoot.style.bottom = safe.yMin * scale;
            _lastSafeArea = safe;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
