using System;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using AppPanel = Unity.AppUI.UI.Panel;
using UiButton = UnityEngine.UIElements.Button;
using UiImage = UnityEngine.UIElements.Image;
using UiTextField = UnityEngine.UIElements.TextField;

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
        int _setupStep;
        string _pendingDisplayName = string.Empty;

        public UiRoute CurrentRoute => _runtime != null ? _runtime.Navigator.CurrentRoute : UiRoute.HomeMap;
        public UiRootTab CurrentRoot => _runtime != null ? _runtime.Navigator.CurrentRoot : UiRootTab.Map;

        void Start()
        {
            _runtime = UiPrototypeRuntime.EnsureExists();
            if (_runtime == null || _runtime.Data == null) return;
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
            if (!_runtime.HasProfile) _runtime.Navigator.ResetToSetup();
            else Render();
        }

        void OnDisable()
        {
            if (_runtime != null && _runtime.Navigator != null) _runtime.Navigator.Changed -= OnNavigationChanged;
        }

        void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != _lastSafeArea || screenSize != _lastScreenSize) ApplySafeArea();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HandleBack();
        }

        public void SelectRoot(UiRootTab root) => _runtime.Navigator.SwitchRoot(root);
        public void Navigate(UiRoute route) => _runtime.Navigator.Push(route);
        public bool HandleBack() => _runtime.Navigator.Back();
        public void ShowOverlay(UiOverlay overlay) => _runtime.Navigator.ShowOverlay(overlay);
        public void BeginWalk() => _runtime.StartWalk();
        public WalkResultDto FinishWalk() => _runtime.FinishWalk();
        public FeedResultDto Feed(string foodId, string companionId) => _runtime.PurchaseAndFeed(foodId, companionId);
        public bool CompleteSetup(string displayName) => _runtime.CompleteSetup(displayName);
        public LandmarkRewardDto CollectSelectedLandmarkStamp() => _runtime.CompleteLandmarkMemory(SelectedLandmark().id);
        public void ConfirmResetLocalProgress() => _runtime.ResetLocalProgress();

        void BuildRoot()
        {
            var root = _document.rootVisualElement;
            root.Clear();
            var styleSheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            _panel = new AppPanel { name = "ar-walking-app-panel", theme = "light", scale = "medium" };
            _panel.AddToClassList("app-root");
            root.Add(_panel);
            _safeRoot = new VisualElement { name = "safe-area" };
            _safeRoot.AddToClassList("safe-area");
            _panel.Add(_safeRoot);
        }

        void OnNavigationChanged() { Render(); RenderOverlay(); }

        void Render()
        {
            if (_safeRoot == null) return;
            _safeRoot.Clear();
            switch (_runtime.Navigator.CurrentRoute)
            {
                case UiRoute.OnboardingSetup: BuildOnboarding(); break;
                case UiRoute.HomeMap: BuildMap(); break;
                case UiRoute.ActiveWalk: BuildActiveWalk(); break;
                case UiRoute.WalkResult: BuildWalkResult(); break;
                case UiRoute.CompanionCollection: BuildCompanions(); break;
                case UiRoute.CompanionDetail: BuildCompanionDetail(); break;
                case UiRoute.ShopFood: BuildShop(); break;
                case UiRoute.LandmarkDetail: BuildLandmarkDetail(); break;
                case UiRoute.JourneyList: BuildJourneyList(); break;
                case UiRoute.JourneyDetail: BuildJourneyDetail(); break;
                default: BuildMap(); break;
            }
        }

        void BuildOnboarding()
        {
            var page = Page("onboarding-page", false);
            page.Add(Image(_assets != null ? _assets.arScene : null, "onboarding-art"));
            var sheet = Card("onboarding-sheet");
            if (_runtime.InitialLoadResult.status == SaveLoadStatus.Corrupt)
            {
                sheet.Add(Eyebrow("LOCAL PROFILE RECOVERY"));
                sheet.Add(Body("The previous local profile could not be read. It was preserved as a backup; create a new phone-only profile to continue."));
            }
            if (_setupStep == 0)
            {
                sheet.Add(Title("Meet your walking companion"));
                sheet.Add(Body("Walk around Ho Chi Minh City, grow animal companions, and collect Landmark Stamps. Your profile stays only on this phone."));
                sheet.Add(Action("Continue", () => { _setupStep = 1; Render(); }, "primary-action"));
            }
            else if (_setupStep == 1)
            {
                sheet.Add(Eyebrow("STEP 1 OF 2"));
                sheet.Add(Title("What should we call you?"));
                sheet.Add(Body("Enter a display name from 1 to 20 characters."));
                var field = new UiTextField("Display name") { name = "display-name-field", value = _pendingDisplayName, maxLength = 20 };
                field.AddToClassList("name-field");
                field.RegisterValueChangedCallback(evt => _pendingDisplayName = evt.newValue);
                sheet.Add(field);
                sheet.Add(Action("Choose starter", () =>
                {
                    _pendingDisplayName = PlayerSaveData.NormalizeDisplayName(field.value);
                    if (!PlayerSaveData.IsValidDisplayName(_pendingDisplayName)) { ShowToast("Enter 1 to 20 characters."); return; }
                    _setupStep = 2;
                    Render();
                }, "primary-action"));
            }
            else
            {
                sheet.Add(Eyebrow("STEP 2 OF 2"));
                sheet.Add(Title("Dog is ready to join"));
                sheet.Add(Image(_assets != null ? _assets.Companion(0) : null, "reveal-companion"));
                sheet.Add(Body("Dog starts unlocked with 450 Growth EXP. The image above is temporary plant artwork standing in for the final Dog model."));
                sheet.Add(Action("Confirm Dog", () =>
                {
                    if (!_runtime.CompleteSetup(_pendingDisplayName)) ShowToast("Check your display name and try again.");
                }, "primary-action"));
            }
            page.Add(sheet);
        }

        void BuildMap()
        {
            var page = Page("map-page", true);
            var viewport = new VisualElement { name = "illustrated-map-viewport" };
            viewport.AddToClassList("map-viewport");
            var canvas = new VisualElement { name = "illustrated-map-canvas" };
            canvas.AddToClassList("map-canvas");
            canvas.Add(Image(_assets != null ? _assets.illustratedMap : null, "map-image"));
            viewport.Add(canvas);
            var manipulator = new IllustratedMapManipulator(canvas, _mapData.Map.minimumZoom, _mapData.Map.maximumZoom);
            viewport.AddManipulator(manipulator);
            foreach (var marker in _mapData.Markers)
            {
                var captured = marker;
                var markerIcon = marker.type == MapMarkerType.Player ? _assets.iconLocation : _assets.iconMap;
                var button = IconAction(markerIcon, marker.type == MapMarkerType.Player ? "YOU" : "PIN", () => OpenMarker(captured), "marker-" + marker.id);
                button.AddToClassList("map-marker");
                button.AddToClassList(marker.type == MapMarkerType.Player ? "marker-player" : "marker-landmark");
                button.name = "marker-" + marker.id;
                button.tooltip = marker.label;
                button.style.left = Length.Percent(marker.normalizedPosition.x * 100f);
                button.style.top = Length.Percent(marker.normalizedPosition.y * 100f);
                canvas.Add(button);
            }
            page.Insert(0, viewport);
            var top = new VisualElement { name = "map-top-overlay" };
            top.AddToClassList("map-top-overlay");
            var greeting = Card("compact-card", "glass-card");
            greeting.Add(Eyebrow("LOCAL-ONLY PROFILE"));
            greeting.Add(Title("Hello, " + _runtime.SaveData.displayName));
            greeting.Add(Body("District 1, Ho Chi Minh City"));
            top.Add(greeting);
            top.Add(IconAction(_assets.iconSettings, "SET", () => ShowOverlay(UiOverlay.Settings), "settings-button"));
            page.Add(top);
            var stats = Card("map-stats", "glass-card");
            stats.Add(Metric(_runtime.SaveData.coins.ToString(), "Coins"));
            stats.Add(Metric(_runtime.SaveData.totalDistanceKilometres.ToString("0.0") + " km", "total distance"));
            stats.Add(ActionWithIcon(_assets.iconSteps, "Start a walk", BeginWalk, "primary-action", "compact-action"));
            page.Add(stats);
            var controls = new VisualElement(); controls.AddToClassList("map-controls");
            controls.Add(IconAction(_assets.iconLocation, "GPS", () => ShowToast("Location permission is requested here when the real map provider is connected."), "location-button"));
            controls.Add(IconAction(_assets.iconCompass, "CTR", manipulator.Recenter, "recenter-button"));
            controls.Add(IconAction(_assets.iconHelp, "?", () => ShowToast("Tap a Landmark pin to view its distance and AR availability."), "map-help-button"));
            page.Add(controls);
        }

        void BuildActiveWalk()
        {
            var metrics = _runtime.WalkProvider.GetLiveMetrics();
            var scroll = ScreenWithHeader("Active Walk", "Live data from IWalkMetricsProvider", true);
            var hero = Card("walk-hero");
            hero.Add(Eyebrow("WALKING NOW"));
            hero.Add(Title("Keep your phone lowered"));
            hero.Add(Body("Distance drives companion rewards. Steps appear only when the provider supplies them."));
            scroll.Add(hero);
            var row = Row();
            row.Add(Metric(metrics.distanceKilometres.ToString("0.00") + " km", "distance"));
            row.Add(Metric(TimeLabel(metrics.elapsedSeconds), "time"));
            row.Add(Metric(metrics.hasSteps ? metrics.steps.ToString("N0") : "--", "steps"));
            scroll.Add(row);
            var near = Card("discovery-card");
            near.Add(Eyebrow("AR-READY DEMO"));
            near.Add(Title("Central Post Office"));
            near.Add(Body("The mock map provider reports this Landmark within its demonstration radius."));
            near.Add(Action("View Landmark", () => { _runtime.SelectedLandmarkIndex = 1; Navigate(UiRoute.LandmarkDetail); }, "secondary-action"));
            scroll.Add(near);
            scroll.Add(Action("Finish walk", () => FinishWalk(), "danger-action"));
        }

        void BuildWalkResult()
        {
            var result = _runtime.LastWalkResult ?? new WalkResultDto();
            var scroll = ScreenWithHeader("Walk complete", "Rewards saved locally", true);
            var hero = Card("summary-hero");
            hero.Add(Eyebrow("DISTANCE-BASED PROGRESSION"));
            hero.Add(Title("+" + result.coinsAwarded + " Coins"));
            hero.Add(Body(result.completedKilometres + " completed km granted +" + result.experiencePerEligibleCompanion + " Growth EXP to companions unlocked before this walk."));
            scroll.Add(hero);
            var row = Row();
            row.Add(Metric(result.distanceKilometres.ToString("0.00") + " km", "distance"));
            row.Add(Metric(TimeLabel(result.durationSeconds), "time"));
            row.Add(Metric(result.hasSteps ? result.steps.ToString("N0") : "--", "steps"));
            scroll.Add(row);
            foreach (var id in result.newlyUnlockedCompanionIds) scroll.Add(DiscoveryLine("NEW", CompanionName(id) + " unlocked", "Starts at 0 Growth EXP"));
            scroll.Add(Action("View companions", () => SelectRoot(UiRootTab.Companions), "primary-action"));
        }

        void BuildCompanions()
        {
            var scroll = ScreenWithHeader("Companions", "Dog, Cat, and Rabbit", false);
            var grid = new VisualElement(); grid.AddToClassList("companion-grid");
            for (var i = 0; i < _data.Companions.Count; i++)
            {
                var index = i;
                var definition = _data.Companions[i];
                var progress = _runtime.Companion(definition.id);
                var unlocked = progress != null && progress.unlocked;
                var button = new UiButton(() => { _runtime.SelectedCompanionIndex = index; Navigate(UiRoute.CompanionDetail); });
                button.name = "companion-" + definition.id;
                button.AddToClassList("companion-card");
                if (!unlocked) button.AddToClassList("locked-card");
                button.Add(Image(_assets != null ? _assets.Companion(i) : null, "companion-image"));
                button.Add(Subtitle(unlocked ? definition.name : "Locked companion"));
                button.Add(Body(unlocked ? StageLine(progress) : definition.unlockHint));
                grid.Add(button);
            }
            scroll.Add(grid);
            scroll.Add(Body("Prototype note: the companion pictures are retained plant-art placeholders, not final animal models."));
        }

        void BuildCompanionDetail()
        {
            var index = Mathf.Clamp(_runtime.SelectedCompanionIndex, 0, _data.Companions.Count - 1);
            var definition = _data.Companions[index];
            var progress = _runtime.Companion(definition.id);
            var unlocked = progress != null && progress.unlocked;
            var scroll = ScreenWithHeader(definition.name, unlocked ? StageLine(progress) : definition.unlockHint, true);
            var stage = Card("companion-stage");
            var picture = Image(_assets != null ? _assets.Companion(index) : null, "detail-companion");
            if (unlocked)
            {
                var scale = CompanionProgressionService.PlaceholderScaleFor(CompanionProgressionService.StageFor(progress.growthExperience));
                picture.style.scale = new Scale(new Vector3(scale, scale, 1f));
            }
            stage.Add(picture);
            stage.Add(Eyebrow("TEMPORARY PLANT-ART PLACEHOLDER"));
            stage.Add(Body(unlocked ? definition.description : definition.unlockHint));
            scroll.Add(stage);
            if (unlocked)
            {
                var details = Card("story-card");
                details.Add(Title(progress.growthExperience + " Growth EXP"));
                details.Add(Body("Baby <500 | Young 500-1499 | Adult 1500+"));
                scroll.Add(details);
                scroll.Add(Action("Buy food", () => SelectRoot(UiRootTab.Shop), "primary-action"));
            }
        }

        void BuildShop()
        {
            var scroll = ScreenWithHeader("Shop", _runtime.SaveData.coins + " Coins available", false);
            foreach (var food in _data.Foods)
            {
                var captured = food;
                var card = Card("list-card");
                var copy = Column();
                copy.Add(Eyebrow(food.coinCost + " COINS"));
                copy.Add(Subtitle(food.name));
                copy.Add(Body(food.description + "  +" + food.growthExperience + " Growth EXP"));
                card.Add(copy);
                card.Add(ActionWithIcon(_assets.iconShop, "Buy & feed", () => ShowFoodPicker(captured), "primary-action", "compact-action"));
                scroll.Add(card);
            }
        }

        void BuildLandmarkDetail()
        {
            var landmark = SelectedLandmark();
            var proximity = _runtime.LandmarkMapProvider.GetLandmarkProximity(landmark.id);
            var scroll = ScreenWithHeader(landmark.name, landmark.localName, true);
            scroll.Add(Image(_assets != null ? _assets.Landmark(_runtime.SelectedLandmarkIndex) : null, "landmark-hero"));
            var card = Card("memory-card");
            card.Add(Eyebrow(landmark.imageTargetReady ? "AR-READY DEMO" : "LANDMARK"));
            card.Add(Title("History")); card.Add(Body(landmark.history));
            card.Add(Title("Architecture")); card.Add(Body(landmark.architecture));
            card.Add(Title("Did You Know?")); card.Add(Body(landmark.didYouKnow));
            card.Add(DiscoveryLine("MAP", proximity.distanceMetres.ToString("0") + " m away", proximity.isWithinUnlockRadius ? "Inside AR unlock radius" : "Walk closer to unlock AR"));
            scroll.Add(card);
            if (landmark.imageTargetReady || proximity.isWithinUnlockRadius)
                scroll.Add(ActionWithIcon(_assets.iconAr, "Open simulated Image Target", _runtime.EnterLandmarkAr, "primary-action"));
        }

        void BuildJourneyList()
        {
            var scroll = ScreenWithHeader("Journey", "Landmark memories saved on this phone", false);
            var banner = Card("journey-banner");
            banner.Add(IconImage(_assets.iconCalendar, "banner-icon"));
            banner.Add(Title(_runtime.SaveData.journeys.Count + " Journey records"));
            banner.Add(Body(_runtime.SaveData.stamps.Count + " Stamps | " + _runtime.SaveData.savedPhotoPaths.Count + " saved photo paths"));
            scroll.Add(banner);
            if (_runtime.SaveData.journeys.Count == 0) scroll.Add(Body("No journeys yet. Complete the Central Post Office AR Memory to create one."));
            for (var i = 0; i < _runtime.SaveData.journeys.Count; i++)
            {
                var index = i;
                var journey = _runtime.SaveData.journeys[i];
                var button = new UiButton(() => { _runtime.SelectedJourneyIndex = index; Navigate(UiRoute.JourneyDetail); });
                button.name = "journey-" + journey.id;
                button.AddToClassList("journey-card");
                button.Add(Image(_assets != null ? _assets.journeyOne : null, "journey-image"));
                var copy = Column(); copy.Add(Eyebrow(DateLabel(journey.createdUtc))); copy.Add(Subtitle(journey.title)); copy.Add(Body(journey.summary)); button.Add(copy);
                scroll.Add(button);
            }
        }

        void BuildJourneyDetail()
        {
            if (_runtime.SaveData.journeys.Count == 0) { BuildJourneyList(); return; }
            var index = Mathf.Clamp(_runtime.SelectedJourneyIndex, 0, _runtime.SaveData.journeys.Count - 1);
            var journey = _runtime.SaveData.journeys[index];
            var scroll = ScreenWithHeader(journey.title, DateLabel(journey.createdUtc), true);
            scroll.Add(Image(_assets != null ? _assets.journeyOne : null, "journey-detail-image"));
            var note = Card("scrapbook-card");
            note.Add(Eyebrow("LOCAL JOURNEY RECORD"));
            note.Add(Title(journey.summary));
            note.Add(DiscoveryLine("PIN", "Landmark", LandmarkName(journey.landmarkId)));
            note.Add(DiscoveryLine("KM", "Distance", journey.distanceKilometres.ToString("0.00") + " km"));
            note.Add(DiscoveryLine("PIC", "Photos", _runtime.SaveData.savedPhotoPaths.Count.ToString()));
            scroll.Add(note);
            scroll.Add(Action("Open Landmark", () => { _runtime.SelectedLandmarkIndex = FindLandmarkIndex(journey.landmarkId); Navigate(UiRoute.LandmarkDetail); }, "secondary-action"));
        }

        void OpenMarker(MapMarkerUiData marker)
        {
            if (marker.type == MapMarkerType.Player) { ShowToast("This is the mock player position."); return; }
            _runtime.SelectedLandmarkIndex = FindLandmarkIndex(marker.targetId);
            Navigate(UiRoute.LandmarkDetail);
        }

        void ShowFoodPicker(FoodUiData food)
        {
            RemoveTransientOverlay();
            _overlayScrim = new VisualElement(); _overlayScrim.AddToClassList("tray-scrim");
            var tray = Card("discovery-tray");
            tray.Add(Eyebrow("CHOOSE AN UNLOCKED COMPANION")); tray.Add(Title("Feed " + food.name));
            foreach (var definition in _data.Companions)
            {
                var progress = _runtime.Companion(definition.id);
                if (progress == null || !progress.unlocked) continue;
                var captured = definition;
                tray.Add(Action(captured.name, () =>
                {
                    var result = Feed(food.id, captured.id);
                    RemoveTransientOverlay();
                    ShowToast(result.success
                        ? captured.name + " gained " + result.experienceGained + " EXP" + (result.StageChanged ? " and became " + result.currentStage : string.Empty)
                        : result.error);
                    Render();
                }, "primary-action"));
            }
            tray.Add(Action("Cancel", RemoveTransientOverlay, "ghost-action"));
            _overlayScrim.Add(tray); _panel.popupContainer.Add(_overlayScrim);
        }

        void RenderOverlay()
        {
            RemoveTransientOverlay();
            if (!_runtime.Navigator.CurrentOverlay.HasValue) return;
            var overlay = _runtime.Navigator.CurrentOverlay.Value;
            _overlayScrim = new VisualElement { name = "overlay-scrim" }; _overlayScrim.AddToClassList("overlay-scrim");
            var modal = Card("modal-card");
            if (overlay == UiOverlay.Settings)
            {
                modal.Add(Title("Settings"));
                modal.Add(Body("Profile: " + _runtime.SaveData.displayName + ". All progress is stored locally; there is no account or sync."));
                modal.Add(Action("Permissions", () => ShowOverlay(UiOverlay.Permissions), "secondary-action"));
                modal.Add(Action("Reset Local Progress", () => ShowOverlay(UiOverlay.Confirmation), "danger-action"));
            }
            else if (overlay == UiOverlay.Permissions)
            {
                modal.Add(Title("Contextual permissions"));
                modal.Add(Body("Location is requested from Map and camera is requested when AR opens. Profile creation requests neither."));
            }
            else if (overlay == UiOverlay.Confirmation)
            {
                modal.Add(Title("Reset Local Progress?"));
                modal.Add(Body("This permanently removes the local profile, Coins, companion growth, Stamps, Journeys, and saved photo paths."));
                modal.Add(Action("Confirm reset", ConfirmResetLocalProgress, "danger-action"));
            }
            else
            {
                modal.Add(Title("Something went wrong")); modal.Add(Body("The requested prototype action could not be completed."));
            }
            modal.Add(ActionWithIcon(_assets.iconClose, "Close", _runtime.Navigator.CloseOverlay, "primary-action"));
            _overlayScrim.Add(modal); _panel.popupContainer.Add(_overlayScrim);
        }

        void RemoveTransientOverlay()
        {
            if (_overlayScrim == null) return;
            _overlayScrim.RemoveFromHierarchy();
            _overlayScrim = null;
        }

        ScrollView ScreenWithHeader(string title, string subtitle, bool showBack)
        {
            var page = Page("content-page", true);
            var header = new VisualElement(); header.AddToClassList("screen-header");
            if (showBack) header.Add(IconAction(_assets.iconBack, "BACK", () => HandleBack(), "back-button"));
            var copy = Column(); copy.Add(Title(title)); copy.Add(Body(subtitle)); header.Add(copy);
            header.Add(IconAction(_assets.iconSettings, "SET", () => ShowOverlay(UiOverlay.Settings), "settings-button"));
            page.Insert(0, header);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "screen-scroll" }; scroll.AddToClassList("screen-scroll");
            page.Insert(1, scroll); return scroll;
        }

        VisualElement Page(string name, bool showNavigation)
        {
            var page = new VisualElement { name = name }; page.AddToClassList("page"); page.AddToClassList(name); _safeRoot.Add(page);
            if (showNavigation) _safeRoot.Add(BuildBottomNavigation());
            return page;
        }

        VisualElement BuildBottomNavigation()
        {
            var nav = new VisualElement { name = "bottom-navigation" }; nav.AddToClassList("bottom-nav");
            nav.Add(NavButton(UiRootTab.Map, _assets.iconMap, "MAP", "Map"));
            nav.Add(NavButton(UiRootTab.Companions, _assets.iconCompanions, "PETS", "Companions"));
            nav.Add(NavButton(UiRootTab.Shop, _assets.iconShop, "FOOD", "Shop"));
            nav.Add(NavButton(UiRootTab.Journey, _assets.iconJourney, "LOG", "Journey"));
            return nav;
        }

        UiButton NavButton(UiRootTab root, Texture2D icon, string fallback, string label)
        {
            var button = new UiButton(() => SelectRoot(root)) { name = "nav-" + root.ToString().ToLowerInvariant() };
            button.AddToClassList("nav-button"); if (CurrentRoot == root) button.AddToClassList("selected-nav");
            if (icon != null) button.Add(IconImage(icon, "nav-icon"));
            else button.Add(new Label(fallback) { name = "nav-icon" });
            button.Add(new Label(label) { name = "nav-label" }); return button;
        }

        LandmarkUiData SelectedLandmark() => _data.Landmarks[Mathf.Clamp(_runtime.SelectedLandmarkIndex, 0, _data.Landmarks.Count - 1)];
        int FindLandmarkIndex(string id) { for (var i = 0; i < _data.Landmarks.Count; i++) if (_data.Landmarks[i].id == id) return i; return 0; }
        string LandmarkName(string id) { var index = FindLandmarkIndex(id); return _data.Landmarks.Count > 0 ? _data.Landmarks[index].name : id; }
        string CompanionName(string id) { foreach (var item in _data.Companions) if (item.id == id) return item.name; return id; }
        static string StageLine(CompanionProgressData progress) => CompanionProgressionService.StageFor(progress.growthExperience) + " | " + progress.growthExperience + " EXP";
        static string TimeLabel(float seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"mm\:ss");
        static string DateLabel(string utc) { return DateTime.TryParse(utc, out var value) ? value.ToLocalTime().ToString("d MMM yyyy") : "Saved locally"; }

        void ShowToast(string message)
        {
            var toast = new Label(message); toast.AddToClassList("toast"); _panel.notificationContainer.Add(toast);
            toast.schedule.Execute(toast.RemoveFromHierarchy).StartingIn(2200);
        }

        static VisualElement Row() { var value = new VisualElement(); value.AddToClassList("metric-row"); return value; }
        static VisualElement Column() { var value = new VisualElement(); value.AddToClassList("column"); return value; }
        static VisualElement Card(params string[] classes) { var value = new VisualElement(); value.AddToClassList("card"); foreach (var item in classes) if (!string.IsNullOrEmpty(item)) value.AddToClassList(item); return value; }
        static Label Title(string text) { var value = new Label(text); value.AddToClassList("title"); return value; }
        static Label Subtitle(string text) { var value = new Label(text); value.AddToClassList("subtitle"); return value; }
        static Label Body(string text) { var value = new Label(text); value.AddToClassList("body"); return value; }
        static Label Eyebrow(string text) { var value = new Label(text); value.AddToClassList("eyebrow"); return value; }
        static VisualElement DiscoveryLine(string glyph, string title, string detail) { var row = new VisualElement(); row.AddToClassList("discovery-line"); var badge = new Label(glyph); badge.AddToClassList("discovery-badge"); row.Add(badge); var copy = Column(); copy.Add(Subtitle(title)); copy.Add(Body(detail)); row.Add(copy); return row; }
        static VisualElement Metric(string value, string label) { var metric = new VisualElement(); metric.AddToClassList("metric"); var a = new Label(value); a.AddToClassList("metric-value"); metric.Add(a); var b = new Label(label); b.AddToClassList("metric-label"); metric.Add(b); return metric; }
        static UiImage Image(Texture2D texture, string className) { var image = new UiImage { image = texture, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore }; image.AddToClassList(className); return image; }
        static UiButton Action(string label, Action action, params string[] classes) { var button = new UiButton(action) { text = label }; button.AddToClassList("action-button"); foreach (var item in classes) if (!string.IsNullOrEmpty(item)) button.AddToClassList(item); return button; }
        static UiButton ActionWithIcon(Texture2D icon, string label, Action action, params string[] classes) { if (icon == null) return Action(label, action, classes); var button = new UiButton(action) { name = label.ToLowerInvariant().Replace(' ', '-') }; button.AddToClassList("action-button"); button.AddToClassList("icon-action-button"); foreach (var item in classes) if (!string.IsNullOrEmpty(item)) button.AddToClassList(item); button.Add(IconImage(icon, "action-icon")); var copy = new Label(label); copy.AddToClassList("action-label"); button.Add(copy); return button; }
        static UiButton IconAction(Texture2D icon, string fallback, Action action, string name) { var button = new UiButton(action) { name = name }; button.AddToClassList("icon-button"); if (icon != null) button.Add(IconImage(icon, "icon-image")); else button.text = fallback; return button; }
        static UiImage IconImage(Texture2D texture, string name) { var image = new UiImage { image = texture, name = name, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore, tintColor = Color.black }; return image; }

        void ApplySafeArea()
        {
            if (_safeRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            var safe = UiSafeAreaSimulation.Resolve(Screen.safeArea);
            var panelHeight = _document.rootVisualElement.resolvedStyle.height;
            var scale = float.IsNaN(panelHeight) || panelHeight <= 0f ? 1f : panelHeight / Screen.height;
            _safeRoot.style.paddingLeft = safe.xMin * scale;
            _safeRoot.style.paddingRight = (Screen.width - safe.xMax) * scale;
            _safeRoot.style.paddingTop = (Screen.height - safe.yMax) * scale;
            _safeRoot.style.paddingBottom = safe.yMin * scale;
            _lastSafeArea = safe; _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
