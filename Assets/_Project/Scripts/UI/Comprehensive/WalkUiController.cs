using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UIElements;
using AppPanel = Unity.AppUI.UI.Panel;
using UiButton = UnityEngine.UIElements.Button;
using UiImage = UnityEngine.UIElements.Image;

namespace ARWalking.UI
{
    /// <summary>
    /// Landmark AR Memory overlay (History -> Architecture -> Did You Know -> Collect Stamp),
    /// rendered as a UI Toolkit panel layered on top of the real AR camera in the shared
    /// "PetAr" scene. Only active when <see cref="PetArSceneContext.LandmarkId"/> is set - the
    /// plain pet-viewing flows (Photo/Feed/Companion/Walk) leave this panel empty and rely on
    /// CorgiAR's uGUI HUD instead. See docs/AR-3D-INTEGRATION-CONTRACT.md.
    /// </summary>
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
        public bool HasLandmarkMemory => !string.IsNullOrEmpty(PetArSceneContext.LandmarkId);

        void Start()
        {
            _runtime = UiPrototypeRuntime.EnsureExists();
            _document = GetComponent<UIDocument>();
            if (_document.panelSettings == null) _document.panelSettings = Resources.Load<PanelSettings>("UI/ARWalkingArPanelSettings");
            var root = _document.rootVisualElement;
            root.Clear();
            var styleSheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            _panel = new AppPanel { name = "ar-walking-ar-panel", theme = "light", scale = "medium" };
            _panel.AddToClassList("app-root"); _panel.AddToClassList("ar-panel-root"); root.Add(_panel);
            // Inline styles always win over any USS rule, including the App UI package's own
            // theme stylesheet (Panel's "light" theme paints an opaque app background by design -
            // no class selector in our own stylesheet can out-rank that). This is the one place
            // that must actually be transparent so the real AR camera shows through.
            _panel.style.backgroundColor = new StyleColor(Color.clear);
            root.style.backgroundColor = new StyleColor(Color.clear);
            _safeRoot = new VisualElement { name = "safe-area" }; _safeRoot.AddToClassList("safe-area"); _panel.Add(_safeRoot);
            _safeRoot.style.backgroundColor = new StyleColor(Color.clear);
            _document.rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
            ApplySafeArea();
            _runtime.Navigator.Changed += Render;
            _runtime.CompanionTapped += OnCompanionTapped;
            _memoryPage = 0;
            _stampCollected = false;
            Render();
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
        }

        public void SimulateImageTargetRecognition() { _memoryPage = 1; Render(); }

        /// <summary>
        /// AR/3D integration hook: call this from the real AR Foundation image-target/plane
        /// recognition handler once the Landmark is recognized, instead of the "Simulate
        /// recognition" debug button - see docs/AR-3D-INTEGRATION-CONTRACT.md. The debug button
        /// stays wired to the same logic as a demo fallback if live recognition is unreliable.
        /// </summary>
        public void OnImageTargetRecognized() => SimulateImageTargetRecognition();

        public void NextMemoryPage() { _memoryPage = Mathf.Min(3, _memoryPage + 1); Render(); }
        public LandmarkRewardDto CollectStamp()
        {
            var result = _runtime.CompleteLandmarkMemory(PetArSceneContext.LandmarkId);
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

        public void ExitToHome() => _runtime.ReturnFromPetAr();

        void Render()
        {
            if (_safeRoot == null) return;
            _safeRoot.Clear();

            // Always-present top bar with a Back button: CorgiAR's uGUI HUD has no exit control
            // of its own (it was a standalone sandbox scene), so this UI Toolkit overlay - already
            // layered above the AR camera and the HUD Canvas - is the one place every PetAr entry
            // point (plain pet viewing included) gets a way back to Home.
            var page = new VisualElement { name = "pet-ar-top-bar-screen" }; page.AddToClassList("ar-page");
            page.pickingMode = PickingMode.Ignore;
            page.style.backgroundColor = new StyleColor(Color.clear);
            var top = new VisualElement(); top.AddToClassList("ar-top-bar");
            top.Add(IconButton(_runtime.Assets.iconBack, "BACK", ExitToHome, "ar-exit"));
            page.Add(top);
            _safeRoot.Add(page);

            if (HasLandmarkMemory) BuildLandmarkAr(page, top);
        }

        void BuildLandmarkAr(VisualElement page, VisualElement top)
        {
            var landmarkId = PetArSceneContext.LandmarkId;
            LandmarkUiData landmark = null;
            foreach (var candidate in _runtime.Data.Landmarks)
                if (candidate.id == landmarkId) landmark = candidate;
            if (landmark == null) return;

            var copy = Column(); copy.Add(Label(landmark.name, "title")); copy.Add(Label("AR Memory", "ar-alert")); top.Add(copy);

            var guide = new VisualElement(); guide.AddToClassList("ar-guide");
            if (_memoryPage == 0)
            {
                guide.Add(Label("Point the camera at " + landmark.name, "body"));
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
