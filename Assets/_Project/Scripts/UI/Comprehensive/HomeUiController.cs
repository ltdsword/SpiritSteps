using System;
using System.Collections.Generic;
using System.IO;
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
        static readonly Color Ink = Rgb(42, 63, 49);
        static readonly Color MutedInk = Rgb(105, 120, 109);
        static readonly Color Primary = Rgb(84, 190, 107);
        static readonly Color BlossomInk = Rgb(143, 72, 86);
        static readonly Color SunInk = Rgb(123, 91, 20);
        static readonly Color SkyInk = Rgb(54, 103, 126);
        static readonly Color White = Color.white;

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
        int _featuredCompanionIndex;
        string _pendingDisplayName = string.Empty;
        bool _pickingPetForPhoto;
        readonly Dictionary<string, Texture2D> _journeyPhotoCache = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, VectorImage> _vectorIcons = new Dictionary<string, VectorImage>();

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
            SyncMapViewVisibility();
        }

        void OnDisable()
        {
            if (_runtime != null && _runtime.Navigator != null) _runtime.Navigator.Changed -= OnNavigationChanged;
            if (_runtime != null && _runtime.MapView != null) _runtime.MapView.OnMarkerTapped -= OnRealMapMarkerTapped;
        }

        void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != _lastSafeArea || screenSize != _lastScreenSize) ApplySafeArea();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HandleBack();
            if (_runtime.Navigator.CurrentRoute == UiRoute.HomeMap && _runtime.MapView != null && _runtime.MapView.IsAvailable)
                RenderRealMapMarkers();
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
            _safeRoot = Element("safe-area", "safe-area");
            _panel.Add(_safeRoot);
        }

        void OnNavigationChanged()
        {
            Render();
            RenderOverlay();
            SyncMapViewVisibility();
        }

        void SyncMapViewVisibility()
        {
            if (_runtime.MapView == null) return;
            var onMapWithNoOverlay = _runtime.Navigator.CurrentRoute == UiRoute.HomeMap && _runtime.Navigator.CurrentOverlay == null;
            _runtime.MapView.SetActive(onMapWithNoOverlay);
        }

        void Render()
        {
            if (_safeRoot == null) return;
            _safeRoot.Clear();
            switch (_runtime.Navigator.CurrentRoute)
            {
                case UiRoute.OnboardingSetup: BuildOnboarding(); break;
                case UiRoute.HomeMap:
                case UiRoute.ActiveWalk: BuildMap(); break;
                case UiRoute.WalkResult: BuildWalkResult(); break;
                case UiRoute.CompanionCollection: BuildCompanions(); break;
                case UiRoute.CompanionDetail: BuildCompanionDetail(); break;
                case UiRoute.ShopFood: BuildShop(); break;
                case UiRoute.LandmarkDetail: BuildLandmarkDetail(); break;
                case UiRoute.JourneyList: BuildJourneyList(); break;
                case UiRoute.JourneyDetail: BuildJourneyDetail(); break;
                case UiRoute.ActivityDashboard: BuildActivityDashboard(); break;
                default: BuildMap(); break;
            }
        }

        void BuildOnboarding()
        {
            var page = Page("onboarding-page", false);
            var art = Image(_assets != null ? _assets.arScene : null, "onboarding-art");
            page.Add(art);
            var brand = Element("onboarding-brand", "onboarding-brand");
            brand.Add(IconView("footprints", "brand-icon", Primary, _assets != null ? _assets.iconSteps : null));
            brand.Add(Label("BẠN BƯỚC", "brand-wordmark"));
            page.Add(brand);

            var sheet = Card("onboarding-sheet", "elevated-card");
            sheet.Add(Element("sheet-handle", "sheet-handle"));
            if (_runtime.InitialLoadResult.status == SaveLoadStatus.Corrupt)
            {
                sheet.Add(Eyebrow("LOCAL PROFILE RECOVERY"));
                sheet.Add(Body("Your previous local profile was preserved as a backup. Create a new phone-only profile to continue."));
            }

            if (_setupStep == 0)
            {
                sheet.Add(Title("Every walk holds a memory"));
                sheet.Add(Body("Explore Sài Gòn with a growing animal friend, discover cultural stories, and fill your Journey passport."));
                sheet.Add(ActionWithIcon("play", _assets != null ? _assets.iconSteps : null, "Begin the journey", () => { _setupStep = 1; Render(); }, "primary-action"));
            }
            else if (_setupStep == 1)
            {
                sheet.Add(Eyebrow("STEP 1 OF 2"));
                sheet.Add(Title("What should we call you?"));
                sheet.Add(Body("Your display name stays only on this phone."));
                var field = new UiTextField("Display name") { name = "display-name-field", value = _pendingDisplayName, maxLength = 20 };
                field.AddToClassList("name-field");
                field.RegisterValueChangedCallback(evt => _pendingDisplayName = evt.newValue);
                sheet.Add(field);
                sheet.Add(ActionWithIcon("chevron-right", null, "Choose your companion", () =>
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
                sheet.Add(Title("Meet " + _data.Companions[0].name));
                var reveal = Element("starter-reveal", "starter-reveal");
                reveal.Add(Image(_assets != null ? _assets.Companion(0) : null, "reveal-companion", ScaleMode.ScaleAndCrop));
                sheet.Add(reveal);
                sheet.Add(Body("Your first companion starts with 450 Growth EXP and grows as you walk."));
                sheet.Add(ActionWithIcon("sparkles", null, "Walk with " + _data.Companions[0].name, () =>
                {
                    if (!_runtime.CompleteSetup(_pendingDisplayName)) ShowToast("Check your display name and try again.");
                }, "primary-action"));
            }
            page.Add(sheet);
        }

        void BuildMap()
        {
            var page = Page("map-page", true);
            if (_runtime.MapView != null && _runtime.MapView.IsAvailable) BuildRealMap(page);
            else BuildIllustratedMapFallback(page);
        }

        void BuildRealMap(VisualElement page)
        {
            var top = BuildTopStatusBar(false);
            top.AddToClassList("map-top-bar");
            page.Add(top);

            // BuildWalkControlCard's own "walk-control-card" USS class floats it (position: absolute) for the
            // illustrated-map path; the "map-bottom-bar" modifier (see ARWalking.uss) puts it back in normal
            // document flow so .map-page's justify-content: space-between can push it to the actual bottom
            // edge instead - the reset has to happen in USS, not via inline style overrides here: setting
            // style.left/right to StyleKeyword.Null does not clear the stylesheet's own left/right values, so
            // an inline-only "position: relative" left the card offset by its old absolute-positioning
            // left:30px without shrinking its width, pushing it off the right edge of the screen.
            var bottom = BuildWalkControlCard();
            bottom.AddToClassList("map-bottom-bar");
            page.Add(bottom);

            _runtime.LocationService.Activate();
            _runtime.MapView.OnMarkerTapped -= OnRealMapMarkerTapped; // avoid a duplicate subscription if BuildMap runs again
            _runtime.MapView.OnMarkerTapped += OnRealMapMarkerTapped;

            page.RegisterCallback<GeometryChangedEvent>(_ => ApplyRealMapMargins(top, bottom));
            ApplyRealMapMargins(top, bottom);
            RenderRealMapMarkers();
        }

        void ApplyRealMapMargins(VisualElement topBar, VisualElement bottomBar)
        {
            var panelHeight = _document.rootVisualElement.resolvedStyle.height;
            if (float.IsNaN(panelHeight) || panelHeight <= 0f) return;
            var scale = Screen.height / panelHeight;

            var topEdgeScreenPx = topBar.worldBound.yMax * scale;
            var bottomEdgeScreenPx = bottomBar.worldBound.yMin * scale;
            var (left, top, right, bottom) = WebViewMapMargins.Compute(topEdgeScreenPx, bottomEdgeScreenPx, Screen.width, Screen.height);
            _runtime.MapView.SetMargins(left, top, right, bottom);
        }

        void RenderRealMapMarkers()
        {
            if (!_runtime.LocationService.HasFix) return;
            var markers = new List<WebViewMapMarker>();
            foreach (var marker in _mapData.Markers)
            {
                if (marker.type != MapMarkerType.Landmark) continue;
                var landmark = _runtime.GeoCatalog?.Find(marker.targetId);
                if (landmark == null) continue;
                markers.Add(new WebViewMapMarker(marker.targetId, marker.label, landmark.Location));
            }
            _runtime.MapView.Render(_runtime.LocationService.Current, markers);
        }

        void OnRealMapMarkerTapped(string landmarkId)
        {
            _runtime.SelectedLandmarkIndex = FindLandmarkIndex(landmarkId);
            Navigate(UiRoute.LandmarkDetail);
        }

        void BuildIllustratedMapFallback(VisualElement page)
        {
            var viewport = Element("illustrated-map-viewport", "map-viewport");
            var canvas = Element("illustrated-map-canvas", "map-canvas");
            canvas.Add(Image(_assets != null ? _assets.illustratedMap : null, "map-image"));
            viewport.Add(canvas);
            var manipulator = new IllustratedMapManipulator(canvas, _mapData.Map.minimumZoom, _mapData.Map.maximumZoom);
            viewport.AddManipulator(manipulator);
            page.Add(viewport);

            var landmarkIndex = 0;
            foreach (var markerData in _mapData.Markers)
            {
                var marker = markerData;
                if (marker.type == MapMarkerType.Player)
                {
                    var player = new UiButton(() => OpenMarker(marker)) { name = "marker-" + marker.id, tooltip = marker.label };
                    player.AddToClassList("player-map-marker");
                    player.style.left = Length.Percent(marker.normalizedPosition.x * 100f);
                    player.style.top = Length.Percent(marker.normalizedPosition.y * 100f);
                    var pulse = Element(null, "player-pulse");
                    player.Add(pulse);
                    var avatarWell = Element(null, "player-avatar-well");
                    avatarWell.Add(Image(_assets != null ? _assets.Companion(FindCompanionIndex(_runtime.PrimaryCompanionId())) : null, "player-companion", ScaleMode.ScaleAndCrop));
                    player.Add(avatarWell);
                    canvas.Add(player);
                    continue;
                }

                var proximity = _runtime.LandmarkMapProvider.GetLandmarkProximity(marker.targetId);
                var button = new UiButton(() => OpenMarker(marker)) { name = "marker-" + marker.id, tooltip = marker.label };
                button.AddToClassList("landmark-map-marker");
                button.AddToClassList("marker-accent-" + (landmarkIndex % 3));
                button.style.left = Length.Percent(marker.normalizedPosition.x * 100f);
                button.style.top = Length.Percent(marker.normalizedPosition.y * 100f);
                var pin = Element(null, "marker-pin");
                pin.Add(IconView("map-pin", "marker-icon", landmarkIndex == 1 ? SunInk : landmarkIndex == 2 ? SkyInk : White, _assets != null ? _assets.iconMap : null));
                if (proximity.isWithinUnlockRadius)
                {
                    var near = Element(null, "nearby-badge");
                    near.Add(IconView("navigation", "nearby-icon", BlossomInk, _assets != null ? _assets.iconCompass : null));
                    pin.Add(near);
                }
                button.Add(pin);
                button.Add(Label(proximity.distanceMetres.ToString("0") + " m", "marker-distance"));
                canvas.Add(button);
                landmarkIndex++;
            }

            page.Add(BuildTopStatusBar(true));
            var location = Element("map-location-pill", "location-pill", "floating-surface");
            location.Add(IconView("map-pin", "location-pill-icon", BlossomInk, _assets != null ? _assets.iconLocation : null));
            location.Add(Label(_mapData.Map.regionName, "location-pill-label"));
            page.Add(location);

            var controls = Element("map-controls", "map-controls");
            controls.Add(IconAction("navigation", _assets != null ? _assets.iconCompass : null, "CTR", manipulator.Recenter, "recenter-button", "map-round-control"));
            controls.Add(IconAction("camera", _assets != null ? _assets.iconCamera : null, "CAM", BeginArPhotoPick, "map-photo-button", "map-round-control", "blossom-control"));
            page.Add(controls);
            page.Add(BuildWalkControlCard());
        }

        VisualElement BuildWalkControlCard()
        {
            var walking = _runtime.WalkProvider.IsWalking;
            var weekly = _runtime.GetWeeklyActivity();
            var metrics = walking ? _runtime.WalkProvider.GetLiveMetrics() : new WalkMetrics
            {
                distanceKilometres = weekly.todayDistanceKilometres,
                hasSteps = weekly.todayHasSteps,
                steps = weekly.todaySteps
            };
            var card = Card("walk-control-card", "floating-surface", "elevated-card");
            var top = Row("walk-summary-row");
            var main = Column("walk-main-metric");
            main.Add(Label(walking ? "Walk in progress" : "Ready to explore", "small-label"));
            var distance = Element(null, "walk-distance-line");
            distance.Add(Label(metrics.distanceKilometres.ToString("0.0"), "walk-distance-value"));
            distance.Add(Label("km", "walk-distance-unit"));
            main.Add(distance);
            top.Add(main);
            top.Add(Metric("+" + Mathf.FloorToInt(metrics.distanceKilometres * 20f), "coins", "walk-mini-metric", "sun-value"));
            top.Add(Metric(metrics.hasSteps ? metrics.steps.ToString("N0") : "--", "steps", "walk-mini-metric", "blossom-value"));
            card.Add(top);

            var goal = Row("walk-goal-row");
            goal.Add(IconView("footprints", "walk-goal-icon", Primary, _assets != null ? _assets.iconSteps : null));
            goal.Add(Progress(DailyGoalRatio(metrics.distanceKilometres, weekly.dailyGoalKilometres), "walk-progress"));
            goal.Add(Label(weekly.dailyGoalKilometres.ToString("0") + " km goal", "walk-goal-label"));
            card.Add(goal);
            card.Add(ActionWithIcon(walking ? "square" : "play", _assets != null ? _assets.iconSteps : null,
                walking ? "End Walk" : "Start Walk", walking ? (Action)(() => FinishWalk()) : BeginWalk,
                walking ? "blossom-action" : "primary-action", "walk-toggle-action"));
            return card;
        }

        void BuildWalkResult()
        {
            var result = _runtime.LastWalkResult ?? new WalkResultDto();
            var page = Page("walk-result-page", false);
            var close = IconAction("x", _assets != null ? _assets.iconClose : null, "X", () => SelectRoot(UiRootTab.Map), "walk-result-close", "dark-round-control");
            page.Add(close);
            var celebration = Element("walk-result-content", "walk-result-content");
            celebration.Add(IconView("sparkles", "result-sparkle", Rgb(250, 220, 116)));
            celebration.Add(Eyebrow("WALK COMPLETE"));
            celebration.Add(Title("Lovely walk!"));
            celebration.Add(Body("Your companions gathered memories along the way."));
            var card = Card("walk-result-card", "elevated-card");
            var metrics = Row("result-metrics");
            metrics.Add(Metric(result.distanceKilometres.ToString("0.00"), "kilometres", "result-metric"));
            metrics.Add(Metric(result.hasSteps ? result.steps.ToString("N0") : "--", "steps", "result-metric"));
            metrics.Add(Metric("+" + result.coinsAwarded, "coins", "result-metric"));
            card.Add(metrics);
            card.Add(Divider());
            card.Add(Eyebrow("COMPANION GROWTH"));
            if (result.rewardedCompanionIds.Count == 0)
                card.Add(Body("Complete a full kilometre to grow your companions."));
            foreach (var id in result.rewardedCompanionIds)
            {
                var row = Row("growth-reward-row");
                row.Add(Image(_assets != null ? _assets.Companion(FindCompanionIndex(id)) : null, "growth-reward-pet", ScaleMode.ScaleToFit));
                var copy = Column();
                copy.Add(Subtitle(CompanionName(id)));
                copy.Add(Body("Walk reward"));
                row.Add(copy);
                row.Add(Label("+" + result.experiencePerEligibleCompanion + " EXP", "growth-reward-value"));
                card.Add(row);
            }
            foreach (var id in result.newlyUnlockedCompanionIds)
                card.Add(InfoRow("sparkles", "New friend", CompanionName(id) + " joined your walk", "sun-info"));
            card.Add(ActionWithIcon("sparkles", null, "Collect & continue", () => SelectRoot(UiRootTab.Companions), "primary-action"));
            celebration.Add(card);
            page.Add(celebration);
        }

        void BuildCompanions()
        {
            var pickingForPhoto = _pickingPetForPhoto;
            _pickingPetForPhoto = false;
            var scroll = ScreenWithHeader("Companions", pickingForPhoto ? "Choose a friend for your AR photo" : UnlockedCompanionCount() + " friends walking with you", false,
                "camera", "AR Photo", BeginArPhotoPick, "blossom-chip");

            _featuredCompanionIndex = Mathf.Clamp(_featuredCompanionIndex, 0, _data.Companions.Count - 1);
            if (!IsUnlocked(_featuredCompanionIndex)) _featuredCompanionIndex = FirstUnlockedCompanionIndex();
            scroll.Add(BuildFeaturedCompanion(_featuredCompanionIndex, pickingForPhoto));

            var ownedGrid = Element("owned-companion-grid", "owned-companion-grid");
            for (var i = 0; i < _data.Companions.Count; i++)
            {
                if (!IsUnlocked(i)) continue;
                var index = i;
                var definition = _data.Companions[i];
                var progress = _runtime.Companion(definition.id);
                var button = new UiButton(() =>
                {
                    if (pickingForPhoto) _runtime.EnterPetAr(definition.id, true);
                    else { _featuredCompanionIndex = index; Render(); }
                }) { name = "companion-" + definition.id };
                button.AddToClassList("owned-companion-card");
                if (_featuredCompanionIndex == i) button.AddToClassList("selected-companion-card");
                var well = Element(null, "companion-thumb-well", "accent-surface-" + (i % 4));
                well.Add(Image(_assets != null ? _assets.Companion(i) : null, "companion-thumb", ScaleMode.ScaleAndCrop));
                button.Add(well);
                button.Add(Label(definition.name, "companion-thumb-name"));
                button.Add(Label(CompanionProgressionService.StageFor(progress.growthExperience).ToString(), "companion-thumb-stage"));
                ownedGrid.Add(button);
            }
            scroll.Add(ownedGrid);

            scroll.Add(SectionTitle("Yet to meet"));
            for (var i = 0; i < _data.Companions.Count; i++)
            {
                if (IsUnlocked(i)) continue;
                var definition = _data.Companions[i];
                var row = Card("locked-companion-row");
                var lockWell = Element(null, "locked-icon-well");
                lockWell.Add(IconView("lock", "locked-icon", MutedInk));
                row.Add(lockWell);
                var copy = Column();
                copy.Add(Subtitle(definition.name));
                copy.Add(Body(definition.unlockHint));
                row.Add(copy);
                scroll.Add(row);
            }
        }

        VisualElement BuildFeaturedCompanion(int index, bool pickingForPhoto)
        {
            var definition = _data.Companions[index];
            var progress = _runtime.Companion(definition.id);
            var stage = CompanionProgressionService.StageFor(progress.growthExperience);
            var card = Card("featured-companion", "elevated-card");
            var hero = Row("featured-companion-hero", "accent-surface-" + (index % 4));
            var portrait = Element(null, "featured-portrait-well");
            portrait.Add(Image(_assets != null ? _assets.Companion(index) : null, "featured-companion-image", ScaleMode.ScaleAndCrop));
            hero.Add(portrait);
            var copy = Column("featured-copy");
            var nameRow = Row("featured-name-row");
            nameRow.Add(Title(definition.name));
            nameRow.Add(Pill(stage.ToString(), "stage-pill"));
            copy.Add(nameRow);
            copy.Add(Body(definition.description));
            copy.Add(StageDots(stage));
            hero.Add(copy);
            card.Add(hero);

            var growth = Element(null, "featured-growth");
            var growthHeader = Row("growth-header");
            var growthLabel = Row("growth-label");
            growthLabel.Add(IconView("sparkles", "growth-icon", SunInk));
            growthLabel.Add(Label(stage == GrowthStage.Adult ? "Fully grown" : "Growth", "small-strong-label"));
            growthHeader.Add(growthLabel);
            growthHeader.Add(Label(GrowthCaption(progress.growthExperience, stage), "small-label"));
            growth.Add(growthHeader);
            growth.Add(Progress(GrowthRatio(progress.growthExperience, stage), "growth-progress"));
            var actions = Row("featured-actions");
            actions.Add(ActionWithIcon("sparkles", null, "Feed", () => SelectRoot(UiRootTab.Shop), "secondary-action", "half-action"));
            actions.Add(ActionWithIcon("camera", _assets != null ? _assets.iconCamera : null, pickingForPhoto ? "Choose" : "AR Photo",
                () => _runtime.EnterPetAr(definition.id, true), "blossom-action", "half-action"));
            growth.Add(actions);
            var details = Action("View companion details", () => { _runtime.SelectedCompanionIndex = index; Navigate(UiRoute.CompanionDetail); }, "text-action");
            growth.Add(details);
            card.Add(growth);
            return card;
        }

        void BuildCompanionDetail()
        {
            var index = Mathf.Clamp(_runtime.SelectedCompanionIndex, 0, _data.Companions.Count - 1);
            var definition = _data.Companions[index];
            var progress = _runtime.Companion(definition.id);
            var unlocked = progress != null && progress.unlocked;
            var scroll = ScreenWithHeader(definition.name, unlocked ? StageLine(progress) : definition.unlockHint, true);
            if (!unlocked)
            {
                var locked = Card("companion-detail-locked", "elevated-card");
                locked.Add(IconView("lock", "detail-lock-icon", MutedInk));
                locked.Add(Title("A friend you have yet to meet"));
                locked.Add(Body(definition.unlockHint));
                scroll.Add(locked);
                return;
            }

            scroll.Add(BuildFeaturedCompanion(index, false));
            var story = Card("companion-story-card");
            story.Add(Eyebrow("YOUR COMPANION"));
            story.Add(Subtitle("Grow together, one walk at a time"));
            story.Add(Body("Baby · under 500 EXP\nYoung · 500–1,499 EXP\nAdult · 1,500+ EXP"));
            scroll.Add(story);
            scroll.Add(ActionWithIcon("paw-print", _assets != null ? _assets.iconCompanions : null, "View in AR", () => _runtime.EnterPetAr(definition.id, false), "primary-action"));
        }

        void BuildShop()
        {
            var scroll = ScreenWithHeader("Shop", "Treats for your walking companions", false, "coins", _runtime.SaveData.coins.ToString("N0"), null, "sun-chip");
            for (var i = 0; i < _data.Foods.Count; i++)
            {
                var foodIndex = i;
                var food = _data.Foods[i];
                var card = Card("shop-food-card", "elevated-card");
                var well = Element(null, "food-art-well", "accent-surface-" + (i % 2 == 0 ? 2 : 3));
                well.Add(Image(_assets != null ? _assets.Food(i) : null, "food-art", ScaleMode.ScaleAndCrop));
                card.Add(well);
                var copy = Column("food-copy");
                copy.Add(Subtitle(food.name));
                copy.Add(Body(food.description));
                var reward = Row("food-reward");
                reward.Add(IconView("sparkles", "food-reward-icon", SunInk));
                reward.Add(Label("+" + food.growthExperience + " Growth EXP", "food-reward-label"));
                copy.Add(reward);
                card.Add(copy);
                var buy = new UiButton(() => ShowFoodPicker(_data.Foods[foodIndex])) { name = "buy-" + food.id };
                buy.AddToClassList("price-pill");
                buy.Add(IconView("coins", "price-icon", White));
                buy.Add(Label(food.coinCost.ToString(), "price-label"));
                card.Add(buy);
                scroll.Add(card);
            }
            var note = Card("shop-note-card");
            note.Add(IconView("paw-print", "shop-note-icon", Primary));
            note.Add(Body("Food is applied immediately to the companion you choose. Every purchase is saved locally."));
            scroll.Add(note);
        }

        void BuildLandmarkDetail()
        {
            var landmark = SelectedLandmark();
            var proximity = _runtime.LandmarkMapProvider.GetLandmarkProximity(landmark.id);
            var scroll = ScreenWithHeader("Discover", landmark.localName, true);
            var hero = Element("landmark-hero-card", "landmark-hero-card", "elevated-card");
            hero.Add(Image(_assets != null ? _assets.Landmark(_runtime.SelectedLandmarkIndex) : null, "landmark-hero"));
            var distance = Pill(proximity.distanceMetres.ToString("0") + " m away", "landmark-distance-pill");
            distance.AddToClassList(proximity.isWithinUnlockRadius ? "near-pill" : "far-pill");
            hero.Add(distance);
            scroll.Add(hero);
            scroll.Add(Title(landmark.name));
            scroll.Add(Body("Walk closer, reveal its cultural memory, and add a new stamp to your Journey."));
            scroll.Add(StorySection("History", landmark.history, "history-card", "book-heart"));
            scroll.Add(StorySection("Architecture", landmark.architecture, "architecture-card", "map"));
            scroll.Add(StorySection("Did you know?", landmark.didYouKnow, "fact-card", "sparkles"));
            var collected = IsStampCollected(landmark.id);
            scroll.Add(InfoRow("stamp", collected ? "Stamp collected" : "Passport stamp", collected ? "Saved in your Journey" : "Complete the AR Memory to collect it", collected ? "primary-info" : "blossom-info"));
            if (landmark.imageTargetReady || proximity.isWithinUnlockRadius)
                scroll.Add(ActionWithIcon("sparkles", _assets != null ? _assets.iconAr : null, "Open AR Memory",
                    () => _runtime.EnterPetAr(_runtime.PrimaryCompanionId(), false, PendingPetInteraction.None, landmark.id), "primary-action"));
            else
                scroll.Add(ActionWithIcon("lock", null, "Walk closer to unlock", () => ShowToast("This Landmark is outside the AR unlock radius."), "disabled-action"));
        }

        void BuildJourneyList()
        {
            var scroll = ScreenWithHeader("Journey", "Your memories across Sài Gòn", false);
            var stats = Row("journey-stats");
            stats.Add(Metric(_runtime.SaveData.journeys.Count.ToString(), "memories", "journey-stat-card"));
            stats.Add(Metric(_runtime.SaveData.stamps.Count.ToString(), "stamps", "journey-stat-card"));
            stats.Add(Metric(_runtime.SaveData.savedPhotoPaths.Count.ToString(), "photos", "journey-stat-card"));
            scroll.Add(stats);

            scroll.Add(SectionTitle("Stamp passport"));
            var passport = Card("passport-card", "elevated-card");
            for (var i = 0; i < _data.Landmarks.Count; i++)
            {
                var landmark = _data.Landmarks[i];
                var stamp = Element(null, "passport-stamp");
                if (IsStampCollected(landmark.id)) stamp.AddToClassList("passport-stamp-collected");
                stamp.Add(IconView(IsStampCollected(landmark.id) ? "stamp" : "lock", "passport-stamp-icon", IsStampCollected(landmark.id) ? Primary : MutedInk));
                stamp.Add(Label(landmark.name, "passport-stamp-label"));
                passport.Add(stamp);
            }
            scroll.Add(passport);
            scroll.Add(SectionTitle("Memory timeline"));
            if (_runtime.SaveData.journeys.Count == 0)
            {
                var empty = Card("journey-empty-card");
                empty.Add(IconView("book-heart", "journey-empty-icon", Primary, _assets != null ? _assets.iconJourney : null));
                empty.Add(Subtitle("Your first page is waiting"));
                empty.Add(Body("Complete a Landmark AR Memory or take an AR photo with a companion."));
                scroll.Add(empty);
            }
            for (var i = _runtime.SaveData.journeys.Count - 1; i >= 0; i--)
            {
                var index = i;
                var journey = _runtime.SaveData.journeys[i];
                var button = new UiButton(() => { _runtime.SelectedJourneyIndex = index; Navigate(UiRoute.JourneyDetail); }) { name = "journey-" + journey.id };
                button.AddToClassList("journey-memory-card");
                button.Add(Image(JourneyImage(journey), "journey-memory-image"));
                var overlay = Element(null, "journey-memory-overlay");
                overlay.Add(Pill(DateLabel(journey.createdUtc), "journey-date-pill"));
                overlay.Add(Subtitle(journey.title));
                overlay.Add(Body(journey.summary));
                button.Add(overlay);
                scroll.Add(button);
            }
        }

        void BuildJourneyDetail()
        {
            if (_runtime.SaveData.journeys.Count == 0) { BuildJourneyList(); return; }
            var index = Mathf.Clamp(_runtime.SelectedJourneyIndex, 0, _runtime.SaveData.journeys.Count - 1);
            var journey = _runtime.SaveData.journeys[index];
            var scroll = ScreenWithHeader(journey.title, DateLabel(journey.createdUtc), true);
            var photo = Card("journey-photo-frame", "elevated-card");
            photo.Add(Image(JourneyImage(journey), "journey-detail-image"));
            photo.Add(Label("Quận 1, Sài Gòn", "journey-photo-caption"));
            scroll.Add(photo);
            var note = Card("scrapbook-card");
            note.Add(Eyebrow("LOCAL JOURNEY RECORD"));
            note.Add(Title(journey.summary));
            if (!string.IsNullOrEmpty(journey.landmarkId))
            {
                note.Add(InfoRow("map-pin", "Landmark", LandmarkName(journey.landmarkId)));
                note.Add(InfoRow("footprints", "Distance", journey.distanceKilometres.ToString("0.00") + " km"));
            }
            else if (!string.IsNullOrEmpty(journey.companionId))
                note.Add(InfoRow("paw-print", "Companion", CompanionName(journey.companionId)));
            scroll.Add(note);
            if (!string.IsNullOrEmpty(journey.landmarkId))
                scroll.Add(ActionWithIcon("map-pin", _assets != null ? _assets.iconMap : null, "Open Landmark", () => { _runtime.SelectedLandmarkIndex = FindLandmarkIndex(journey.landmarkId); Navigate(UiRoute.LandmarkDetail); }, "secondary-action"));
            else if (!string.IsNullOrEmpty(journey.companionId))
                scroll.Add(ActionWithIcon("camera", _assets != null ? _assets.iconAr : null, "View in AR", () => _runtime.EnterPetAr(journey.companionId, false), "blossom-action"));
        }

        void BuildActivityDashboard()
        {
            var weekly = _runtime.GetWeeklyActivity();
            var scroll = ScreenWithHeader("Activity Records", DateTime.Now.ToString("ddd, MMM d"), true);
            var record = Card("activity-record-card", "elevated-card");
            record.Add(Title("Walking Record"));
            var ring = new ActivityRing(DailyGoalRatio(weekly.todayDistanceKilometres, weekly.dailyGoalKilometres));
            ring.name = "daily-activity-ring";
            ring.Add(Label(weekly.todayHasSteps ? weekly.todaySteps.ToString("N0") : weekly.todayDistanceKilometres.ToString("0.0"), "activity-ring-value"));
            ring.Add(Label(weekly.todayHasSteps ? "STEPS" : "KILOMETRES", "activity-ring-label"));
            ring.Add(Pill(weekly.todayDistanceKilometres.ToString("0.0") + " / " + weekly.dailyGoalKilometres.ToString("0") + " km", "activity-goal-pill"));
            record.Add(ring);

            var chart = Element("weekly-activity-chart", "weekly-chart");
            foreach (var day in weekly.days)
            {
                var column = Element(null, "weekly-chart-column");
                var barTrack = Element(null, "weekly-chart-bar-track");
                var barFill = Element(null, "weekly-chart-bar-fill");
                if (day.isFuture) barFill.AddToClassList("weekly-chart-bar-fill-future");
                else if (day.isToday) barFill.AddToClassList("weekly-chart-bar-fill-today");
                var ratio = DailyGoalRatio(day.distanceKilometres, weekly.dailyGoalKilometres);
                barFill.style.height = Length.Percent(day.isFuture ? 0f : Mathf.Max(ratio * 100f, day.distanceKilometres > 0 ? 5f : 1.5f));
                barTrack.Add(barFill);
                column.Add(barTrack);
                var dayLabel = Label(day.date.ToString("ddd"), "weekly-chart-day-label");
                var dateLabel = Label(day.date.Day.ToString(), "weekly-chart-date-label");
                if (day.isToday) { dayLabel.AddToClassList("weekly-chart-today-label"); dateLabel.AddToClassList("weekly-chart-today-label"); }
                column.Add(dayLabel);
                column.Add(dateLabel);
                chart.Add(column);
            }
            record.Add(chart);
            var average = Pill("Weekly average  " + weekly.weeklyAverageKilometres.ToString("0.0") + " km", "average-pill");
            record.Add(average);
            scroll.Add(record);

            var summary = Row("activity-summary-row");
            summary.Add(Metric(weekly.todayDistanceKilometres.ToString("0.0") + " km", "today", "activity-summary-card"));
            summary.Add(Metric(weekly.todayHasSteps ? weekly.todaySteps.ToString("N0") : "--", "steps", "activity-summary-card"));
            summary.Add(Metric(Mathf.RoundToInt(DailyGoalRatio(weekly.todayDistanceKilometres, weekly.dailyGoalKilometres) * 100f) + "%", "goal", "activity-summary-card"));
            scroll.Add(summary);
            var friends = Card("activity-friends-card");
            friends.Add(IconView("paw-print", "activity-friends-icon", Primary));
            var friendCopy = Column();
            friendCopy.Add(Subtitle("" + UnlockedCompanionCount() + " companions discovered"));
            friendCopy.Add(Body("Every completed kilometre helps your unlocked friends grow."));
            friends.Add(friendCopy);
            scroll.Add(friends);
        }

        void BeginArPhotoPick()
        {
            _pickingPetForPhoto = true;
            SelectRoot(UiRootTab.Companions);
        }

        void OpenMarker(MapMarkerUiData marker)
        {
            if (marker.type == MapMarkerType.Player) { ShowToast(CompanionName(_runtime.PrimaryCompanionId()) + " is walking with you."); return; }
            _runtime.SelectedLandmarkIndex = FindLandmarkIndex(marker.targetId);
            Navigate(UiRoute.LandmarkDetail);
        }

        void ShowFoodPicker(FoodUiData food)
        {
            RemoveTransientOverlay();
            _overlayScrim = Element("food-picker-scrim", "tray-scrim");
            var tray = Card("food-picker-tray", "discovery-tray", "elevated-card");
            tray.Add(Element("sheet-handle", "sheet-handle"));
            tray.Add(Eyebrow("CHOOSE A COMPANION"));
            tray.Add(Title("Who gets the " + food.name + "?"));
            var choices = Element(null, "food-companion-grid");
            for (var i = 0; i < _data.Companions.Count; i++)
            {
                var definition = _data.Companions[i];
                var progress = _runtime.Companion(definition.id);
                if (progress == null || !progress.unlocked) continue;
                var captured = definition;
                var choice = new UiButton(() =>
                {
                    var result = Feed(food.id, captured.id);
                    RemoveTransientOverlay();
                    if (result.success) _runtime.EnterPetAr(captured.id, false, PendingPetInteraction.Feed);
                    else ShowToast(result.error);
                    Render();
                }) { name = "feed-" + captured.id };
                choice.AddToClassList("food-companion-choice");
                var choiceWell = Element(null, "food-choice-well", "accent-surface-" + (i % 4));
                choiceWell.Add(Image(_assets != null ? _assets.Companion(FindCompanionIndex(captured.id)) : null, "food-choice-image", ScaleMode.ScaleAndCrop));
                choice.Add(choiceWell);
                choice.Add(Label(captured.name, "food-choice-label"));
                choices.Add(choice);
            }
            tray.Add(choices);
            tray.Add(Action("Cancel", RemoveTransientOverlay, "secondary-action"));
            _overlayScrim.Add(tray);
            _panel.popupContainer.Add(_overlayScrim);
        }

        void RenderOverlay()
        {
            RemoveTransientOverlay();
            if (!_runtime.Navigator.CurrentOverlay.HasValue) return;
            var overlay = _runtime.Navigator.CurrentOverlay.Value;
            _overlayScrim = Element("overlay-scrim", "overlay-scrim");
            var modal = Card("modal-card", "elevated-card");
            var close = IconAction("x", _assets != null ? _assets.iconClose : null, "X", _runtime.Navigator.CloseOverlay, "modal-close", "small-round-control");
            modal.Add(close);
            if (overlay == UiOverlay.Settings)
            {
                modal.Add(Eyebrow("BẠN BƯỚC"));
                modal.Add(Title("Hello, " + _runtime.SaveData.displayName));
                modal.Add(Body("Your walks, companions, stamps, and photos live only on this phone."));
                modal.Add(InfoRow("map-pin", "Location", "Requested only when Map needs it", "primary-info"));
                modal.Add(InfoRow("camera", "Camera", "Requested only when AR opens", "blossom-info"));
                modal.Add(ActionWithIcon("settings", _assets != null ? _assets.iconSettings : null, "Permissions", () => ShowOverlay(UiOverlay.Permissions), "secondary-action"));
                modal.Add(Action("Reset local progress", () => ShowOverlay(UiOverlay.Confirmation), "danger-action"));
            }
            else if (overlay == UiOverlay.Permissions)
            {
                modal.Add(Eyebrow("CONTEXTUAL PERMISSIONS"));
                modal.Add(Title("Your privacy comes first"));
                modal.Add(Body("Location is requested from Map. Camera is requested only when AR opens. Creating a profile requests neither."));
            }
            else if (overlay == UiOverlay.Confirmation)
            {
                modal.Add(Eyebrow("LOCAL DATA"));
                modal.Add(Title("Reset your Journey?"));
                modal.Add(Body("This permanently removes the profile, Coins, companion growth, Stamps, Journeys, and saved photo paths."));
                modal.Add(Action("Yes, reset everything", ConfirmResetLocalProgress, "danger-action"));
            }
            else
            {
                modal.Add(Title("Something went wrong"));
                modal.Add(Body("The requested action could not be completed."));
            }
            modal.Add(Action("Close", _runtime.Navigator.CloseOverlay, "primary-action"));
            _overlayScrim.Add(modal);
            _panel.popupContainer.Add(_overlayScrim);
        }

        void RemoveTransientOverlay()
        {
            if (_overlayScrim == null) return;
            _overlayScrim.RemoveFromHierarchy();
            _overlayScrim = null;
        }

        ScrollView ScreenWithHeader(string title, string subtitle, bool showBack,
            string actionIcon = null, string actionLabel = null, Action action = null, string actionClass = null)
        {
            var page = Page("content-page", true);
            page.Add(BuildTopStatusBar(false));
            var header = Row("screen-header");
            if (showBack) header.Add(IconAction("arrow-left", _assets != null ? _assets.iconBack : null, "BACK", () => HandleBack(), "back-button", "small-round-control"));
            var copy = Column("screen-header-copy");
            copy.Add(Title(title));
            copy.Add(Body(subtitle));
            header.Add(copy);
            if (!string.IsNullOrEmpty(actionLabel))
            {
                var chip = ActionWithIcon(actionIcon, actionIcon == "coins" ? null : _assets != null ? _assets.iconCamera : null,
                    actionLabel, action ?? (() => { }), actionClass ?? "secondary-action", "header-chip");
                header.Add(chip);
            }
            page.Add(header);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "screen-scroll" };
            scroll.AddToClassList("screen-scroll");
            page.Add(scroll);
            return scroll;
        }

        VisualElement BuildTopStatusBar(bool mapMode)
        {
            var bar = Element("top-status-bar", "top-status-bar");
            if (mapMode) bar.AddToClassList("map-top-status-bar");
            var metrics = Row("status-pill-group");
            metrics.Add(StatusPill("coins", _assets != null ? _assets.iconShop : null, _runtime.SaveData.coins.ToString("N0"), "coin-status-pill", null, null));
            metrics.Add(StatusPill("footprints", _assets != null ? _assets.iconSteps : null, _runtime.SaveData.totalDistanceKilometres.ToString("0.0") + " km", "distance-status-pill", () => Navigate(UiRoute.ActivityDashboard), "activity-dashboard-button"));
            bar.Add(metrics);
            var profile = new UiButton(() => ShowOverlay(UiOverlay.Settings)) { name = "settings-button" };
            profile.AddToClassList("profile-button");
            var initial = string.IsNullOrWhiteSpace(_runtime.SaveData.displayName) ? "B" : _runtime.SaveData.displayName.Substring(0, 1).ToUpperInvariant();
            profile.Add(Label(initial, "profile-initial"));
            bar.Add(profile);
            return bar;
        }

        UiButton StatusPill(string iconName, Texture2D fallback, string value, string className, Action action, string name)
        {
            var pill = new UiButton(action ?? (() => { })) { name = name };
            pill.AddToClassList("status-pill");
            pill.AddToClassList(className);
            var iconWell = Element(null, "status-icon-well");
            iconWell.Add(IconView(iconName, "status-icon", iconName == "coins" ? SunInk : White, fallback));
            pill.Add(iconWell);
            pill.Add(Label(value, "status-value"));
            return pill;
        }

        VisualElement Page(string name, bool showNavigation)
        {
            var page = Element(name, "page", name);
            _safeRoot.Add(page);
            if (showNavigation) _safeRoot.Add(BuildBottomNavigation());
            return page;
        }

        VisualElement BuildBottomNavigation()
        {
            var nav = Element("bottom-navigation", "bottom-nav", "elevated-card");
            nav.Add(NavButton(UiRootTab.Map, "map", _assets != null ? _assets.iconMap : null, "Map"));
            nav.Add(NavButton(UiRootTab.Companions, "paw-print", _assets != null ? _assets.iconCompanions : null, "Companions"));
            nav.Add(NavButton(UiRootTab.Journey, "book-heart", _assets != null ? _assets.iconJourney : null, "Journey"));
            nav.Add(NavButton(UiRootTab.Shop, "store", _assets != null ? _assets.iconShop : null, "Shop"));
            return nav;
        }

        UnityEngine.UIElements.Button NavButton(UiRootTab root, string iconName, Texture2D fallback, string label)
        {
            var selected = CurrentRoot == root;
            var button = new UnityEngine.UIElements.Button(() => SelectRoot(root)) { name = "nav-" + root.ToString().ToLowerInvariant() };
            button.AddToClassList("nav-button");
            if (selected) button.AddToClassList("selected-nav");
            button.Add(IconView(iconName, "nav-icon", selected ? Primary : Rgb(141, 151, 143), fallback));
            var navLabel = Label(label, "nav-label");
            navLabel.AddToClassList("small-label");
            button.Add(navLabel);
            return button;
        }

        Texture2D JourneyImage(JourneyEntryData journey)
        {
            if (string.IsNullOrEmpty(journey.photoPath)) return _assets != null ? _assets.journeyOne : null;
            if (_journeyPhotoCache.TryGetValue(journey.photoPath, out var cached) && cached != null) return cached;
            if (!File.Exists(journey.photoPath)) return _assets != null ? _assets.journeyOne : null;
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(File.ReadAllBytes(journey.photoPath))) return _assets != null ? _assets.journeyOne : null;
            _journeyPhotoCache[journey.photoPath] = texture;
            return texture;
        }

        bool IsStampCollected(string landmarkId)
        {
            foreach (var stamp in _runtime.SaveData.stamps)
                if (stamp != null && stamp.landmarkId == landmarkId) return true;
            return false;
        }

        int UnlockedCompanionCount()
        {
            var count = 0;
            for (var i = 0; i < _data.Companions.Count; i++) if (IsUnlocked(i)) count++;
            return count;
        }

        bool IsUnlocked(int index)
        {
            var progress = _runtime.Companion(_data.Companions[index].id);
            return progress != null && progress.unlocked;
        }

        int FirstUnlockedCompanionIndex()
        {
            for (var i = 0; i < _data.Companions.Count; i++) if (IsUnlocked(i)) return i;
            return 0;
        }

        int FindCompanionIndex(string id)
        {
            for (var i = 0; i < _data.Companions.Count; i++) if (_data.Companions[i].id == id) return i;
            return 0;
        }

        LandmarkUiData SelectedLandmark() => _data.Landmarks[Mathf.Clamp(_runtime.SelectedLandmarkIndex, 0, _data.Landmarks.Count - 1)];
        int FindLandmarkIndex(string id) { for (var i = 0; i < _data.Landmarks.Count; i++) if (_data.Landmarks[i].id == id) return i; return 0; }
        string LandmarkName(string id) { var index = FindLandmarkIndex(id); return _data.Landmarks.Count > 0 ? _data.Landmarks[index].name : id; }
        string CompanionName(string id) { foreach (var item in _data.Companions) if (item.id == id) return item.name; return id; }
        static string StageLine(CompanionProgressData progress) => CompanionProgressionService.StageFor(progress.growthExperience) + " · " + progress.growthExperience + " EXP";
        static string DateLabel(string utc) => DateTime.TryParse(utc, out var value) ? value.ToLocalTime().ToString("d MMM yyyy") : "Saved locally";
        static float DailyGoalRatio(float distanceKilometres, float goalKilometres) => goalKilometres > 0f ? Mathf.Clamp01(distanceKilometres / goalKilometres) : 0f;
        static float GrowthRatio(int experience, GrowthStage stage) => stage == GrowthStage.Baby ? Mathf.Clamp01(experience / 500f) : stage == GrowthStage.Young ? Mathf.Clamp01((experience - 500f) / 1000f) : 1f;
        static string GrowthCaption(int experience, GrowthStage stage) => stage == GrowthStage.Baby ? experience + " / 500 EXP" : stage == GrowthStage.Young ? experience + " / 1,500 EXP" : "Max";

        void ShowToast(string message)
        {
            var toast = Label(message, "toast");
            _panel.notificationContainer.Add(toast);
            toast.schedule.Execute(toast.RemoveFromHierarchy).StartingIn(2200);
        }

        VisualElement StageDots(GrowthStage current)
        {
            var stages = Element(null, "stage-dots");
            for (var i = 0; i < 3; i++)
            {
                var dot = Element(null, "stage-dot");
                if (i <= (int)current) dot.AddToClassList("stage-dot-active");
                stages.Add(dot);
                if (i < 2) stages.Add(IconView("chevron-right", "stage-chevron", MutedInk));
            }
            return stages;
        }

        VisualElement StorySection(string title, string body, string className, string iconName)
        {
            var card = Card("story-section", className);
            var heading = Row("story-heading");
            heading.Add(IconView(iconName, "story-icon", Ink));
            heading.Add(Subtitle(title));
            card.Add(heading);
            card.Add(Body(body));
            return card;
        }

        VisualElement InfoRow(string iconName, string title, string detail, string className = null)
        {
            var row = Element(null, "info-row");
            if (!string.IsNullOrEmpty(className)) row.AddToClassList(className);
            var well = Element(null, "info-icon-well");
            well.Add(IconView(iconName, "info-icon", Ink));
            row.Add(well);
            var copy = Column();
            copy.Add(Subtitle(title));
            copy.Add(Body(detail));
            row.Add(copy);
            return row;
        }

        static VisualElement Divider() => Element(null, "divider");
        static Label SectionTitle(string text) => Label(text, "section-title");
        static VisualElement Column(params string[] classes) => Element(null, Join("column", classes));
        static VisualElement Row(params string[] classes) => Element(null, Join("row", classes));
        static VisualElement Card(params string[] classes) => Element(null, Join("card", classes));
        static Label Title(string text) => Label(text, "title");
        static Label Subtitle(string text) => Label(text, "subtitle");
        static Label Body(string text) => Label(text, "body");
        static Label Eyebrow(string text) => Label(text, "eyebrow");

        static VisualElement Element(string name, params string[] classes)
        {
            var value = new VisualElement { name = name };
            foreach (var item in classes) if (!string.IsNullOrEmpty(item)) value.AddToClassList(item);
            return value;
        }

        static string[] Join(string first, string[] rest)
        {
            var values = new string[(rest?.Length ?? 0) + 1];
            values[0] = first;
            if (rest != null) Array.Copy(rest, 0, values, 1, rest.Length);
            return values;
        }

        static Label Label(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        static VisualElement Pill(string text, params string[] classes)
        {
            var pill = Element(null, Join("pill", classes));
            pill.Add(Label(text, "pill-label"));
            return pill;
        }

        static VisualElement Metric(string value, string label, params string[] classes)
        {
            var metric = Element(null, Join("metric", classes));
            metric.Add(Label(value, "metric-value"));
            metric.Add(Label(label, "metric-label"));
            return metric;
        }

        static VisualElement Progress(float ratio, params string[] classes)
        {
            var track = Element(null, Join("progress-track", classes));
            var fill = Element(null, "progress-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
            track.Add(fill);
            return track;
        }

        static UiImage Image(Texture2D texture, string className, ScaleMode scaleMode = ScaleMode.ScaleAndCrop)
        {
            var image = new UiImage { image = texture, scaleMode = scaleMode, pickingMode = PickingMode.Ignore };
            image.AddToClassList(className);
            return image;
        }

        UiImage IconView(string iconName, string name, Color tint, Texture2D fallback = null)
        {
            if (!_vectorIcons.TryGetValue(iconName, out var vector))
            {
                vector = Resources.Load<VectorImage>("UI/Icons/" + iconName);
                _vectorIcons[iconName] = vector;
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

        static UiButton Action(string label, Action action, params string[] classes)
        {
            var button = new UiButton(action) { text = label };
            button.AddToClassList("action-button");
            foreach (var item in classes) if (!string.IsNullOrEmpty(item)) button.AddToClassList(item);
            return button;
        }

        UiButton ActionWithIcon(string iconName, Texture2D fallback, string label, Action action, params string[] classes)
        {
            var button = new UiButton(action) { name = label.ToLowerInvariant().Replace(' ', '-') };
            button.AddToClassList("action-button");
            button.AddToClassList("icon-action-button");
            foreach (var item in classes) if (!string.IsNullOrEmpty(item)) button.AddToClassList(item);
            var whiteIcon = Array.IndexOf(classes, "primary-action") >= 0 || Array.IndexOf(classes, "blossom-action") < 0 && Array.IndexOf(classes, "disabled-action") < 0;
            var tint = Array.IndexOf(classes, "primary-action") >= 0 ? White : Array.IndexOf(classes, "blossom-action") >= 0 ? BlossomInk : Ink;
            if (whiteIcon && Array.IndexOf(classes, "primary-action") >= 0) tint = White;
            button.Add(IconView(iconName, "action-icon", tint, fallback));
            button.Add(Label(label, "action-label"));
            return button;
        }

        UiButton IconAction(string iconName, Texture2D fallback, string fallbackText, Action action, string name, params string[] classes)
        {
            var button = new UiButton(action) { name = name };
            button.AddToClassList("icon-button");
            foreach (var item in classes) if (!string.IsNullOrEmpty(item)) button.AddToClassList(item);
            var icon = IconView(iconName, "icon-image", Array.IndexOf(classes, "dark-round-control") >= 0 ? White : Ink, fallback);
            if (icon.vectorImage != null || icon.image != null) button.Add(icon); else button.text = fallbackText;
            return button;
        }

        static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);

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
            _lastSafeArea = safe;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }

        sealed class ActivityRing : VisualElement
        {
            readonly float _progress;

            public ActivityRing(float progress)
            {
                _progress = Mathf.Clamp01(progress);
                AddToClassList("activity-ring");
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0f || rect.height <= 0f) return;
                var painter = context.painter2D;
                var center = rect.center;
                var radius = Mathf.Min(rect.width, rect.height) * 0.39f;
                painter.lineWidth = 34f;
                painter.lineCap = LineCap.Round;
                painter.strokeColor = Rgb(239, 237, 222);
                painter.BeginPath();
                painter.Arc(center, radius, Angle.Degrees(-90f), Angle.Degrees(269.9f), ArcDirection.Clockwise);
                painter.Stroke();
                if (_progress <= 0f) return;
                painter.strokeColor = Primary;
                painter.BeginPath();
                painter.Arc(center, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 359.9f * _progress), ArcDirection.Clockwise);
                painter.Stroke();
            }
        }
    }
}
