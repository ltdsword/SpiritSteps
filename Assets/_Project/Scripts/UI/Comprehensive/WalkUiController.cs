using System;
using System.Collections.Generic;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UIElements;
using AppPanel = Unity.AppUI.UI.Panel;
using UiButton = UnityEngine.UIElements.Button;
using UiImage = UnityEngine.UIElements.Image;

namespace ARWalking.UI
{
    /// <summary>
    /// UI Toolkit layer above the shared PetAr scene. Plain pet/photo/feed entries only show the
    /// v0-style exit control; Landmark entries add the scan, story-sheet, and stamp flow.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class WalkUiController : MonoBehaviour
    {
        static readonly Color Ink = new Color32(42, 63, 49, 255);
        static readonly Color Primary = new Color32(84, 190, 107, 255);
        static readonly Color BlossomInk = new Color32(143, 72, 86, 255);
        static readonly Color White = Color.white;

        UIDocument _document;
        UiPrototypeRuntime _runtime;
        AppPanel _panel;
        VisualElement _safeRoot;
        Rect _lastSafeArea;
        Vector2Int _lastScreenSize;
        int _memoryPage;
        bool _stampCollected;
        readonly Dictionary<string, VectorImage> _icons = new Dictionary<string, VectorImage>();

        public UiRoute CurrentRoute => _runtime.Navigator.CurrentRoute;
        public bool HasLandmarkMemory => !string.IsNullOrEmpty(PetArSceneContext.LandmarkId);

        void Start()
        {
            _runtime = UiPrototypeRuntime.EnsureExists();
            _document = GetComponent<UIDocument>();
            if (_document.panelSettings == null)
                _document.panelSettings = Resources.Load<PanelSettings>("UI/ARWalkingArPanelSettings");
            var root = _document.rootVisualElement;
            root.Clear();
            var styleSheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            _panel = new AppPanel { name = "ar-walking-ar-panel", theme = "light", scale = "medium" };
            _panel.AddToClassList("app-root");
            _panel.AddToClassList("ar-panel-root");
            _panel.style.backgroundColor = new StyleColor(Color.clear);
            _panel.pickingMode = PickingMode.Ignore;
            root.style.backgroundColor = new StyleColor(Color.clear);
            root.pickingMode = PickingMode.Ignore;
            root.Add(_panel);
            _safeRoot = Element("safe-area", "safe-area");
            _safeRoot.style.backgroundColor = new StyleColor(Color.clear);
            _safeRoot.pickingMode = PickingMode.Ignore;
            _panel.Add(_safeRoot);
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
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
        public void OnImageTargetRecognized() => SimulateImageTargetRecognition();
        public void NextMemoryPage() { _memoryPage = Mathf.Min(3, _memoryPage + 1); Render(); }

        public LandmarkRewardDto CollectStamp()
        {
            var result = _runtime.CompleteLandmarkMemory(PetArSceneContext.LandmarkId);
            _stampCollected = true;
            var unlockedName = string.IsNullOrEmpty(result.unlockedCompanionId) ? null : CompanionName(result.unlockedCompanionId);
            ShowToast(result.newlyCompleted
                ? "Stamp collected" + (unlockedName != null ? " · " + unlockedName + " unlocked!" : string.Empty)
                : "This Landmark memory is already in your Journey.");
            Render();
            return result;
        }

        public void ExitToHome() => _runtime.ReturnFromPetAr();

        void Render()
        {
            if (_safeRoot == null) return;
            _safeRoot.Clear();
            var page = Element("pet-ar-top-bar-screen", "ar-page");
            page.pickingMode = PickingMode.Ignore;
            page.style.backgroundColor = new StyleColor(Color.clear);
            _safeRoot.Add(page);

            var top = Element("ar-top-bar", "ar-top-bar");
            var exit = IconButton("chevron-left", _runtime.Assets.iconBack, ExitToHome, "ar-exit", "dark-round-control");
            top.Add(exit);
            page.Add(top);
            if (HasLandmarkMemory) BuildLandmarkAr(page, top);
        }

        void BuildLandmarkAr(VisualElement page, VisualElement top)
        {
            var landmark = FindLandmark(PetArSceneContext.LandmarkId);
            if (landmark == null) return;
            var titlePill = Element("ar-landmark-pill", "ar-top-pill");
            titlePill.Add(Label(landmark.name, "title"));
            titlePill.Add(Label("AR Memory", "ar-alert"));
            top.Add(titlePill);
            top.Add(Element(null, "ar-top-spacer"));

            if (_stampCollected)
            {
                BuildCollected(page, landmark);
                return;
            }

            if (_memoryPage == 0)
            {
                page.Add(Element("ar-scanning-frame", "ar-scanning-frame"));
                var controls = Element("ar-scan-controls", "ar-scan-controls");
                var instruction = Element(null, "ar-instruction-pill");
                instruction.Add(Icon("sparkles", "ar-instruction-icon", Primary));
                instruction.Add(Label("Point at " + landmark.name, "body"));
                controls.Add(instruction);
                controls.Add(ActionButton("sparkles", "Reveal the Memory", SimulateImageTargetRecognition, "primary-action", "ar-reveal-button"));
                page.Add(controls);
                return;
            }

            var heading = _memoryPage == 1 ? "History" : _memoryPage == 2 ? "Architecture" : "Did You Know?";
            var body = _memoryPage == 1 ? landmark.history : _memoryPage == 2 ? landmark.architecture : landmark.didYouKnow;
            var sheet = Element("ar-story-sheet", "ar-story-sheet");
            sheet.Add(Element(null, "sheet-handle"));
            var headingRow = Element(null, "ar-story-heading");
            headingRow.Add(Icon("sparkles", "ar-story-heading-icon", Primary));
            headingRow.Add(Label(heading.ToUpperInvariant(), "eyebrow"));
            sheet.Add(headingRow);
            sheet.Add(Label(body, "body"));
            sheet.Add(PageDots(_memoryPage - 1));
            var actions = Element(null, "ar-story-actions");
            if (_memoryPage > 1)
                actions.Add(IconButton("chevron-left", _runtime.Assets.iconBack, () => { _memoryPage--; Render(); }, "ar-memory-previous", "small-round-control"));
            if (_memoryPage < 3)
                actions.Add(ActionButton("chevron-right", "Next", NextMemoryPage, "primary-action"));
            else
                actions.Add(ActionButton("stamp", "Collect Stamp", () => CollectStamp(), "blossom-action"));
            sheet.Add(actions);
            page.Add(sheet);
        }

        VisualElement PageDots(int selected)
        {
            var dots = Element(null, "ar-page-dots");
            for (var i = 0; i < 3; i++)
            {
                var dot = Element(null, "ar-page-dot");
                if (i == selected) dot.AddToClassList("ar-page-dot-active");
                dots.Add(dot);
            }
            return dots;
        }

        void BuildCollected(VisualElement page, LandmarkUiData landmark)
        {
            var collected = Element("ar-memory-collected", "ar-collected");
            var medal = Element(null, "ar-stamp-medallion");
            medal.Add(Icon("stamp", "ar-stamp-icon", White));
            collected.Add(medal);
            collected.Add(Label("Memory Collected!", "title"));
            collected.Add(Label(landmark.name + " was added to your Journey passport.", "body"));
            var actions = Element(null, "ar-collected-actions");
            actions.Add(ActionButton("camera", "Take AR Photo", () => ShowToast("Use the AR camera controls to capture your companion."), "blossom-action"));
            actions.Add(ActionButton("map", "Back to Map", ExitToHome, "secondary-action"));
            collected.Add(actions);
            page.Add(collected);
        }

        LandmarkUiData FindLandmark(string id)
        {
            foreach (var candidate in _runtime.Data.Landmarks) if (candidate.id == id) return candidate;
            return null;
        }

        void OnCompanionTapped(string companionId) => ShowToast(CompanionName(companionId) + " reacted!");
        string CompanionName(string id) { foreach (var item in _runtime.Data.Companions) if (item.id == id) return item.name; return id; }

        void ShowToast(string message)
        {
            var toast = Label(message, "toast");
            _panel.notificationContainer.Add(toast);
            toast.schedule.Execute(toast.RemoveFromHierarchy).StartingIn(2200);
        }

        UiImage Icon(string key, string name, Color tint, Texture2D fallback = null)
        {
            if (!_icons.TryGetValue(key, out var vector))
            {
                vector = Resources.Load<VectorImage>("UI/Icons/" + key);
                _icons[key] = vector;
            }
            var image = new UiImage
            {
                name = name,
                vectorImage = vector,
                image = vector == null ? fallback : null,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
                tintColor = tint
            };
            if (!string.IsNullOrEmpty(name)) image.AddToClassList(name);
            return image;
        }

        UiButton IconButton(string icon, Texture2D fallback, Action action, string name, params string[] classes)
        {
            var button = new UiButton(action) { name = name };
            button.AddToClassList("icon-button");
            foreach (var item in classes) button.AddToClassList(item);
            button.Add(Icon(icon, "icon-image", Array.IndexOf(classes, "dark-round-control") >= 0 ? White : Ink, fallback));
            return button;
        }

        UiButton ActionButton(string icon, string text, Action action, params string[] classes)
        {
            var button = new UiButton(action) { name = text.ToLowerInvariant().Replace(' ', '-') };
            button.AddToClassList("action-button");
            button.AddToClassList("icon-action-button");
            foreach (var item in classes) button.AddToClassList(item);
            var tint = Array.IndexOf(classes, "primary-action") >= 0 ? White : Array.IndexOf(classes, "blossom-action") >= 0 ? BlossomInk : Ink;
            button.Add(Icon(icon, "action-icon", tint));
            button.Add(Label(text, "action-label"));
            return button;
        }

        static VisualElement Element(string name, params string[] classes)
        {
            var element = new VisualElement { name = name };
            foreach (var item in classes) if (!string.IsNullOrEmpty(item)) element.AddToClassList(item);
            return element;
        }

        static Label Label(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

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
            _lastSafeArea = safe;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
