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
        int _memoryPage;
        bool _stampCollected;

        public UiRoute CurrentRoute => _runtime.Navigator.CurrentRoute;

        void Start()
        {
            _runtime = UiPrototypeRuntime.EnsureExists();
            _document = GetComponent<UIDocument>();
            if (_document.panelSettings == null) _document.panelSettings = Resources.Load<PanelSettings>("UI/ARWalkingPanelSettings");
            var root = _document.rootVisualElement;
            root.Clear();
            var styleSheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            _panel = new AppPanel { name = "ar-walking-ar-panel", theme = "light", scale = "medium" };
            _panel.AddToClassList("app-root"); root.Add(_panel);
            _safeRoot = new VisualElement { name = "safe-area" }; _safeRoot.AddToClassList("safe-area"); _panel.Add(_safeRoot);
            _document.rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
            ApplySafeArea();
            _runtime.Navigator.Changed += Render;
            _runtime.CompanionTapped += OnCompanionTapped;
            if (_runtime.Navigator.CurrentRoute != UiRoute.LandmarkArMemory && _runtime.Navigator.CurrentRoute != UiRoute.ArPhoto)
                _runtime.Navigator.Push(UiRoute.LandmarkArMemory);
            else Render();
            ShowToast("Camera permission is requested here when the real AR camera is connected.");
        }

        void OnDisable()
        {
            if (_runtime == null) return;
            _runtime.Navigator.Changed -= Render;
            _runtime.CompanionTapped -= OnCompanionTapped;
        }

        void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != _lastSafeArea || screenSize != _lastScreenSize) ApplySafeArea();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) BackOrExit();
        }

        public void SimulateImageTargetRecognition() { _memoryPage = 1; Render(); }

        /// <summary>
        /// AR/3D integration hook: call this from the real Vuforia target-found handler once an Image Target
        /// is recognized, instead of the "Simulate recognition" debug button - see docs/AR-3D-INTEGRATION-CONTRACT.md.
        /// The debug button stays wired to the same logic as a demo fallback if live recognition is unreliable.
        /// </summary>
        public void OnImageTargetRecognized() => SimulateImageTargetRecognition();

        public void NextMemoryPage() { _memoryPage = Mathf.Min(3, _memoryPage + 1); Render(); }
        public LandmarkRewardDto CollectStamp()
        {
            var landmark = _runtime.Data.Landmarks[Mathf.Clamp(_runtime.SelectedLandmarkIndex, 0, _runtime.Data.Landmarks.Count - 1)];
            var result = _runtime.CompleteLandmarkMemory(landmark.id);
            _stampCollected = true;
            var unlockedName = string.IsNullOrEmpty(result.unlockedCompanionId) ? null : CompanionName(result.unlockedCompanionId);
            ShowToast(result.newlyCompleted
                ? "Stamp collected" + (unlockedName != null ? " - " + unlockedName + " unlocked!" : string.Empty)
                : "This Landmark reward was already collected.");
            Render();
            return result;
        }

        void OnCompanionTapped(string companionId) => ShowToast(CompanionName(companionId) + " reacted!");
        string CompanionName(string id) { foreach (var item in _runtime.Data.Companions) if (item.id == id) return item.name; return id; }

        public void OpenPhoto() => _runtime.Navigator.Push(UiRoute.ArPhoto);
        public void ExitToHome() => _runtime.ReturnFromArToHome();
        public void BackOrExit()
        {
            if (_runtime.Navigator.CurrentRoute == UiRoute.ArPhoto) _runtime.Navigator.Back();
            else ExitToHome();
        }

        void Render()
        {
            if (_safeRoot == null) return;
            _safeRoot.Clear();
            if (_runtime.Navigator.CurrentRoute == UiRoute.ArPhoto) BuildPhotoPreview();
            else BuildLandmarkAr();
        }

        void BuildLandmarkAr()
        {
            var landmark = _runtime.Data.Landmarks[Mathf.Clamp(_runtime.SelectedLandmarkIndex, 0, _runtime.Data.Landmarks.Count - 1)];
            var page = new VisualElement { name = "landmark-ar-memory-screen" }; page.AddToClassList("ar-page");
            var backdrop = new UiImage { image = _runtime.Assets.arScene, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            backdrop.AddToClassList("ar-backdrop"); page.Add(backdrop);
            var top = new VisualElement(); top.AddToClassList("ar-top-bar");
            top.Add(IconButton(_runtime.Assets.iconBack, "BACK", ExitToHome, "ar-exit"));
            var copy = Column(); copy.Add(Label(landmark.name, "title")); copy.Add(Label("Simulated Image Target demo", "ar-alert")); top.Add(copy);
            top.Add(IconButton(_runtime.Assets.iconCamera, "PIC", OpenPhoto, "ar-photo")); page.Add(top);

            var guide = new VisualElement(); guide.AddToClassList("ar-guide");
            if (_memoryPage == 0)
            {
                guide.Add(Label("Point the camera at the Central Post Office Image Target", "body"));
                guide.Add(Button("Simulate recognition", SimulateImageTargetRecognition, "primary-action"));
            }
            else
            {
                var heading = _memoryPage == 1 ? "History" : _memoryPage == 2 ? "Architecture" : "Did You Know?";
                var body = _memoryPage == 1 ? landmark.history : _memoryPage == 2 ? landmark.architecture : landmark.didYouKnow;
                guide.Add(Label(heading, "subtitle")); guide.Add(Label(body, "body"));
                if (_memoryPage < 3) guide.Add(Button("Next", NextMemoryPage, "primary-action"));
                else if (!_stampCollected) guide.Add(Button("Collect Landmark Stamp", () => CollectStamp(), "primary-action"));
                else guide.Add(Button("View Journey", () => _runtime.ReturnHome(UiRootTab.Journey), "primary-action"));
            }
            page.Add(guide);

            var companion = new UiImage { image = _runtime.Assets.Companion(0), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            companion.AddToClassList("ar-companion"); page.Add(companion);
            _safeRoot.Add(page);
        }

        void BuildPhotoPreview()
        {
            var page = new VisualElement { name = "ar-photo-preview-screen" }; page.AddToClassList("ar-page");
            var preview = new UiImage { image = _runtime.Assets.arScene, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            preview.AddToClassList("photo-preview"); page.Add(preview);
            var companion = new UiImage { image = _runtime.Assets.Companion(0), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            companion.AddToClassList("photo-companion"); page.Add(companion);
            var top = new VisualElement(); top.AddToClassList("ar-top-bar");
            top.Add(IconButton(_runtime.Assets.iconBack, "BACK", () => _runtime.Navigator.Back(), "photo-back")); top.Add(Label("AR photo preview", "title")); page.Add(top);
            var card = new VisualElement(); card.AddToClassList("photo-caption-card");
            card.Add(Label("Dog at Central Post Office", "subtitle")); card.Add(Label("Temporary preview; real camera capture is not connected", "body")); page.Add(card);
            var actions = new VisualElement(); actions.AddToClassList("photo-actions");
            actions.Add(Button("Retake", () => _runtime.Navigator.Back(), "secondary-action"));
            actions.Add(Button("Save photo path", SavePhoto, "primary-action")); page.Add(actions);
            _safeRoot.Add(page);
        }

        public void SavePhoto()
        {
            _runtime.SaveArPhoto();
            ShowToast("Photo path saved to the local profile");
            _runtime.Navigator.Back();
        }

        void ShowToast(string message)
        {
            var toast = Label(message, "toast"); _panel.notificationContainer.Add(toast);
            toast.schedule.Execute(toast.RemoveFromHierarchy).StartingIn(2200);
        }

        static VisualElement Column() { var value = new VisualElement(); value.AddToClassList("column"); return value; }
        static Label Label(string text, string className) { var label = new Label(text); label.AddToClassList(className); return label; }
        static UiButton IconButton(Texture2D icon, string fallback, System.Action action, string name) { var button = new UiButton(action) { name = name }; button.AddToClassList("icon-button"); if (icon != null) { var image = new UiImage { image = icon, name = "icon-image", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore, tintColor = Color.black }; button.Add(image); } else button.text = fallback; return button; }
        static UiButton Button(string text, System.Action action, string className) { var button = new UiButton(action) { text = text }; button.AddToClassList("action-button"); button.AddToClassList(className); return button; }

        void ApplySafeArea()
        {
            if (_safeRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            var safe = UiSafeAreaSimulation.Resolve(Screen.safeArea);
            var panelHeight = _document.rootVisualElement.resolvedStyle.height;
            var scale = float.IsNaN(panelHeight) || panelHeight <= 0f ? 1f : panelHeight / Screen.height;
            _safeRoot.style.left = safe.xMin * scale;
            _safeRoot.style.right = (Screen.width - safe.xMax) * scale;
            _safeRoot.style.top = (Screen.height - safe.yMax) * scale;
            _safeRoot.style.bottom = safe.yMin * scale;
            _lastSafeArea = safe; _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
