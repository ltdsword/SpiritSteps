using System;
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
    public sealed class HomeUiController : MonoBehaviour
    {
        UIDocument _document;
        UiPrototypeRuntime _runtime;
        AppPanel _panel;
        VisualElement _safeRoot;
        VisualElement _overlayScrim;
        PrototypeUiAssets _assets;
        IUiDataProvider _data;
        IMapDataProvider _mapData;
        Rect _lastSafeArea;
        Vector2Int _lastScreenSize;

        public UiRoute CurrentRoute => _runtime != null ? _runtime.Navigator.CurrentRoute : UiRoute.HomeMap;
        public UiRootTab CurrentRoot => _runtime != null ? _runtime.Navigator.CurrentRoot : UiRootTab.Map;

        void Start()
        {
            _runtime = UiPrototypeRuntime.EnsureExists();
            if (_runtime == null || _runtime.Data == null)
                return;

            _assets = _runtime.Assets;
            _data = _runtime.Data;
            _mapData = _runtime.MapData;
            _document = GetComponent<UIDocument>();
            if (_document.panelSettings == null)
                _document.panelSettings = Resources.Load<PanelSettings>("UI/ARWalkingPanelSettings");

            BuildRoot();
            _document.rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());
            ApplySafeArea();
            _runtime.Navigator.Changed += OnNavigationChanged;
            if (!_runtime.HasCompletedOnboarding)
                _runtime.Navigator.Push(UiRoute.OnboardingPermissions);
            else if (_runtime.Navigator.CurrentRoot == UiRootTab.WalkAr)
                _runtime.Navigator.SwitchRoot(UiRootTab.Map);
            else
                Render();
        }

        void OnDisable()
        {
            if (_runtime != null && _runtime.Navigator != null)
                _runtime.Navigator.Changed -= OnNavigationChanged;
        }

        void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != _lastSafeArea || screenSize != _lastScreenSize)
                ApplySafeArea();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                HandleBack();
        }

        public void SelectRoot(UiRootTab root)
        {
            if (root == UiRootTab.WalkAr)
                _runtime.EnterWalkScene();
            else
                _runtime.Navigator.SwitchRoot(root);
        }

        public void Navigate(UiRoute route) => _runtime.Navigator.Push(route);
        public bool HandleBack() => _runtime.Navigator.Back();
        public void ShowOverlay(UiOverlay overlay) => _runtime.Navigator.ShowOverlay(overlay);

        void BuildRoot()
        {
            var root = _document.rootVisualElement;
            root.Clear();
            var styleSheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            _panel = new AppPanel
            {
                name = "ar-walking-app-panel",
                theme = "light",
                scale = "medium"
            };
            _panel.AddToClassList("app-root");
            root.Add(_panel);

            _safeRoot = new VisualElement { name = "safe-area" };
            _safeRoot.AddToClassList("safe-area");
            _panel.Add(_safeRoot);
        }

        void OnNavigationChanged()
        {
            Render();
            RenderOverlay();
        }

        void Render()
        {
            if (_safeRoot == null)
                return;

            _safeRoot.Clear();
            var route = _runtime.Navigator.CurrentRoute;
            switch (route)
            {
                case UiRoute.OnboardingPermissions: BuildOnboarding(); break;
                case UiRoute.HomeMap: BuildMap(); break;
                case UiRoute.ActiveWalk: BuildActiveWalk(); break;
                case UiRoute.WalkSummary: BuildWalkSummary(); break;
                case UiRoute.SpiritCollection: BuildSpiritCollection(); break;
                case UiRoute.SpiritDetail: BuildSpiritDetail(); break;
                case UiRoute.SeedlingGrowth: BuildGarden(); break;
                case UiRoute.HatchReveal: BuildHatchReveal(); break;
                case UiRoute.LandmarkMemory: BuildLandmarkMemory(); break;
                case UiRoute.JourneyJournal: BuildJournal(); break;
                case UiRoute.JourneyDetail: BuildJourneyDetail(); break;
                case UiRoute.ArCompanion:
                case UiRoute.ArPhoto:
                    return;
                default: BuildMap(); break;
            }
        }

        void BuildOnboarding()
        {
            var page = Page("onboarding-page", false);
            var art = Image(_assets != null ? _assets.arScene : null, "onboarding-art");
            page.Add(art);
            var sheet = Card("onboarding-sheet");
            sheet.Add(Title("Walk gently. Remember deeply."));
            sheet.Add(Body("Explore nearby places with a small Vietnamese spirit companion. Your discoveries become a private journey journal."));
            sheet.Add(PermissionRow("◎", UiStrings.Get("permission.location"), "Used only for nearby discoveries"));
            sheet.Add(PermissionRow("▣", UiStrings.Get("permission.camera"), "Used when you open the AR camera"));
            sheet.Add(PermissionRow("⌁", UiStrings.Get("permission.activity"), "Used to grow seedlings with steps"));
            sheet.Add(Action(UiStrings.Get("action.continue"), () =>
            {
                _runtime.HasCompletedOnboarding = true;
                _runtime.Navigator.SwitchRoot(UiRootTab.Map);
            }, "primary-action"));
            sheet.Add(Body("You can change permissions later in Settings."));
            page.Add(sheet);
        }

        void BuildMap()
        {
            var page = Page("map-page", true);
            var viewport = new VisualElement { name = "illustrated-map-viewport" };
            viewport.AddToClassList("map-viewport");
            var mapCanvas = new VisualElement { name = "illustrated-map-canvas" };
            mapCanvas.AddToClassList("map-canvas");
            var mapImage = Image(_assets != null ? _assets.illustratedMap : null, "map-image");
            mapCanvas.Add(mapImage);
            viewport.Add(mapCanvas);
            var manipulator = new IllustratedMapManipulator(mapCanvas, _mapData.Map.minimumZoom, _mapData.Map.maximumZoom);
            viewport.AddManipulator(manipulator);

            foreach (var marker in _mapData.Markers)
            {
                var markerButton = Action(MarkerGlyph(marker.type), () => OpenMarker(marker), "map-marker");
                markerButton.name = "marker-" + marker.id;
                markerButton.tooltip = marker.label;
                markerButton.style.left = Length.Percent(marker.normalizedPosition.x * 100f);
                markerButton.style.top = Length.Percent(marker.normalizedPosition.y * 100f);
                markerButton.AddToClassList("marker-" + marker.type.ToString().ToLowerInvariant());
                mapCanvas.Add(markerButton);
            }

            page.Insert(0, viewport);
            var top = new VisualElement { name = "map-top-overlay" };
            top.AddToClassList("map-top-overlay");
            var greeting = Card("compact-card", "glass-card");
            greeting.Add(Eyebrow(UiStrings.Get("status.prototype")));
            greeting.Add(Title(UiStrings.Get("screen.map")));
            greeting.Add(Body("Quận 1 · Thành phố Hồ Chí Minh"));
            top.Add(greeting);
            top.Add(IconAction("⚙", () => ShowOverlay(UiOverlay.Settings), "settings-button"));
            page.Add(top);

            var stats = Card("map-stats", "glass-card");
            stats.Add(Metric("4,268", "steps today"));
            stats.Add(Metric("03", "memories"));
            stats.Add(Action(UiStrings.Get("action.startWalk"), () => Navigate(UiRoute.ActiveWalk), "primary-action", "compact-action"));
            page.Add(stats);

            var controls = new VisualElement();
            controls.AddToClassList("map-controls");
            controls.Add(IconAction("⌖", manipulator.Recenter, "recenter-button"));
            controls.Add(IconAction("?", () => ShowOverlay(UiOverlay.SyncStatus), "help-button"));
            page.Add(controls);
        }

        void BuildActiveWalk()
        {
            var scroll = ScreenWithHeader("Active Walk", "A quiet route through Sài Gòn", true);
            var hero = Card("walk-hero");
            hero.Add(Eyebrow("WALKING NOW"));
            hero.Add(Title("Follow the green thread"));
            hero.Add(Body("Keep your phone lowered. We will gently alert you when a memory is nearby."));
            hero.Add(Progress(0.62f));
            scroll.Add(hero);
            var metrics = Row();
            metrics.Add(Metric(_runtime.ActiveWalkSteps.ToString("N0"), "steps"));
            metrics.Add(Metric(_runtime.ActiveWalkMinutes + " min", "time"));
            metrics.Add(Metric("1.3 km", "distance"));
            scroll.Add(metrics);
            var near = Card("discovery-card");
            near.Add(Eyebrow("NEARBY MEMORY"));
            near.Add(Title("Bưu điện Trung tâm Sài Gòn"));
            near.Add(Body("About 180 m away · a cultural memory is waiting"));
            near.Add(Action("View landmark", () => { _runtime.SelectedLandmarkIndex = 1; Navigate(UiRoute.LandmarkMemory); }, "secondary-action"));
            scroll.Add(near);
            scroll.Add(Action(UiStrings.Get("action.finishWalk"), () => Navigate(UiRoute.WalkSummary), "danger-action"));
        }

        void BuildWalkSummary()
        {
            var scroll = ScreenWithHeader("Walk complete", "A small journey, carefully kept", true);
            var hero = Card("summary-hero");
            hero.Add(Eyebrow("THURSDAY · QUẬN 1"));
            hero.Add(Title("You carried three memories home"));
            hero.Add(Body("Your steps helped a seedling grow and revealed a new landmark story."));
            scroll.Add(hero);
            var metrics = Row();
            metrics.Add(Metric("2,146", "steps"));
            metrics.Add(Metric("31 min", "time"));
            metrics.Add(Metric("3", "discoveries"));
            scroll.Add(metrics);
            scroll.Add(DiscoveryLine("✦", "Memory fragment", "Collected near Nhà thờ Đức Bà"));
            scroll.Add(DiscoveryLine("♧", "Seedling progress", "+2,146 steps"));
            scroll.Add(DiscoveryLine("▧", "AR photograph", "Saved privately"));
            scroll.Add(Action(UiStrings.Get("action.saveJourney"), () =>
            {
                ShowToast("Journey saved");
                _runtime.Navigator.SwitchRoot(UiRootTab.Journal);
            }, "primary-action"));
        }

        void BuildGarden()
        {
            var scroll = ScreenWithHeader(UiStrings.Get("screen.garden"), "Your steps help each memory wake", false);
            var banner = Card("garden-banner");
            banner.Add(Title("4,268 steps today"));
            banner.Add(Body("Every step is shared across the seedlings in your garden."));
            banner.Add(Progress(0.71f));
            scroll.Add(banner);
            for (var i = 0; i < _data.Seedlings.Count; i++)
            {
                var index = i;
                var seed = _data.Seedlings[i];
                var card = Card("list-card", seed.ready ? "ready-card" : string.Empty);
                card.Add(Image(_assets != null ? _assets.Seedling(i) : null, "list-thumb"));
                var copy = Column();
                copy.Add(Eyebrow(seed.locationName));
                copy.Add(Subtitle(seed.name));
                copy.Add(Body(seed.ready ? "Ready to welcome" : seed.currentSteps.ToString("N0") + " / " + seed.requiredSteps.ToString("N0") + " steps"));
                copy.Add(Progress(seed.Progress));
                card.Add(copy);
                card.Add(Action(seed.ready ? "Hatch" : "View", () => { _runtime.SelectedSeedlingIndex = index; Navigate(UiRoute.HatchReveal); }, seed.ready ? "primary-action" : "secondary-action", "compact-action"));
                scroll.Add(card);
            }
        }

        void BuildHatchReveal()
        {
            var scroll = ScreenWithHeader("A new spirit", "A memory has taken root", true);
            var stage = Card("reveal-stage");
            stage.Add(Eyebrow("NEW COMPANION"));
            stage.Add(Image(_assets != null ? _assets.Spirit(1) : null, "reveal-spirit"));
            stage.Add(Title("Linh Hồn Sen"));
            stage.Add(Body("Born from patient steps near Hồ Con Rùa. This gentle spirit carries stories of calm water and summer rain."));
            stage.Add(Action(UiStrings.Get("action.hatch"), () =>
            {
                _runtime.SelectedSpiritIndex = 1;
                ShowToast("Linh Hồn Sen joined you");
                _runtime.Navigator.SwitchRoot(UiRootTab.Book);
            }, "primary-action"));
            scroll.Add(stage);
        }

        void BuildSpiritCollection()
        {
            var scroll = ScreenWithHeader(UiStrings.Get("screen.collection"), "Vietnamese stories, walking beside you", false);
            var progress = Card("collection-progress");
            progress.Add(Title("2 of 3 spirits remembered"));
            progress.Add(Progress(0.67f));
            progress.Add(Action("Cultural collectibles", () => ShowOverlay(UiOverlay.Collectibles), "secondary-action", "compact-action"));
            scroll.Add(progress);
            var grid = new VisualElement();
            grid.AddToClassList("spirit-grid");
            for (var i = 0; i < _data.Spirits.Count; i++)
            {
                var index = i;
                var spirit = _data.Spirits[i];
                var card = new UiButton(() => { _runtime.SelectedSpiritIndex = index; Navigate(UiRoute.SpiritDetail); });
                card.name = "spirit-" + spirit.id;
                card.AddToClassList("spirit-card");
                if (!spirit.collected) card.AddToClassList("locked-card");
                card.Add(Image(_assets != null ? _assets.Spirit(i) : null, "spirit-image"));
                card.Add(Subtitle(spirit.collected ? spirit.name : "Undiscovered spirit"));
                card.Add(Body(spirit.collected ? spirit.culturalTitle : "Keep exploring"));
                grid.Add(card);
            }
            scroll.Add(grid);
        }

        void BuildSpiritDetail()
        {
            var spirit = _data.Spirits[Mathf.Clamp(_runtime.SelectedSpiritIndex, 0, _data.Spirits.Count - 1)];
            var scroll = ScreenWithHeader(spirit.name, spirit.culturalTitle, true);
            var stage = Card("spirit-stage");
            stage.Add(Image(_assets != null ? _assets.Spirit(_runtime.SelectedSpiritIndex) : null, "detail-spirit"));
            stage.Add(Eyebrow("3D COMPANION PREVIEW"));
            stage.Add(Body("Drag to imagine the companion turning · production 3D model placeholder"));
            scroll.Add(stage);
            var story = Card("story-card");
            story.Add(Title("The memory it carries"));
            story.Add(Body(spirit.description));
            story.Add(DiscoveryLine("◉", "First met", "Công viên Tao Đàn"));
            story.Add(DiscoveryLine("✦", "Shared journeys", "4 walks"));
            scroll.Add(story);
            scroll.Add(Action("Explore together in AR", _runtime.EnterWalkScene, "primary-action"));
        }

        void BuildLandmarkMemory()
        {
            var index = Mathf.Clamp(_runtime.SelectedLandmarkIndex, 0, _data.Landmarks.Count - 1);
            var landmark = _data.Landmarks[index];
            var scroll = ScreenWithHeader("Landmark Memory", landmark.subtitle, true);
            scroll.Add(Image(_assets != null ? _assets.Landmark(index) : null, "landmark-hero"));
            var story = Card("memory-card");
            story.Add(Eyebrow("DISCOVERED IN QUẬN 1"));
            story.Add(Title(landmark.name));
            story.Add(Body(landmark.memoryText));
            story.Add(DiscoveryLine("⌖", "Place", landmark.subtitle));
            story.Add(DiscoveryLine("◷", "Best remembered", "Slowly, from the shaded pavement"));
            scroll.Add(story);
            scroll.Add(Action("Add to this journey", () => ShowToast("Memory linked to your journey"), "primary-action"));
        }

        void BuildJournal()
        {
            var scroll = ScreenWithHeader(UiStrings.Get("screen.journal"), "A scrapbook made from real walks", false);
            var calendar = Card("journal-banner");
            calendar.Add(Eyebrow("AUGUST 2026"));
            calendar.Add(Title("7 days explored"));
            calendar.Add(Body("8,942 steps · 6 memories · 2 photographs"));
            scroll.Add(calendar);
            for (var i = 0; i < _data.Journeys.Count; i++)
            {
                var index = i;
                var journey = _data.Journeys[i];
                var card = new UiButton(() => { _runtime.SelectedJourneyIndex = index; Navigate(UiRoute.JourneyDetail); });
                card.name = "journey-" + journey.id;
                card.AddToClassList("journey-card");
                card.Add(Image(i == 0 ? _assets.journalOne : _assets.journalTwo, "journey-image"));
                var copy = Column();
                copy.Add(Eyebrow(journey.dateLabel));
                copy.Add(Subtitle(journey.title));
                copy.Add(Body(journey.summary));
                copy.Add(Body(journey.steps.ToString("N0") + " steps · " + journey.memories + " memories"));
                card.Add(copy);
                scroll.Add(card);
            }
        }

        void BuildJourneyDetail()
        {
            var index = Mathf.Clamp(_runtime.SelectedJourneyIndex, 0, _data.Journeys.Count - 1);
            var journey = _data.Journeys[index];
            var scroll = ScreenWithHeader(journey.title, journey.dateLabel, true);
            scroll.Add(Image(index == 0 ? _assets.journalOne : _assets.journalTwo, "journey-detail-image"));
            var note = Card("scrapbook-card");
            note.Add(Eyebrow("FIELD NOTE"));
            note.Add(Title("A warm afternoon in Sài Gòn"));
            note.Add(Body(journey.summary + " We slowed down beneath the trees, listened to the city, and found a story worth carrying home."));
            note.Add(DiscoveryLine("⌁", "Steps", journey.steps.ToString("N0")));
            note.Add(DiscoveryLine("✦", "Memories", journey.memories.ToString()));
            note.Add(DiscoveryLine("▧", "Photographs", "1 saved"));
            scroll.Add(note);
            scroll.Add(Action("Open associated landmark", () => { _runtime.SelectedLandmarkIndex = index % _data.Landmarks.Count; Navigate(UiRoute.LandmarkMemory); }, "secondary-action"));
        }

        ScrollView ScreenWithHeader(string title, string subtitle, bool showBack)
        {
            var page = Page("content-page", true);
            var header = new VisualElement();
            header.AddToClassList("screen-header");
            if (showBack)
                header.Add(IconAction("‹", () => HandleBack(), "back-button"));
            var copy = Column();
            copy.Add(Title(title));
            copy.Add(Body(subtitle));
            header.Add(copy);
            header.Add(IconAction("⚙", () => ShowOverlay(UiOverlay.Settings), "settings-button"));
            page.Insert(0, header);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "screen-scroll" };
            scroll.AddToClassList("screen-scroll");
            page.Insert(1, scroll);
            return scroll;
        }

        VisualElement Page(string name, bool showNavigation)
        {
            var page = new VisualElement { name = name };
            page.AddToClassList("page");
            _safeRoot.Add(page);
            if (showNavigation)
                _safeRoot.Add(BuildBottomNavigation());
            return page;
        }

        VisualElement BuildBottomNavigation()
        {
            var nav = new VisualElement { name = "bottom-navigation" };
            nav.AddToClassList("bottom-nav");
            nav.Add(NavButton(UiRootTab.Map, "⌖", UiStrings.Get("nav.map")));
            nav.Add(NavButton(UiRootTab.Garden, "♧", UiStrings.Get("nav.garden")));
            var walk = NavButton(UiRootTab.WalkAr, "◎", UiStrings.Get("nav.walk"));
            walk.AddToClassList("raised-nav");
            nav.Add(walk);
            nav.Add(NavButton(UiRootTab.Journal, "▤", UiStrings.Get("nav.journal")));
            nav.Add(NavButton(UiRootTab.Book, "▥", UiStrings.Get("nav.book")));
            return nav;
        }

        UiButton NavButton(UiRootTab root, string glyph, string label)
        {
            var button = new UiButton(() => SelectRoot(root)) { name = "nav-" + root.ToString().ToLowerInvariant() };
            button.AddToClassList("nav-button");
            if (CurrentRoot == root) button.AddToClassList("selected-nav");
            button.Add(new Label(glyph) { name = "nav-glyph" });
            button.Add(new Label(label) { name = "nav-label" });
            return button;
        }

        void OpenMarker(MapMarkerUiData marker)
        {
            switch (marker.type)
            {
                case MapMarkerType.Landmark:
                    _runtime.SelectedLandmarkIndex = FindLandmarkIndex(marker.targetId);
                    Navigate(UiRoute.LandmarkMemory);
                    break;
                case MapMarkerType.Seedling:
                    ShowDiscoveryTray("A seedling is nearby", "Walk a little closer to begin growing this memory.", "Open Garden", () => SelectRoot(UiRootTab.Garden));
                    break;
                case MapMarkerType.CulturalCollectible:
                    ShowDiscoveryTray("Cultural collectible", marker.label + " can be added to your Memory Book.", "View collection", () => ShowOverlay(UiOverlay.Collectibles));
                    break;
                case MapMarkerType.ArDiscoveryHint:
                    ShowDiscoveryTray("AR discovery hint", "A companion moment may appear here.", "Open AR", _runtime.EnterWalkScene);
                    break;
                default:
                    ShowToast("You are here with your selected spirit");
                    break;
            }
        }

        int FindLandmarkIndex(string id)
        {
            for (var i = 0; i < _data.Landmarks.Count; i++)
                if (_data.Landmarks[i].id == id) return i;
            return 0;
        }

        void RenderOverlay()
        {
            if (_overlayScrim != null)
            {
                _overlayScrim.RemoveFromHierarchy();
                _overlayScrim = null;
            }

            if (!_runtime.Navigator.CurrentOverlay.HasValue)
                return;

            var overlay = _runtime.Navigator.CurrentOverlay.Value;
            _overlayScrim = new VisualElement { name = "overlay-scrim" };
            _overlayScrim.AddToClassList("overlay-scrim");
            var modal = Card("modal-card");
            modal.Add(Title(OverlayTitle(overlay)));
            modal.Add(Body(OverlayBody(overlay)));
            if (overlay == UiOverlay.Settings)
            {
                modal.Add(DiscoveryLine("⌖", "Permissions", "Location, camera, and activity"));
                modal.Add(DiscoveryLine("☁", "Sync", "Prototype data stays on this device"));
                modal.Add(Action("Review permissions", () => ShowOverlay(UiOverlay.Permissions), "secondary-action"));
            }
            if (overlay == UiOverlay.Collectibles)
            {
                foreach (var item in _data.Collectibles)
                    modal.Add(DiscoveryLine(item.collected ? "✦" : "◇", item.name, item.collected ? item.category : "Undiscovered"));
            }
            modal.Add(Action(UiStrings.Get("action.close"), _runtime.Navigator.CloseOverlay, "primary-action"));
            _overlayScrim.Add(modal);
            _panel.popupContainer.Add(_overlayScrim);
        }

        void ShowDiscoveryTray(string title, string body, string actionLabel, Action action)
        {
            _runtime.Navigator.CloseOverlay();
            if (_overlayScrim != null) _overlayScrim.RemoveFromHierarchy();
            _overlayScrim = new VisualElement();
            _overlayScrim.AddToClassList("tray-scrim");
            var tray = Card("discovery-tray");
            tray.Add(Eyebrow("MAP DISCOVERY"));
            tray.Add(Title(title));
            tray.Add(Body(body));
            tray.Add(Action(actionLabel, () => { _overlayScrim.RemoveFromHierarchy(); _overlayScrim = null; action(); }, "primary-action"));
            tray.Add(Action("Not now", () => { _overlayScrim.RemoveFromHierarchy(); _overlayScrim = null; }, "ghost-action"));
            _overlayScrim.Add(tray);
            _panel.popupContainer.Add(_overlayScrim);
        }

        void ShowToast(string message)
        {
            var toast = new Label(message);
            toast.AddToClassList("toast");
            _panel.notificationContainer.Add(toast);
            toast.schedule.Execute(toast.RemoveFromHierarchy).StartingIn(1800);
        }

        static string OverlayTitle(UiOverlay overlay)
        {
            switch (overlay)
            {
                case UiOverlay.Settings: return "Settings";
                case UiOverlay.Permissions: return "Permissions";
                case UiOverlay.SyncStatus: return "Map guide";
                case UiOverlay.Collectibles: return "Cultural Collectibles";
                case UiOverlay.Confirmation: return "Are you sure?";
                case UiOverlay.Error: return "Something went wrong";
                default: return UiStrings.Get("empty.title");
            }
        }

        static string OverlayBody(UiOverlay overlay)
        {
            switch (overlay)
            {
                case UiOverlay.Permissions: return "Location, camera, and activity permissions are requested only when the related prototype feature is opened.";
                case UiOverlay.SyncStatus: return "Drag and pinch the illustrated map. It is a discovery surface, not turn-by-turn navigation.";
                case UiOverlay.Collectibles: return "Small cultural fragments remembered during your walks.";
                case UiOverlay.Error: return "The prototype could not complete that action. Your saved journey is unchanged.";
                case UiOverlay.Settings: return UiStrings.Get("status.synced");
                default: return UiStrings.Get("empty.body");
            }
        }

        static VisualElement PermissionRow(string glyph, string title, string detail)
        {
            var row = Card("permission-row");
            row.Add(new Label(glyph) { name = "permission-glyph" });
            var copy = Column(); copy.Add(Subtitle(title)); copy.Add(Body(detail)); row.Add(copy);
            row.Add(new Label("✓") { name = "permission-check" });
            return row;
        }

        static VisualElement DiscoveryLine(string glyph, string title, string detail)
        {
            var row = new VisualElement(); row.AddToClassList("discovery-line");
            var badge = new Label(glyph); badge.AddToClassList("discovery-badge"); row.Add(badge);
            var copy = Column(); copy.Add(Subtitle(title)); copy.Add(Body(detail)); row.Add(copy);
            return row;
        }

        static VisualElement Row() { var row = new VisualElement(); row.AddToClassList("metric-row"); return row; }
        static VisualElement Column() { var column = new VisualElement(); column.AddToClassList("column"); return column; }
        static VisualElement Card(params string[] classes) { var card = new VisualElement(); card.AddToClassList("card"); foreach (var c in classes) if (!string.IsNullOrEmpty(c)) card.AddToClassList(c); return card; }
        static Label Title(string text) { var label = new Label(text); label.AddToClassList("title"); return label; }
        static Label Subtitle(string text) { var label = new Label(text); label.AddToClassList("subtitle"); return label; }
        static Label Body(string text) { var label = new Label(text); label.AddToClassList("body"); return label; }
        static Label Eyebrow(string text) { var label = new Label(text); label.AddToClassList("eyebrow"); return label; }

        static VisualElement Metric(string value, string label)
        {
            var metric = new VisualElement(); metric.AddToClassList("metric");
            var valueLabel = new Label(value); valueLabel.AddToClassList("metric-value"); metric.Add(valueLabel);
            var labelElement = new Label(label); labelElement.AddToClassList("metric-label"); metric.Add(labelElement);
            return metric;
        }

        static VisualElement Progress(float value)
        {
            var track = new VisualElement(); track.AddToClassList("progress-track");
            var fill = new VisualElement(); fill.AddToClassList("progress-fill"); fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f); track.Add(fill);
            return track;
        }

        static UiImage Image(Texture2D texture, string className)
        {
            var image = new UiImage { image = texture, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            image.AddToClassList(className);
            return image;
        }

        static UiButton Action(string label, Action action, params string[] classes)
        {
            var button = new UiButton(action) { text = label };
            button.AddToClassList("action-button");
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) button.AddToClassList(c);
            return button;
        }

        static UiButton IconAction(string glyph, Action action, string name)
        {
            var button = new UiButton(action) { text = glyph, name = name };
            button.AddToClassList("icon-button");
            return button;
        }

        static string MarkerGlyph(MapMarkerType type)
        {
            switch (type)
            {
                case MapMarkerType.PlayerSpirit: return "●";
                case MapMarkerType.Landmark: return "⌂";
                case MapMarkerType.Seedling: return "♧";
                case MapMarkerType.CulturalCollectible: return "✦";
                case MapMarkerType.ArDiscoveryHint: return "◎";
                default: return "•";
            }
        }

        void ApplySafeArea()
        {
            if (_safeRoot == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            var safe = UiSafeAreaSimulation.Resolve(Screen.safeArea);
            var panelHeight = _document.rootVisualElement.resolvedStyle.height;
            var scale = float.IsNaN(panelHeight) || panelHeight <= 0f ? 1f : panelHeight / Screen.height;
            _safeRoot.style.paddingLeft = safe.xMin * scale;
            _safeRoot.style.paddingRight = (Screen.width - safe.xMax) * scale;
            _safeRoot.style.paddingTop = (Screen.height - safe.yMax) * scale;
            _safeRoot.style.paddingBottom = safe.yMin * scale;
            _lastSafeArea = safe;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
