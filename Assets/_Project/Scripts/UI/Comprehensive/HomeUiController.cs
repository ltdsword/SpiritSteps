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
        static readonly Color ActivityDeepGreen = Rgb(74, 150, 90);
        static readonly Color AlertRed = Rgb(224, 78, 63);
        static readonly ActivityPeriod[] PeriodTabs = { ActivityPeriod.Week, ActivityPeriod.Month, ActivityPeriod.Year };

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
        GeoPoint? _lastRenderedMapFix;
        bool _lastRenderedMapIsWalking;
        int _setupStep;
        int _featuredCompanionIndex;
        string _pendingDisplayName = string.Empty;
        bool _displayNameFieldFocused;
        bool _accountResetConfirming;
        bool _accountNameEditing;
        string _pendingAccountDisplayName = string.Empty;
        float _lastKeyboardInsetPixels;
        int _viewerPhotoIndex;
        ActivityPeriod _activityPeriod;
        int _activityOffset;
        Label _walkDistanceValueLabel;
        Label _walkCoinsValueLabel;
        Label _walkStepsValueLabel;
        VisualElement _walkProgressFill;
        string _nearbyLandmarkAlertId;
        VisualElement _nearbyLandmarkAlertButton;
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
            // The map is an Android native WebView owned by the DontDestroyOnLoad runtime, so it
            // outlives the Home scene and renders above Unity's AR camera unless explicitly hidden.
            // The next HomeUiController enables it again from Start/SyncMapViewVisibility when the
            // player returns to a route that actually displays the map.
            if (_runtime != null && _runtime.MapView != null) _runtime.MapView.SetActive(false);
        }

        void Update()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != _lastSafeArea || screenSize != _lastScreenSize) ApplySafeArea();
            var keyboardInsetPixels = CurrentKeyboardInsetPixels();
            if (!Mathf.Approximately(keyboardInsetPixels, _lastKeyboardInsetPixels))
            {
                _lastKeyboardInsetPixels = keyboardInsetPixels;
                ApplySafeArea();
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // Escape/Android back closes whatever overlay is on top (photo viewer, sheet,
                // modal...) first, instead of falling through to the screen underneath - without
                // this, back silently navigated that screen while the overlay stayed open on top.
                if (_overlayScrim != null) RemoveTransientOverlay();
                else HandleBack();
            }
            if (IsMapRoute() && _runtime.MapView != null && _runtime.MapView.IsAvailable)
                RenderRealMapMarkers();
            if (IsMapRoute()) RefreshNearbyLandmarkAlert();
            if (_runtime.Navigator.CurrentRoute == UiRoute.ActiveWalk) RefreshWalkControlCard();
        }

        // Independent of the single-slot Mission Card (tutorial -> milestone -> landmark): this
        // shows for ANY undiscovered landmark in range, so it can be visible at the same time as an
        // unrelated tutorial/milestone mission (see docs/images/map.png). Only toggles the cached
        // button's visibility in place when the nearby landmark actually changes, matching the
        // "only act when state differs" pattern RenderRealMapMarkers already uses for the WebView push.
        void RefreshNearbyLandmarkAlert()
        {
            var nextId = FindNearbyUndiscoveredLandmarkId();
            if (nextId == _nearbyLandmarkAlertId) return;
            if (_overlayScrim != null && _overlayScrim.Q(className: "memory-panel") != null)
                RemoveTransientOverlay();
            _nearbyLandmarkAlertId = nextId;
            if (_nearbyLandmarkAlertButton != null)
                _nearbyLandmarkAlertButton.style.display = nextId != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // The nearby mission and Landmark scanner share one radius so the mission never appears
        // before its contextual Scan action is available.
        const float NearbyLandmarkAlertRadiusMetres = LandmarkGeoData.DefaultUnlockRadiusMeters;

        // Picks the NEAREST not-yet-discovered landmark within range, not just the first one in catalog
        // order, so if two are simultaneously in range the closer one is the one offered.
        string FindNearbyUndiscoveredLandmarkId()
        {
            if (_data == null) return null;
            string nearestId = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var landmark in _data.Landmarks)
            {
                if (!_runtime.IsLandmarkScanSupported(landmark.id)) continue;
                if (IsStampCollected(landmark.id)) continue;
                var distance = _runtime.LandmarkMapProvider.GetLandmarkProximity(landmark.id).distanceMetres;
                if (distance > NearbyLandmarkAlertRadiusMetres || distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestId = landmark.id;
            }
            return nearestId;
        }

        // Render() only rebuilds the walk-control-card on route/overlay changes, but distance/steps/coins
        // change every frame during a walk - rewriting just these labels each frame keeps the numbers ticking
        // live without rebuilding the whole page (which OnLocationUpdated firing every frame would make costly).
        void RefreshWalkControlCard()
        {
            if (_walkDistanceValueLabel == null || !_runtime.WalkProvider.IsWalking) return;
            var metrics = _runtime.WalkProvider.GetLiveMetrics();
            var weekly = _runtime.GetWeeklyActivity();
            _walkDistanceValueLabel.text = metrics.distanceKilometres.ToString("0.0");
            if (_walkCoinsValueLabel != null) _walkCoinsValueLabel.text = "+" + WalkCoinsPreview(metrics.distanceKilometres);
            if (_walkStepsValueLabel != null) _walkStepsValueLabel.text = metrics.hasSteps ? metrics.steps.ToString("N0") : "--";
            if (_walkProgressFill != null) _walkProgressFill.style.width = Length.Percent(DailyGoalRatio(metrics.distanceKilometres, weekly.dailyGoalKilometres) * 100f);
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
        /// <summary>Opens the floating Landmark sheet for the given landmark id (test/entry-point hook).</summary>
        public void ShowLandmark(string landmarkId) => ShowLandmarkSheet(landmarkId);
        /// <summary>Opens the Account panel (test/entry-point hook) - mirrors tapping the top-bar profile button.</summary>
        public void ShowAccount() => ShowAccountPanel();
        /// <summary>Closes whichever floating sheet/modal is open, if any (test/entry-point hook).</summary>
        public void CloseFloatingOverlay() => RemoveTransientOverlay();
        /// <summary>Opens the Shop's Pet Detail modal for a companion index (test/entry-point hook).</summary>
        public void ShowPetDetail(int index) => ShowPetDetailModal(index);
        /// <summary>Id of the landmark the nearby-landmark alert button currently targets, or null if
        /// it isn't showing (test/entry-point hook).</summary>
        public string NearbyLandmarkAlertId => _nearbyLandmarkAlertId;
        /// <summary>Opens the nearby-landmark "Find & scan this memory" panel (test/entry-point hook) -
        /// mirrors tapping the map's red "!" alert button. No-op if it isn't currently showing.</summary>
        public void ShowNearbyMemory()
        {
            if (_nearbyLandmarkAlertId != null) ShowNearbyMemoryPanel(_nearbyLandmarkAlertId);
        }
        /// <summary>Claims the Map's current Mission Card reward, if any is claimable (test/entry-point hook).</summary>
        public bool ClaimCurrentMission()
        {
            var mission = _runtime.CurrentMission();
            var claimed = _runtime.ClaimCurrentMission();
            if (!claimed) return false;
            Render();
            ShowToast(string.IsNullOrEmpty(mission?.rewardLabel) ? "Mission reward claimed!" : "Reward claimed · " + mission.rewardLabel);
            return true;
        }
        /// <summary>Switches the Activity Dashboard's Week/Month/Year tab (test/entry-point hook) - mirrors tapping a period tab, resetting to the current period's offset.</summary>
        public void SetActivityPeriod(ActivityPeriod period) { _activityPeriod = period; _activityOffset = 0; Render(); }

        void BuildRoot()
        {
            var root = _document.rootVisualElement;
            root.Clear();
            var styleSheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            _panel = new AppPanel { name = "ar-walking-app-panel", theme = "light", scale = "medium" };
            _panel.AddToClassList("app-root");
            root.Add(_panel);
            // App UI's Panel centers its notification container both vertically and horizontally by
            // default, which stacks toasts on top of each other and overflows the screen when several
            // fire in quick succession (e.g. spamming the Feed button) - anchor it to the top instead;
            // ShowToast itself keeps only one toast on screen at a time.
            _panel.notificationContainer.style.justifyContent = Justify.FlexStart;
            _panel.notificationContainer.style.paddingTop = 140f;
            _safeRoot = Element("safe-area", "safe-area");
            _panel.Add(_safeRoot);
        }

        void OnNavigationChanged()
        {
            Render();
            RenderOverlay();
            SyncMapViewVisibility();
        }

        // The native WebView map is a separate OS-level surface, not a Unity-rendered element - it
        // paints over anything docked within its margins regardless of Unity's own z-order. Any
        // floating sheet/modal (_overlayScrim - Account panel, Feed sheet, Pet Detail, Landmark
        // sheet, food picker) must hide it while open, not just route-level UiOverlay navigation.
        void SyncMapViewVisibility()
        {
            if (_runtime.MapView == null) return;
            var onMapWithNoOverlay = IsMapRoute() && _runtime.Navigator.CurrentOverlay == null && _overlayScrim == null;
            _runtime.MapView.SetActive(onMapWithNoOverlay);
        }

        // BuildMap() renders the real/illustrated map for both of these routes (an active walk still shows
        // the map), so anything gating on "is the map currently on screen" must match that pair, not just
        // HomeMap alone - that mismatch previously left the WebView disabled for the entire ActiveWalk route.
        bool IsMapRoute() => _runtime.Navigator.CurrentRoute == UiRoute.HomeMap || _runtime.Navigator.CurrentRoute == UiRoute.ActiveWalk;

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
                // On-device the on-screen keyboard would otherwise sit directly over this field (it's
                // the second-to-last element in a bottom-docked sheet) - shift the whole page up while
                // the field is focused so the field (and the button below it) stay above the keyboard.
                field.RegisterCallback<FocusInEvent>(_ => { _displayNameFieldFocused = true; _lastKeyboardInsetPixels = CurrentKeyboardInsetPixels(); ApplySafeArea(); });
                field.RegisterCallback<FocusOutEvent>(_ => { _displayNameFieldFocused = false; _lastKeyboardInsetPixels = CurrentKeyboardInsetPixels(); ApplySafeArea(); });
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
                sheet.Add(Body("Your first companion starts as a Baby. Feed it to help it grow, and walk together to earn Coins."));
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

            // The native WebView surface is a separate OS-level view positioned by SetMargins below,
            // not a Unity-rendered element - it draws over any UI Toolkit content inside its bounds
            // regardless of Unity's own z-order. When the Mission Card is showing, its bottom edge
            // (not just the top bar's) must set the WebView's top margin, or the card gets hidden
            // behind the map surface.
            var missionCard = BuildMissionCard(118f);
            VisualElement topBoundary = top;
            if (missionCard != null) { page.Add(missionCard); topBoundary = missionCard; }

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

            // The nearby-landmark alert lives in the top status bar (next to the Activity Dashboard
            // button), not as a floating overlay here - the native WebView surface always paints over
            // Unity content within its margins regardless of z-order, so nothing docked mid-map would
            // ever stay visible.
            _runtime.LocationService.Activate();
            _runtime.MapView.OnMarkerTapped -= OnRealMapMarkerTapped; // avoid a duplicate subscription if BuildMap runs again
            _runtime.MapView.OnMarkerTapped += OnRealMapMarkerTapped;

            page.RegisterCallback<GeometryChangedEvent>(_ => ApplyRealMapMargins(topBoundary, bottom));
            ApplyRealMapMargins(topBoundary, bottom);
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

        // Called every frame while a map route is showing (see Update()). Pushing a full state - player,
        // landmarks, and a growing walk trail - through EvaluateJS on every one of those frames regardless of
        // whether anything changed would scale badly as a walk gets longer, so this only pushes again once the
        // player's fix or the walking on/off state actually differs from what was last pushed.
        void RenderRealMapMarkers()
        {
            if (!_runtime.LocationService.HasFix) return;
            var current = _runtime.LocationService.Current;
            var isWalking = _runtime.WalkProvider.IsWalking;
            if (_lastRenderedMapFix.HasValue && _lastRenderedMapFix.Value.Equals(current) && _lastRenderedMapIsWalking == isWalking) return;
            _lastRenderedMapFix = current;
            _lastRenderedMapIsWalking = isWalking;

            var markers = new List<WebViewMapMarker>();
            foreach (var marker in _mapData.Markers)
            {
                if (marker.type != MapMarkerType.Landmark) continue;
                var landmark = _runtime.GeoCatalog?.Find(marker.targetId);
                if (landmark == null) continue;
                markers.Add(new WebViewMapMarker(marker.targetId, marker.label, landmark.Location));
            }
            var trail = isWalking ? _runtime.WalkProvider.GetLiveMetrics().trail : Array.Empty<GeoPoint>();
            _runtime.MapView.Render(current, markers, trail);
        }

        void OnRealMapMarkerTapped(string landmarkId) => ShowLandmarkSheet(landmarkId);

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

            var missionCard = BuildMissionCard(194f);
            if (missionCard != null) page.Add(missionCard);

            var controls = Element("map-controls", "map-controls");
            controls.Add(IconAction("navigation", _assets != null ? _assets.iconCompass : null, "CTR", manipulator.Recenter, "recenter-button", "map-round-control"));
            controls.Add(IconAction("camera", _assets != null ? _assets.iconCamera : null, "CAM", BeginArPhotoPick, "map-photo-button", "map-round-control", "blossom-control"));
            page.Add(controls);
            page.Add(BuildWalkControlCard());
        }

        /// <summary>Small "!" button in the top status bar, next to the Activity Dashboard button (see
        /// docs/images/map.png) - visible whenever <see cref="FindNearbyUndiscoveredLandmarkId"/> finds
        /// any not-yet-discovered landmark within its unlock radius. Independent of the Mission Card's
        /// single active-mission slot, so both can show at once. Lives in the top bar rather than
        /// floating over the map itself because the real map's native WebView surface always paints
        /// over Unity content within its bounds regardless of z-order, so nothing docked mid-map can
        /// ever stay visible there.</summary>
        VisualElement BuildNearbyLandmarkAlertButton()
        {
            var button = IconAction("alert", null, "!", ShowNearbyMemory,
                "nearby-landmark-alert-button", "top-hub-button", "top-alert-button", "dark-round-control");
            button.style.display = _nearbyLandmarkAlertId != null ? DisplayStyle.Flex : DisplayStyle.None;
            _nearbyLandmarkAlertButton = button;
            return button;
        }

        VisualElement BuildWalkControlCard()
        {
            var walking = _runtime.WalkProvider.IsWalking;
            var weekly = _runtime.GetWeeklyActivity();
            // This card is a session HUD. Daily/lifetime activity belongs in the top status bar and
            // Activity Dashboard, so an idle card must always start from a clean zero state.
            var metrics = walking ? _runtime.WalkProvider.GetLiveMetrics() : new WalkMetrics { hasSteps = true };
            var card = Card("walk-control-card", "floating-surface", "elevated-card");
            var top = Row("walk-summary-row");
            var main = Column("walk-main-metric");
            main.Add(Label(walking ? "Walk in progress" : "Ready to explore", "small-label"));
            var distance = Element(null, "walk-distance-line");
            _walkDistanceValueLabel = Label(metrics.distanceKilometres.ToString("0.0"), "walk-distance-value");
            distance.Add(_walkDistanceValueLabel);
            distance.Add(Label("km", "walk-distance-unit"));
            main.Add(distance);
            top.Add(main);
            var coinsMetric = Metric("+" + WalkCoinsPreview(metrics.distanceKilometres), "coins", "walk-mini-metric", "sun-value");
            _walkCoinsValueLabel = coinsMetric.Q<Label>(className: "metric-value");
            top.Add(coinsMetric);
            var stepsMetric = Metric(metrics.hasSteps ? metrics.steps.ToString("N0") : "--", "steps", "walk-mini-metric", "blossom-value");
            _walkStepsValueLabel = stepsMetric.Q<Label>(className: "metric-value");
            top.Add(stepsMetric);
            card.Add(top);

            var goal = Row("walk-goal-row");
            goal.Add(IconView("footprints", "walk-goal-icon", Primary, _assets != null ? _assets.iconSteps : null));
            var progressTrack = Progress(DailyGoalRatio(metrics.distanceKilometres, weekly.dailyGoalKilometres), "walk-progress");
            _walkProgressFill = progressTrack.Q<VisualElement>(className: "progress-fill");
            goal.Add(progressTrack);
            goal.Add(Label(weekly.dailyGoalKilometres.ToString("0") + " km goal", "walk-goal-label"));
            card.Add(goal);
            card.Add(ActionWithIcon(walking ? "square" : "play", _assets != null ? _assets.iconSteps : null,
                walking ? "End Walk" : "Start Walk", walking ? (Action)(() => FinishWalk()) : BeginWalk,
                walking ? "blossom-action" : "primary-action", "walk-toggle-action"));
            return card;
        }

        /// <summary>Live coin estimate for the Walk HUD while walking - mirrors
        /// <see cref="CompanionProgressionService.CompleteWalk"/>'s lead-companion-only formula so the
        /// number shown while walking matches what FinishWalk() will actually award.</summary>
        int WalkCoinsPreview(float distanceKilometres)
        {
            var leadId = _runtime.SaveData?.leadCompanionId;
            if (string.IsNullOrEmpty(leadId)) return 0;
            var progress = _runtime.Companion(leadId);
            if (progress == null || !progress.owned) return 0;
            var entry = CompanionRoster.Find(leadId);
            if (string.IsNullOrEmpty(entry.Id)) return 0;
            var incomePerHundredMetres = CompanionProgressionService.IncomeOf(entry, progress.growthExperience);
            return Mathf.RoundToInt(incomePerHundredMetres * (distanceKilometres * 1000f / 100f));
        }

        /// <summary>The floating Mission Card (design doc "one component for all mission types") -
        /// returns null when there's nothing active to show.</summary>
        VisualElement BuildMissionCard(float topOffset)
        {
            var mission = _runtime.CurrentMission();
            if (mission == null) return null;

            var card = Card("mission-card", "elevated-card", "floating-surface");
            card.style.top = topOffset;
            var header = Row("mission-card-header");
            var iconWell = Element(null, "mission-icon-well");
            iconWell.Add(Label(mission.iconKey, "mission-icon-emoji"));
            header.Add(iconWell);
            var copy = Column();
            copy.Add(Label(mission.type.ToString().ToUpperInvariant() + " · MISSION", "mission-kicker"));
            copy.Add(Label(mission.title, "mission-title"));
            header.Add(copy);
            if (mission.type != MissionType.Landmark)
                header.Add(Label(MissionProgressLabel(mission), "mission-progress-label"));
            card.Add(header);

            var ratio = mission.targetValue > 0f ? Mathf.Clamp01(mission.currentValue / mission.targetValue) : 1f;
            card.Add(Progress(ratio, "mission-progress-track"));

            if (mission.status == MissionStatus.Active)
            {
                var description = Body(mission.description);
                description.AddToClassList("mission-description");
                card.Add(description);
            }
            else if (mission.type == MissionType.Landmark)
                card.Add(ActionWithIcon("map-pin", _assets != null ? _assets.iconMap : null, "Explore " + mission.landmarkName,
                    () => ShowLandmarkSheet(mission.landmarkId), "primary-action", "mission-claim-button"));
            else
                card.Add(ActionWithIcon("sparkles", null, "Mission Complete — Tap to Claim",
                    () => ClaimCurrentMission(), "mission-claim-button", "icon-action-button"));
            return card;
        }

        static string MissionProgressLabel(MissionUiState mission)
        {
            if (mission.missionId == "tutorial-walk")
                return Mathf.RoundToInt(mission.currentValue * 1000f) + " / " + Mathf.RoundToInt(mission.targetValue * 1000f) + " m";
            if (mission.type == MissionType.Walking)
                return mission.currentValue.ToString("0.0") + " / " + mission.targetValue.ToString("0") + " km";
            return string.Empty;
        }

        void BuildWalkResult()
        {
            var result = _runtime.LastWalkResult ?? new WalkResultDto();
            var page = Page("walk-result-page", false);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "walk-result-scroll" };
            scroll.AddToClassList("walk-result-scroll");
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
            card.Add(Eyebrow("WALKING INCOME"));
            if (string.IsNullOrEmpty(result.leadCompanionId) || result.coinsAwarded <= 0)
                card.Add(Body("Set an active companion as your lead to earn Coins while you walk."));
            else
            {
                var row = Row("growth-reward-row");
                row.Add(Image(_assets != null ? _assets.Companion(FindCompanionIndex(result.leadCompanionId)) : null, "growth-reward-pet", ScaleMode.ScaleToFit));
                var copy = Column();
                copy.Add(Subtitle(CompanionName(result.leadCompanionId)));
                copy.Add(Body("Your lead companion"));
                row.Add(copy);
                row.Add(Label("+" + result.coinsAwarded + " coins", "growth-reward-value"));
                card.Add(row);
            }
            foreach (var id in result.newlyUnlockedCompanionIds)
                card.Add(InfoRow("sparkles", "New friend", CompanionName(id) + " joined your walk", "sun-info"));
            card.Add(ActionWithIcon("sparkles", null, "Collect & continue", () => SelectRoot(UiRootTab.Companions), "primary-action"));
            celebration.Add(card);
            scroll.Add(celebration);
            page.Add(scroll);

            // Keep the exit above the scrolling layer so it remains reachable even when a large
            // roster makes the reward summary several screens tall.
            var close = IconAction("x", _assets != null ? _assets.iconClose : null, "X", () => SelectRoot(UiRootTab.Map), "walk-result-close", "dark-round-control");
            page.Add(close);
        }

        void BuildCompanions()
        {
            var scroll = ScreenWithHeader("Companions", OwnedCompanionCount() + " friends walking with you", false,
                "camera", "AR Photo", BeginArPhotoPick, "blossom-chip", true);

            _featuredCompanionIndex = Mathf.Clamp(_featuredCompanionIndex, 0, _data.Companions.Count - 1);
            if (!IsOwned(_featuredCompanionIndex)) _featuredCompanionIndex = FirstOwnedCompanionIndex();
            if (OwnedCompanionCount() > 0) scroll.Add(BuildFeaturedCompanion(_featuredCompanionIndex));

            var ownedGrid = Element("owned-companion-grid", "owned-companion-grid");
            var ownedCount = 0;
            for (var i = 0; i < _data.Companions.Count; i++)
            {
                if (!IsOwned(i)) continue;
                ownedCount++;
                var index = i;
                var definition = _data.Companions[i];
                var progress = _runtime.Companion(definition.id);
                var entry = CompanionRoster.Find(definition.id);
                var button = new UiButton(() => { _featuredCompanionIndex = index; Render(); }) { name = "companion-" + definition.id };
                button.AddToClassList("owned-companion-card");
                if (_featuredCompanionIndex == i) button.AddToClassList("selected-companion-card");
                var well = Element(null, "companion-thumb-well", "accent-surface-" + (i % 4));
                well.Add(Image(_assets != null ? _assets.Companion(i) : null, "companion-thumb", ScaleMode.ScaleAndCrop));
                button.Add(well);
                button.Add(Label(definition.name, "companion-thumb-name"));
                button.Add(Label(CompanionProgressionService.StageFor(entry, progress.growthExperience).ToString(), "companion-thumb-stage"));
                ownedGrid.Add(button);
            }
            PadGridRow(ownedGrid, ownedCount, 3, "owned-companion-card");
            scroll.Add(ownedGrid);
        }

        /// <summary>Three-column grids use <c>justify-content: space-between</c> so full rows
        /// space their cards evenly, but that same rule stretches an incomplete last row's cards
        /// out to the container's edges instead of packing them to the left. Padding the row out
        /// to a full set of <paramref name="columns"/> with invisible, same-width filler elements
        /// keeps the real cards left-aligned without needing CSS Grid or :nth-child (neither
        /// available in UI Toolkit's USS subset).</summary>
        static void PadGridRow(VisualElement grid, int itemCount, int columns, string itemClass)
        {
            var remainder = itemCount % columns;
            if (remainder == 0) return;
            for (var i = 0; i < columns - remainder; i++)
            {
                var filler = new VisualElement { pickingMode = PickingMode.Ignore };
                filler.AddToClassList(itemClass);
                filler.style.visibility = Visibility.Hidden;
                grid.Add(filler);
            }
        }

        VisualElement BuildFeaturedCompanion(int index)
        {
            var definition = _data.Companions[index];
            var progress = _runtime.Companion(definition.id);
            var entry = CompanionRoster.Find(definition.id);
            var stage = CompanionProgressionService.StageFor(entry, progress.growthExperience);
            var wrapper = Column("featured-companion-wrapper");
            wrapper.Add(BuildFoodInventoryRow());

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
            copy.Add(Pill(entry.Rarity.ToString().ToUpperInvariant(), "rarity-pill", "rarity-" + entry.Rarity.ToString().ToLowerInvariant()));
            copy.Add(BuildAutoScrollingDescription(definition.description));
            var dotsRow = Row("featured-dots-row");
            dotsRow.Add(StageDots(stage));
            dotsRow.Add(Label(NextStageLabel(stage), "next-stage-label"));
            copy.Add(dotsRow);
            hero.Add(copy);
            card.Add(hero);

            var growth = Element(null, "featured-growth");
            var growthHeader = Row("growth-header");
            var growthLabel = Row("growth-label");
            growthLabel.Add(IconView("sparkles", "growth-icon", SunInk));
            growthLabel.Add(Label(stage == GrowthStage.Adult ? "Fully grown" : "Growth", "small-strong-label"));
            growthHeader.Add(growthLabel);
            growthHeader.Add(Label(GrowthCaption(entry, progress.growthExperience, stage), "small-label"));
            growth.Add(growthHeader);
            growth.Add(Progress(GrowthRatio(entry, progress.growthExperience, stage), "growth-progress"));
            growth.Add(Body("Feed " + definition.name + " to help it grow."));
            growth.Add(WalkingIncomeRow(entry, progress.growthExperience));

            var isLead = _runtime.LeadCompanionId == definition.id;
            var actions = Row("featured-actions");
            actions.Add(ActionWithIcon("heart", null, "Feed", () => _runtime.EnterPet3D(definition.id), "blossom-action", "half-action"));
            actions.Add(ActionWithIcon("paw-print", _assets != null ? _assets.iconCompanions : null,
                isLead ? "Leading" : "Set as lead",
                () => { _runtime.SetLeadCompanion(definition.id); Render(); },
                isLead ? "secondary-action" : "primary-action", "half-action"));
            growth.Add(actions);
            var details = Action("View companion details", () => { _runtime.SelectedCompanionIndex = index; Navigate(UiRoute.CompanionDetail); }, "text-action");
            growth.Add(details);
            card.Add(growth);
            wrapper.Add(card);
            return wrapper;
        }

        VisualElement WalkingIncomeRow(CompanionRoster.Entry entry, int growthExperience)
        {
            var row = Element(null, "info-row", "walking-income-row");
            var well = Element(null, "info-icon-well");
            well.Add(IconView("coins", "info-icon", SunInk));
            row.Add(well);
            row.Add(Label("Walking income", "small-strong-label"));
            var spacer = Element(null);
            spacer.style.flexGrow = 1;
            row.Add(spacer);
            row.Add(Label(CompanionProgressionService.IncomeOf(entry, growthExperience).ToString("0.0") + " coins / 100m", "walking-income-value"));
            return row;
        }

        VisualElement BuildFoodInventoryRow()
        {
            var row = Row("food-inventory-row");
            for (var i = 0; i < _data.Foods.Count; i++)
            {
                var food = _data.Foods[i];
                var chip = new UiButton(() => SelectRoot(UiRootTab.Shop)) { name = "food-chip-" + food.id };
                chip.AddToClassList("food-inventory-chip");
                var well = Element(null, "food-inventory-icon-well", "accent-surface-" + (i % 4));
                well.Add(Image(_assets != null ? _assets.Food(i) : null, "food-inventory-icon", ScaleMode.ScaleAndCrop));
                chip.Add(well);
                var copy = Column();
                var nameRow = Row("food-inventory-name-row");
                nameRow.Add(Label(food.name, "food-inventory-name"));
                nameRow.Add(Label("×" + _runtime.FoodQuantity(food.id), "food-inventory-quantity"));
                copy.Add(nameRow);
                copy.Add(Label("+" + food.growthExperience + " EXP", "food-inventory-exp"));
                chip.Add(copy);
                row.Add(chip);
            }
            return row;
        }

        void BuildCompanionDetail()
        {
            var index = Mathf.Clamp(_runtime.SelectedCompanionIndex, 0, _data.Companions.Count - 1);
            var definition = _data.Companions[index];
            var progress = _runtime.Companion(definition.id);
            var owned = progress != null && progress.owned;
            var scroll = ScreenWithHeader(definition.name, owned ? StageLine(progress) : definition.unlockHint, true);
            if (!owned)
            {
                var locked = Card("companion-detail-locked", "elevated-card");
                locked.Add(IconView("lock", "detail-lock-icon", MutedInk));
                locked.Add(Title("A friend you have yet to meet"));
                locked.Add(Body(progress != null && progress.unlocked ? "Unlocked - buy it in the Shop to bring it home." : definition.unlockHint));
                scroll.Add(locked);
                return;
            }

            scroll.Add(BuildFeaturedCompanion(index));
            var entry = CompanionRoster.Find(definition.id);
            var story = Card("companion-story-card");
            story.Add(Eyebrow("YOUR COMPANION"));
            story.Add(Subtitle("Grow together, one walk at a time"));
            story.Add(Body("Baby · under " + entry.YoungExp + " EXP\nYoung · " + entry.YoungExp + "–" + (entry.AdultExp - 1) + " EXP\nAdult · " + entry.AdultExp + "+ EXP"));
            scroll.Add(story);
            scroll.Add(ActionWithIcon("paw-print", _assets != null ? _assets.iconCompanions : null, "View in AR", () => _runtime.EnterPetAr(definition.id, false), "primary-action"));
        }

        void BuildShop()
        {
            var scroll = ScreenWithHeader("Shop", "Spend coins to grow your friends", false, "coins", _runtime.SaveData.coins.ToString("N0"), null, "sun-chip");

            scroll.Add(SectionTitle("Companion Food"));
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
                var buy = new UiButton(() =>
                {
                    var result = _runtime.PurchaseFood(_data.Foods[foodIndex].id, 1);
                    ShowToast(result.success ? "+1 " + _data.Foods[foodIndex].name : result.error);
                    if (result.success) Render();
                }) { name = "buy-" + food.id };
                buy.AddToClassList("price-pill");
                buy.Add(IconView("coins", "price-icon", White));
                buy.Add(Label(food.coinCost.ToString(), "price-label"));
                card.Add(buy);
                scroll.Add(card);
            }

            scroll.Add(SectionTitle("Companions"));
            var petGrid = Element("shop-pet-grid", "shop-pet-grid");
            for (var i = 0; i < _data.Companions.Count; i++)
            {
                var progress = _runtime.Companion(_data.Companions[i].id);
                // Already-owned companions have nothing left to buy here - they live on the
                // Companions tab instead, where Feed/Set as lead actually apply to them.
                if (progress != null && progress.owned) continue;

                var index = i;
                var definition = _data.Companions[i];
                var entry = CompanionRoster.Find(definition.id);
                var unlocked = progress != null && progress.unlocked;

                var card = new UiButton(() => ShowPetDetailModal(index)) { name = "shop-pet-" + definition.id };
                card.AddToClassList("card");
                card.AddToClassList("shop-pet-card");
                if (!unlocked) card.AddToClassList("shop-pet-card-dim");

                var imageWell = Element(null, "shop-pet-image-well", "accent-surface-" + (i % 4));
                imageWell.Add(Image(_assets != null ? _assets.Companion(i) : null, "shop-pet-image", ScaleMode.ScaleAndCrop));
                imageWell.Add(Pill(entry.Rarity.ToString().ToUpperInvariant(), "rarity-pill", "rarity-" + entry.Rarity.ToString().ToLowerInvariant(), "shop-pet-rarity-badge"));
                var statusBadge = Element(null, "shop-pet-status-badge");
                statusBadge.Add(IconView(unlocked ? "coins" : "lock", "shop-pet-status-icon", unlocked ? SunInk : MutedInk));
                imageWell.Add(statusBadge);
                card.Add(imageWell);

                card.Add(Label(definition.name, "shop-pet-name"));
                var reqRatio = entry.UnlockDistanceKilometres > 0f ? Mathf.Clamp01(_runtime.SaveData.totalDistanceKilometres / entry.UnlockDistanceKilometres) : 1f;
                card.Add(Progress(unlocked ? 1f : reqRatio, "shop-pet-progress"));

                var footer = Row("shop-pet-footer");
                footer.Add(Label(entry.UnlockDistanceKilometres <= 0f ? "Starter" : unlocked ? "Unlocked" : entry.UnlockDistanceKilometres.ToString("0.#") + " km required", "shop-pet-req-label"));
                var priceRow = Row("shop-pet-price");
                priceRow.Add(IconView("coins", "shop-pet-price-icon", SunInk));
                priceRow.Add(Label(entry.PriceCoins <= 0 ? "Free" : entry.PriceCoins.ToString("N0"), "shop-pet-price-label"));
                footer.Add(priceRow);
                card.Add(footer);

                petGrid.Add(card);
            }
            scroll.Add(petGrid);

            var note = Card("shop-note-card");
            note.Add(IconView("paw-print", "shop-note-icon", Primary));
            note.Add(Body("Earn more coins by walking, discovering landmarks, and completing AR memories."));
            scroll.Add(note);
        }

        /// <summary>Floating Pet Detail card (design doc "not a new screen") opened from a Shop
        /// companion card - shows unlock progress, income/growth stats, price, and a state-dependent
        /// CTA (Locked / Buy / Set as lead / Leading).</summary>
        void ShowPetDetailModal(int index)
        {
            var definition = _data.Companions[index];
            var progress = _runtime.Companion(definition.id);
            var entry = CompanionRoster.Find(definition.id);
            var owned = progress != null && progress.owned;
            var unlocked = progress != null && progress.unlocked;
            var isLead = _runtime.SaveData.leadCompanionId == definition.id;

            // withCloseButton:false - the hero image bleeds over the modal's top edge (negative
            // margin), so a close button added before it would paint underneath it; add our own
            // after the hero instead so it's the later (topmost-painted) sibling.
            var modal = ShowCenteredModal("pet-detail-modal", false);
            var hero = Element(null, "pet-detail-hero", "accent-surface-" + (index % 4));
            hero.Add(Image(_assets != null ? _assets.Companion(index) : null, "pet-detail-hero-image", ScaleMode.ScaleAndCrop));
            hero.Add(Pill(entry.Rarity.ToString().ToUpperInvariant(), "rarity-pill", "rarity-" + entry.Rarity.ToString().ToLowerInvariant(), "pet-detail-rarity-badge"));
            modal.Add(hero);
            modal.Add(IconAction("x", _assets != null ? _assets.iconClose : null, "X", RemoveTransientOverlay, "pet-detail-close", "small-round-control"));

            var nameRow = Row("pet-detail-name-row");
            nameRow.Add(Title(definition.name));
            var stateLabel = Label(owned ? (isLead ? "Active companion" : "Owned") : unlocked ? "Unlocked" : "Locked", "pet-detail-state");
            stateLabel.style.color = owned ? Primary : unlocked ? SunInk : MutedInk;
            nameRow.Add(stateLabel);
            modal.Add(nameRow);
            modal.Add(Body(definition.description));

            var reqRatio = entry.UnlockDistanceKilometres > 0f ? Mathf.Clamp01(_runtime.SaveData.totalDistanceKilometres / entry.UnlockDistanceKilometres) : 1f;
            var progressBar = Progress(unlocked ? 1f : reqRatio, "pet-detail-progress");
            if (unlocked) progressBar.AddToClassList("pet-detail-progress-gold");
            modal.Add(progressBar);
            modal.Add(Label(entry.UnlockDistanceKilometres <= 0f ? "Starter companion" : unlocked
                ? entry.UnlockDistanceKilometres.ToString("0.#") + " / " + entry.UnlockDistanceKilometres.ToString("0.#") + " km"
                : _runtime.SaveData.totalDistanceKilometres.ToString("0.0") + " / " + entry.UnlockDistanceKilometres.ToString("0.#") + " km", "pet-detail-progress-label"));

            var statGrid = Row("pet-detail-stats");
            var incomeStat = Element(null, "pet-detail-stat", "pet-detail-income-stat");
            incomeStat.Add(Label("WALKING INCOME", "pet-detail-stat-label"));
            incomeStat.Add(Label("Young " + entry.BaseIncomePerHundredMetres.ToString("0.0") + " /100m", "pet-detail-stat-value"));
            incomeStat.Add(Label("Adult " + (entry.BaseIncomePerHundredMetres * 1.30f).ToString("0.0") + " /100m", "pet-detail-stat-value"));
            statGrid.Add(incomeStat);
            var expStat = Element(null, "pet-detail-stat", "pet-detail-exp-stat");
            expStat.Add(Label("GROWTH EXP", "pet-detail-stat-label"));
            expStat.Add(Label("Young " + entry.YoungExp, "pet-detail-stat-value"));
            expStat.Add(Label("Adult " + entry.AdultExp, "pet-detail-stat-value"));
            statGrid.Add(expStat);
            modal.Add(statGrid);

            var priceRow = Row("pet-detail-price-row");
            priceRow.Add(IconView("coins", "pet-detail-price-icon", SunInk));
            priceRow.Add(Label(entry.PriceCoins <= 0 ? "Free" : entry.PriceCoins.ToString("N0") + " coins", "pet-detail-price-label"));
            modal.Add(priceRow);

            if (!unlocked)
                modal.Add(Action("Locked · " + Mathf.Max(0f, entry.UnlockDistanceKilometres - _runtime.SaveData.totalDistanceKilometres).ToString("0.#") + " km to reach", () => { }, "disabled-action"));
            else if (!owned)
                modal.Add(Action("Buy · " + (entry.PriceCoins <= 0 ? "Free" : entry.PriceCoins.ToString("N0") + " coins"), () =>
                {
                    var result = _runtime.PurchaseCompanion(definition.id);
                    if (result.success) { RemoveTransientOverlay(); Render(); } else ShowToast(result.error);
                }, "primary-action"));
            else if (!isLead)
                modal.Add(Action("Set as lead", () => { _runtime.SetLeadCompanion(definition.id); RemoveTransientOverlay(); Render(); }, "primary-action"));
            else
                modal.Add(Action("Leading", () => { }, "leading-action"));
        }

        void BuildLandmarkDetail()
        {
            var landmark = SelectedLandmark();
            var proximity = _runtime.LandmarkMapProvider.GetLandmarkProximity(landmark.id);
            var scroll = ScreenWithHeader("Discover", landmark.localName, true, headerIcon: "map-pin");
            var hero = Element("landmark-hero-card", "landmark-hero-card", "elevated-card");
            hero.Add(Image(_assets != null ? _assets.Landmark(_runtime.SelectedLandmarkIndex) : null, "landmark-hero"));
            var distance = Pill(proximity.distanceMetres.ToString("0") + " m away", "landmark-distance-pill");
            distance.AddToClassList(proximity.isWithinUnlockRadius ? "near-pill" : "far-pill");
            hero.Add(distance);
            scroll.Add(hero);
            scroll.Add(Title(landmark.name));
            scroll.Add(Body("Walk closer, reveal its cultural memory, and add a new stamp to your Journey."));
            scroll.Add(StorySection("History", landmark.history, "history-card", "book-heart"));
            scroll.Add(StorySection("Cultural Significance", landmark.architecture, "architecture-card", "map"));
            scroll.Add(StorySection("Did you know?", landmark.didYouKnow, "fact-card", "sparkles"));
            var collected = IsStampCollected(landmark.id);
            scroll.Add(InfoRow("stamp", collected ? "Stamp collected" : "Passport stamp", collected ? "Saved in your Journey" : "Complete the AR Memory to collect it", collected ? "primary-info" : "blossom-info"));
            if (landmark.imageTargetReady && proximity.isWithinUnlockRadius)
                scroll.Add(ActionWithIcon("sparkles", _assets != null ? _assets.iconAr : null, "Open AR Memory",
                    () => _runtime.EnterPetAr(_runtime.PrimaryCompanionId(), false, PendingPetInteraction.None, landmark.id), "primary-action"));
            else
                scroll.Add(ActionWithIcon("lock", null, "Walk closer to unlock", () => ShowToast("This Landmark is outside the AR unlock radius."), "disabled-action"));
        }

        /// <summary>Floating Landmark sheet (design doc "not a new screen") shown over whatever
        /// screen is currently open - the primary way to view a landmark from a Map pin tap or the
        /// Mission Card. Completed Journey stamps render their full story directly and revisit the
        /// dedicated scanner instead of routing through this Discover view.</summary>
        void ShowLandmarkSheet(string landmarkId)
        {
            var index = FindLandmarkIndex(landmarkId);
            var landmark = _data.Landmarks[index];
            var proximity = _runtime.LandmarkMapProvider.GetLandmarkProximity(landmark.id);

            RemoveTransientOverlay();
            _overlayScrim = Element("landmark-sheet-scrim", "tray-scrim");
            var sheet = Card("landmark-sheet", "landmark-sheet");

            var hero = Element(null, "landmark-sheet-hero");
            hero.Add(Image(_assets != null ? _assets.Landmark(index) : null, "landmark-sheet-hero-image"));
            hero.Add(IconAction("x", _assets != null ? _assets.iconClose : null, "X", RemoveTransientOverlay, "landmark-sheet-close", "small-round-control", "dark-round-control"));
            hero.Add(Pill(proximity.distanceMetres.ToString("0") + " m away", "landmark-sheet-distance"));
            sheet.Add(hero);

            var body = Element(null, "landmark-sheet-body");
            var collected = IsStampCollected(landmark.id);
            if (collected)
            {
                body.Add(Label(landmark.localName, "landmark-sheet-local-name"));
                body.Add(Title(landmark.name));
                body.Add(Body("Walk closer, reveal its cultural memory, and add a new stamp to your Journey."));
                body.Add(StorySection("History", landmark.history, "history-card", "book-heart"));
                body.Add(StorySection("Cultural Significance", landmark.architecture, "architecture-card", "map"));
                body.Add(StorySection("Did you know?", landmark.didYouKnow, "fact-card", "sparkles"));
                body.Add(InfoRow("stamp", "Stamp collected", "Saved in your Journey", "primary-info"));
            }
            else
            {
                body.Add(Title(landmark.name));
                body.Add(Body("There's a mission on this landmark. Please approach this site and explore it."));
                body.Add(InfoRow("lock", "Mission locked", "Explore this landmark to reveal its story", "blossom-info"));
            }
            if (landmark.imageTargetReady && proximity.isWithinUnlockRadius)
                body.Add(ActionWithIcon("sparkles", _assets != null ? _assets.iconAr : null, "Open AR Memory",
                    () => { RemoveTransientOverlay(); _runtime.EnterPetAr(_runtime.PrimaryCompanionId(), false, PendingPetInteraction.None, landmark.id); }, "primary-action"));
            else
                body.Add(ActionWithIcon("lock", null, "Walk closer to unlock", () => ShowToast("This Landmark is outside the AR unlock radius."), "disabled-action"));
            sheet.Add(body);

            _overlayScrim.Add(sheet);
            // Added directly to _panel (a sibling of _safeRoot, which owns the bottom nav bar)
            // rather than into _panel.popupContainer - that container renders through App UI's
            // own floating-root mechanism, entirely outside this document's normal child order,
            // so the nav bar (and anything else in _safeRoot) painted over it regardless of
            // BringToFront(). Appending here instead puts the scrim after _safeRoot in the same
            // parent, which is enough on its own to paint on top.
            _panel.Add(_overlayScrim);
            SyncMapViewVisibility();
        }

        /// <summary>Floating "Find & scan this memory" panel opened from the nearby-landmark alert
        /// button (see docs/images/memory_floating_panel.png) - a compact teaser distinct from the
        /// full Landmark sheet, whose Scan action jumps straight into the existing LandmarkScan AR
        /// scanner (<see cref="UiPrototypeRuntime.EnterLandmarkScan"/>, already wired to the
        /// Journey tab's Scan action).</summary>
        void ShowNearbyMemoryPanel(string landmarkId)
        {
            var index = FindLandmarkIndex(landmarkId);
            var landmark = _data.Landmarks[index];
            var proximity = _runtime.LandmarkMapProvider.GetLandmarkProximity(landmark.id);

            var modal = ShowCenteredModal("memory-panel");
            var kicker = Row("memory-panel-kicker");
            kicker.Add(IconView("alert", "memory-panel-kicker-icon", AlertRed));
            kicker.Add(Label("NEARBY MISSION", "memory-panel-kicker-label"));
            modal.Add(kicker);
            modal.Add(Title("Find & scan this memory"));

            var row = Row("memory-panel-photo-row");
            var photoCol = Column("memory-panel-photo-col");
            photoCol.Add(Image(MissionClueImage(landmark, index), "memory-panel-photo-crop", ScaleMode.ScaleAndCrop));
            photoCol.Add(Label("Photo clue", "memory-panel-photo-caption"));
            row.Add(photoCol);

            var textCol = Column("memory-panel-text-col");
            textCol.Add(Label(landmark.name, "memory-panel-landmark-name"));
            var clue = Body("Clue: " + (string.IsNullOrWhiteSpace(landmark.missionClue)
                ? "Look closely at the photo clue and find the matching landmark image."
                : landmark.missionClue));
            clue.AddToClassList("memory-panel-clue");
            textCol.Add(clue);
            if (!string.IsNullOrWhiteSpace(landmark.missionHint))
            {
                var hint = Body("Hint: " + landmark.missionHint);
                hint.AddToClassList("memory-panel-hint");
                textCol.Add(hint);
            }
            row.Add(textCol);
            modal.Add(row);

            var distanceRow = Row("memory-panel-distance-row");
            distanceRow.Add(IconView("navigation", "memory-panel-distance-icon", AlertRed));
            distanceRow.Add(Body(proximity.distanceMetres.ToString("0") + " m away"));
            modal.Add(distanceRow);

            var tip = Row("memory-panel-tip");
            tip.Add(IconView("sparkles", "memory-panel-tip-icon", SunInk));
            tip.Add(Body("Find and scan the photo to unlock a secret reward."));
            modal.Add(tip);

            modal.Add(ActionWithIcon("camera", _assets != null ? _assets.iconAr : null, "Scan",
                () => OpenLandmarkScanner(landmark.id, true), "primary-action", "memory-panel-scan-button"));
        }

        void BuildJourneyList()
        {
            var scroll = ScreenWithHeader("Journey", "Your memories across Saigon", false);

            var latestStamp = LatestStamp();
            if (latestStamp != null)
                scroll.Add(BuildLatestStampCard(latestStamp));

            var stats = Row("journey-stats");
            stats.Add(Metric(_runtime.SaveData.journeys.Count.ToString(), "memories", "journey-stat-card"));
            stats.Add(Metric(_runtime.SaveData.stamps.Count.ToString(), "stamps", "journey-stat-card"));
            stats.Add(Metric(_runtime.SaveData.savedPhotoPaths.Count.ToString(), "photos", "journey-stat-card"));
            scroll.Add(stats);

            scroll.Add(ActionWithIcon("camera", _assets != null ? _assets.iconAr : null, "Scan",
                OpenNearestLandmarkScanner, "primary-action", "journey-scan-action"));

            scroll.Add(SectionTitle("Stamp passport"));
            var passport = Card("passport-card", "elevated-card");
            for (var i = 0; i < _data.Landmarks.Count; i++)
            {
                var landmarkIndex = i;
                var landmark = _data.Landmarks[i];
                var collected = IsStampCollected(landmark.id);
                // Only an unlocked stamp opens its memory - a locked one stays a plain,
                // non-interactive tile (nothing to show yet).
                var stamp = collected
                    ? new UiButton(() =>
                    {
                        var journeyIndex = FindLatestJourneyIndexForLandmark(landmark.id);
                        if (journeyIndex < 0) return;
                        _runtime.SelectedJourneyIndex = journeyIndex;
                        Navigate(UiRoute.JourneyDetail);
                    }) { name = "passport-stamp-" + landmark.id }
                    : Element(null);
                stamp.AddToClassList("passport-stamp");
                if (collected) stamp.AddToClassList("passport-stamp-collected");
                if (collected)
                    stamp.Add(Image(_assets != null ? _assets.Landmark(landmarkIndex) : null, "passport-stamp-image", ScaleMode.ScaleAndCrop));
                else
                    stamp.Add(IconView("lock", "passport-stamp-icon", MutedInk));
                stamp.Add(Label(landmark.name, "passport-stamp-label"));
                passport.Add(stamp);
            }
            scroll.Add(passport);

            scroll.Add(SectionTitle("Photos"));
            scroll.Add(Body("AR photos with your companions"));
            if (_runtime.SaveData.savedPhotoPaths.Count == 0)
            {
                var emptyPhotos = Card("journey-empty-card");
                emptyPhotos.Add(IconView("gallery", "journey-empty-icon", Primary, _assets != null ? _assets.iconJourney : null));
                emptyPhotos.Add(Subtitle("No photos yet"));
                emptyPhotos.Add(Body("Take an AR photo with a companion to see it here."));
                scroll.Add(emptyPhotos);
            }
            else
            {
                var photoGrid = Element("journey-photo-grid", "journey-photo-grid");
                for (var i = _runtime.SaveData.savedPhotoPaths.Count - 1; i >= 0; i--)
                {
                    var index = i;
                    var thumb = new UiButton(() =>
                    {
                        // Landmark story or companion "View in AR", depending on which kind of
                        // journey entry this photo belongs to - falls back to the standalone
                        // viewer only for a photo with no journey entry pointing at it, which the
                        // normal capture flow shouldn't produce.
                        var journeyIndex = FindJourneyIndexForPhotoPath(_runtime.SaveData.savedPhotoPaths[index]);
                        if (journeyIndex >= 0)
                        {
                            _runtime.SelectedJourneyIndex = journeyIndex;
                            Navigate(UiRoute.JourneyDetail);
                        }
                        else
                        {
                            _viewerPhotoIndex = index;
                            ShowPhotoViewer();
                        }
                    }) { name = "journey-photo-" + index };
                    thumb.AddToClassList("journey-photo-thumb");
                    thumb.Add(Image(LoadPhoto(_runtime.SaveData.savedPhotoPaths[index]), "journey-photo-thumb-image", ScaleMode.ScaleAndCrop));
                    photoGrid.Add(thumb);
                }
                scroll.Add(photoGrid);
            }
        }

        VisualElement BuildLatestStampCard(StampData stamp)
        {
            var landmarkIndex = FindLandmarkIndex(stamp.landmarkId);
            var landmark = _data.Landmarks[landmarkIndex];
            var journeyIndex = FindLatestJourneyIndexForLandmark(stamp.landmarkId);
            var card = new UiButton(() =>
            {
                if (journeyIndex < 0) return;
                _runtime.SelectedJourneyIndex = journeyIndex;
                Navigate(UiRoute.JourneyDetail);
            }) { name = "latest-landmark-stamp" };
            card.AddToClassList("latest-stamp-card");
            card.AddToClassList("elevated-card");

            var seal = Element(null, "latest-stamp-seal");
            seal.Add(Image(_assets != null ? _assets.Landmark(landmarkIndex) : null,
                "latest-stamp-image", ScaleMode.ScaleAndCrop));
            card.Add(seal);

            var copy = Column("latest-stamp-copy");
            copy.Add(Eyebrow("LATEST LANDMARK STAMP"));
            copy.Add(Title(landmark.name));
            copy.Add(Body("Discovered " + StampDateLabel(stamp.collectedUtc)));
            if (!string.IsNullOrEmpty(landmark.companionRewardId))
                copy.Add(Pill("Companion reward · " + CompanionName(landmark.companionRewardId), "latest-stamp-reward"));

            if (journeyIndex >= 0 && !string.IsNullOrEmpty(_runtime.SaveData.journeys[journeyIndex].photoPath))
                copy.Add(Pill("Photo attached", "latest-stamp-photo-state"));
            else
                copy.Add(Body("Photo optional · Tap to view this memory"));
            card.Add(copy);
            return card;
        }

        /// <summary>Full-screen photo viewer (design doc "Photos" gallery) with a filmstrip to switch
        /// between every saved AR photo, and a delete action.</summary>
        void ShowPhotoViewer()
        {
            RemoveTransientOverlay();
            _overlayScrim = Element("journey-photo-viewer-scrim", "journey-photo-viewer");
            RenderPhotoViewerContent();
            // Added directly to _panel (a sibling of _safeRoot, which owns the bottom nav bar)
            // rather than into _panel.popupContainer - that container renders through App UI's
            // own floating-root mechanism, entirely outside this document's normal child order,
            // so the nav bar (and anything else in _safeRoot) painted over it regardless of
            // BringToFront(). Appending here instead puts the scrim after _safeRoot in the same
            // parent, which is enough on its own to paint on top.
            _panel.Add(_overlayScrim);
            SyncMapViewVisibility();
        }

        void RenderPhotoViewerContent()
        {
            _overlayScrim.Clear();
            var photos = _runtime.SaveData.savedPhotoPaths;
            if (photos.Count == 0) { RemoveTransientOverlay(); return; }
            _viewerPhotoIndex = Mathf.Clamp(_viewerPhotoIndex, 0, photos.Count - 1);
            var path = photos[_viewerPhotoIndex];

            var header = Row("journey-photo-viewer-header");
            header.Add(IconAction("x", _assets != null ? _assets.iconClose : null, "X", RemoveTransientOverlay, "journey-photo-viewer-close", "small-round-control", "dark-round-control"));
            header.Add(Subtitle("Photo " + (_viewerPhotoIndex + 1) + " of " + photos.Count));
            _overlayScrim.Add(header);

            _overlayScrim.Add(Image(LoadPhoto(path), "journey-photo-viewer-image", ScaleMode.ScaleToFit));

            _overlayScrim.Add(Action("Delete photo", () =>
            {
                _runtime.DeletePhoto(path);
                // Refreshes the Photos grid/stats and Memory timeline underneath the still-open
                // viewer immediately - previously they only picked up the deletion on the next full
                // Render() (e.g. a tab switch), and the timeline never picked it up at all since the
                // old code only ever touched savedPhotoPaths, never the Journey entry referencing it.
                Render();
                if (photos.Count == 0) RemoveTransientOverlay();
                else RenderPhotoViewerContent();
            }, "danger-action", "journey-photo-viewer-delete"));

            var strip = Element(null, "journey-photo-viewer-strip");
            for (var i = 0; i < photos.Count; i++)
            {
                var index = i;
                var thumb = new UiButton(() => { _viewerPhotoIndex = index; RenderPhotoViewerContent(); }) { name = "viewer-thumb-" + i };
                thumb.AddToClassList("journey-photo-viewer-strip-item");
                if (index == _viewerPhotoIndex) thumb.AddToClassList("journey-photo-viewer-strip-item-selected");
                thumb.Add(Image(LoadPhoto(photos[i]), "journey-photo-viewer-strip-image", ScaleMode.ScaleAndCrop));
                strip.Add(thumb);
            }
            _overlayScrim.Add(strip);
        }

        void BuildJourneyDetail()
        {
            if (_runtime.SaveData.journeys.Count == 0) { BuildJourneyList(); return; }
            var index = Mathf.Clamp(_runtime.SelectedJourneyIndex, 0, _runtime.SaveData.journeys.Count - 1);
            var journey = _runtime.SaveData.journeys[index];
            var scroll = ScreenWithHeader(JourneyDisplayTitle(journey), DateLabel(journey.createdUtc), true);
            var photo = Card("journey-photo-frame", "elevated-card");
            photo.Add(Image(JourneyImage(journey), "journey-detail-image"));
            photo.Add(Label(string.IsNullOrEmpty(journey.photoPath) ? "Landmark Stamp" : "Photo Memory", "journey-photo-caption"));
            scroll.Add(photo);
            var note = Card("scrapbook-card");
            note.Add(Eyebrow("LOCAL JOURNEY RECORD"));
            note.Add(Title(journey.summary));
            LandmarkUiData landmarkMemory = null;
            if (!string.IsNullOrEmpty(journey.landmarkId))
            {
                note.Add(InfoRow("map-pin", "Landmark", LandmarkName(journey.landmarkId)));
                note.Add(InfoRow("calendar", "Discovered", StampDateLabel(journey.createdUtc)));
                landmarkMemory = _data.Landmarks[FindLandmarkIndex(journey.landmarkId)];
                if (!string.IsNullOrEmpty(landmarkMemory.companionRewardId))
                    note.Add(InfoRow("paw-print", "Companion reward", CompanionName(landmarkMemory.companionRewardId)));
                note.Add(InfoRow("footprints", "Distance", journey.distanceKilometres.ToString("0.00") + " km"));
            }
            else if (!string.IsNullOrEmpty(journey.companionId))
                note.Add(InfoRow("paw-print", "Companion", CompanionName(journey.companionId)));
            scroll.Add(note);
            if (landmarkMemory != null)
            {
                scroll.Add(SectionTitle("Landmark Story"));
                scroll.Add(StorySection("History", landmarkMemory.history, "history-card", "book-heart"));
                scroll.Add(StorySection("Cultural Significance", landmarkMemory.architecture, "architecture-card", "map"));
                scroll.Add(StorySection("Did you know?", landmarkMemory.didYouKnow, "fact-card", "sparkles"));
                scroll.Add(ActionWithIcon("camera", _assets != null ? _assets.iconAr : null, "Scan Again",
                    () => OpenLandmarkScannerAgain(journey.landmarkId), "primary-action", "journey-scan-again-action"));
            }
            else if (!string.IsNullOrEmpty(journey.companionId))
                scroll.Add(ActionWithIcon("camera", _assets != null ? _assets.iconAr : null, "View in AR", () => _runtime.EnterPetAr(journey.companionId, false), "blossom-action"));
        }

        void BuildActivityDashboard()
        {
            var page = Page("content-page", false);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "screen-scroll" };
            scroll.AddToClassList("screen-scroll");
            page.Add(scroll);

            var header = Row("activity-dashboard-header");
            var iconWell = Element(null, "activity-dashboard-icon-well");
            iconWell.Add(IconView("hub", "icon-image", ActivityDeepGreen));
            header.Add(iconWell);
            var headerCopy = Column("screen-header-copy");
            headerCopy.Add(Title("Activity Records"));
            headerCopy.Add(Body(CompanionProgressionService.LocalNow(DateTime.UtcNow).ToString("ddd, MMM d")));
            header.Add(headerCopy);
            header.Add(IconAction("x", _assets != null ? _assets.iconClose : null, "X", () => HandleBack(), "activity-dashboard-close", "small-round-control"));
            scroll.Add(header);

            var weekTotal = _runtime.GetActivityPeriod(ActivityPeriod.Week, 0);
            var yearTotal = _runtime.GetActivityPeriod(ActivityPeriod.Year, 0);
            var totalsRow = Row("activity-totals-row");
            var weekCard = Column("activity-total-card");
            weekCard.Add(Eyebrow("THIS WEEK"));
            weekCard.Add(Label(weekTotal.totalKilometres.ToString("0.0") + " km", "activity-total-value"));
            totalsRow.Add(weekCard);
            var yearCard = Column("activity-total-card");
            yearCard.Add(Eyebrow("THIS YEAR"));
            var yearValue = Label(yearTotal.totalKilometres.ToString("0.0") + " km", "activity-total-value");
            yearValue.AddToClassList("activity-total-value-year");
            yearCard.Add(yearValue);
            totalsRow.Add(yearCard);
            scroll.Add(totalsRow);

            var record = Card("activity-record-card", "elevated-card");

            var tabs = Row("activity-period-tabs");
            foreach (var period in PeriodTabs)
            {
                var capturedPeriod = period;
                var tab = new UiButton(() => { _activityPeriod = capturedPeriod; _activityOffset = 0; Render(); }) { name = "activity-period-" + period.ToString().ToLowerInvariant() };
                tab.AddToClassList("activity-period-tab");
                if (_activityPeriod == period) tab.AddToClassList("activity-period-tab-selected");
                tab.Add(Label(period.ToString(), "activity-period-tab-label"));
                tabs.Add(tab);
            }
            record.Add(tabs);

            var data = _runtime.GetActivityPeriod(_activityPeriod, _activityOffset);

            var nav = Row("activity-period-nav");
            nav.Add(IconAction("chevron-left", null, "<", () => { _activityOffset -= 1; Render(); }, "activity-period-prev", "small-round-control"));
            nav.Add(Label(data.periodLabel, "activity-period-label"));
            var nextButton = IconAction("chevron-right", null, ">", () => { if (data.canGoNext) { _activityOffset += 1; Render(); } }, "activity-period-next", "small-round-control");
            if (!data.canGoNext) nextButton.AddToClassList("activity-period-next-disabled");
            nav.Add(nextButton);
            record.Add(nav);

            var ring = new ActivityRing(DailyGoalRatio(data.totalKilometres, data.targetKilometres));
            ring.name = "daily-activity-ring";
            ring.Add(IconView("footprints", "activity-ring-icon", Primary));
            ring.Add(Label(data.hasSteps ? data.totalSteps.ToString("N0") : data.totalKilometres.ToString("0.0"), "activity-ring-value"));
            ring.Add(Label(data.hasSteps ? "STEPS" : "KILOMETRES", "activity-ring-label"));
            ring.Add(Pill(data.totalKilometres.ToString("0.0") + " / " + data.targetKilometres.ToString("0") + " km", "activity-goal-pill"));
            record.Add(ring);

            var refRow = Row("activity-chart-reference-row");
            refRow.Add(Label(data.referenceLabel, "activity-chart-reference-label"));
            record.Add(refRow);

            var chart = Element("weekly-activity-chart", "weekly-chart");
            var perBarTarget = data.bars.Length > 0 ? data.targetKilometres / data.bars.Length : 0f;
            foreach (var bar in data.bars)
            {
                var column = Element(null, "weekly-chart-column");
                column.style.width = Length.Percent(100f / data.bars.Length);
                var barTrack = Element(null, "weekly-chart-bar-track");
                var barFill = Element(null, "weekly-chart-bar-fill");
                if (bar.isFuture) barFill.AddToClassList("weekly-chart-bar-fill-future");
                else if (bar.isCurrent) barFill.AddToClassList("weekly-chart-bar-fill-today");
                var ratio = DailyGoalRatio(bar.distanceKilometres, perBarTarget);
                barFill.style.height = Length.Percent(bar.isFuture ? 0f : Mathf.Max(ratio * 100f, bar.distanceKilometres > 0 ? 5f : 1.5f));
                barTrack.Add(barFill);
                column.Add(barTrack);
                var topLabel = Label(bar.topLabel, "weekly-chart-day-label");
                if (data.bars.Length > 7) topLabel.AddToClassList("weekly-chart-day-label-compact");
                if (bar.isCurrent) topLabel.AddToClassList("weekly-chart-today-label");
                column.Add(topLabel);
                if (!string.IsNullOrEmpty(bar.subLabel))
                {
                    var subLabel = Label(bar.subLabel, "weekly-chart-date-label");
                    if (bar.isCurrent) subLabel.AddToClassList("weekly-chart-today-label");
                    column.Add(subLabel);
                }
                chart.Add(column);
            }
            record.Add(chart);

            var average = Pill(data.averageLabel + "  " + data.averageKilometres.ToString("0.0") + " km", "average-pill");
            record.Add(average);
            scroll.Add(record);
        }

        void BeginArPhotoPick() => _runtime.EnterPetAr(_runtime.PrimaryCompanionId(), true);

        void OpenMarker(MapMarkerUiData marker)
        {
            if (marker.type == MapMarkerType.Player) { ShowToast(CompanionName(_runtime.PrimaryCompanionId()) + " is walking with you."); return; }
            ShowLandmarkSheet(marker.targetId);
        }

        /// <summary>Opens a bottom sheet (scrim + slide-up card) into the popup container - the
        /// pattern originated by the old ShowFoodPicker, generalized for reuse by the Landmark
        /// sheet, Feed sheet, and Account panel. Add content to the returned element; closing any
        /// transient overlay (<see cref="RemoveTransientOverlay"/>) dismisses it.</summary>
        VisualElement ShowSheet(string sheetName)
        {
            RemoveTransientOverlay();
            _overlayScrim = Element(sheetName + "-scrim", "tray-scrim");
            var sheet = Card(sheetName, "discovery-tray", "elevated-card");
            sheet.Add(Element("sheet-handle", "sheet-handle"));
            _overlayScrim.Add(sheet);
            // Added directly to _panel (a sibling of _safeRoot, which owns the bottom nav bar)
            // rather than into _panel.popupContainer - that container renders through App UI's
            // own floating-root mechanism, entirely outside this document's normal child order,
            // so the nav bar (and anything else in _safeRoot) painted over it regardless of
            // BringToFront(). Appending here instead puts the scrim after _safeRoot in the same
            // parent, which is enough on its own to paint on top.
            _panel.Add(_overlayScrim);
            SyncMapViewVisibility();
            return sheet;
        }

        /// <summary>Opens a centered floating modal (scrim + card) into the popup container - used
        /// by the Shop's Pet Detail card. Add content to the returned element.</summary>
        VisualElement ShowCenteredModal(string modalName, bool withCloseButton = true)
        {
            RemoveTransientOverlay();
            _overlayScrim = Element(modalName + "-scrim", "overlay-scrim");
            var modal = Card(modalName, "elevated-card", "modal-card");
            if (withCloseButton)
                modal.Add(IconAction("x", _assets != null ? _assets.iconClose : null, "X", RemoveTransientOverlay, "modal-close", "small-round-control"));
            _overlayScrim.Add(modal);
            // Added directly to _panel (a sibling of _safeRoot, which owns the bottom nav bar)
            // rather than into _panel.popupContainer - that container renders through App UI's
            // own floating-root mechanism, entirely outside this document's normal child order,
            // so the nav bar (and anything else in _safeRoot) painted over it regardless of
            // BringToFront(). Appending here instead puts the scrim after _safeRoot in the same
            // parent, which is enough on its own to paint on top.
            _panel.Add(_overlayScrim);
            SyncMapViewVisibility();
            return modal;
        }

        /// <summary>Account panel (avatar, editable display name, inline two-step progress reset) -
        /// opened from the top-bar profile button. Replaces the old centered Settings overlay as the
        /// primary entry point; that overlay's Settings/Permissions/Confirmation content is still
        /// reachable through <see cref="ShowOverlay"/> for anything not covered here.</summary>
        void ShowAccountPanel()
        {
            _accountResetConfirming = false;
            _accountNameEditing = false;
            _pendingAccountDisplayName = _runtime.SaveData.displayName;
            RenderAccountPanel();
        }

        void RenderAccountPanel()
        {
            var modal = ShowCenteredModal("account-panel");
            modal.Add(Eyebrow("ACCOUNT"));

            var avatar = Element(null, "account-avatar");
            var initial = string.IsNullOrWhiteSpace(_runtime.SaveData.displayName) ? "B" : _runtime.SaveData.displayName.Substring(0, 1).ToUpperInvariant();
            avatar.Add(Label(initial, "account-avatar-initial"));
            modal.Add(avatar);

            var nameRow = Row("account-name-row");
            var nameField = new UiTextField { name = "account-name-field", value = _pendingAccountDisplayName, maxLength = 20, isReadOnly = !_accountNameEditing };
            nameField.AddToClassList("account-name-field");
            if (!_accountNameEditing) nameField.AddToClassList("account-name-field-readonly");
            nameField.RegisterValueChangedCallback(evt => { if (_accountNameEditing) _pendingAccountDisplayName = evt.newValue; });
            nameRow.Add(nameField);
            nameRow.Add(IconAction(_accountNameEditing ? "check" : "pencil", null, _accountNameEditing ? "SAVE" : "EDIT",
                _accountNameEditing ? (Action)CommitAccountNameEdit : BeginAccountNameEdit,
                "account-name-edit-button", "small-round-control", _accountNameEditing ? "account-name-save-button" : "account-name-pencil-button",
                _accountNameEditing ? "dark-round-control" : null));
            modal.Add(nameRow);
            if (_accountNameEditing) nameField.schedule.Execute(nameField.Focus).StartingIn(1);
            modal.Add(Divider());

            if (!_accountResetConfirming)
            {
                modal.Add(Action("Restart All Progress", () => { _accountResetConfirming = true; RenderAccountPanel(); }, "danger-action"));
            }
            else
            {
                modal.Add(Body("This resets coins, companions, and journey. Are you sure?"));
                var confirmRow = Row();
                confirmRow.Add(Action("Cancel", () => { _accountResetConfirming = false; RenderAccountPanel(); }, "secondary-action", "half-action"));
                confirmRow.Add(Action("Reset", () => { RemoveTransientOverlay(); ConfirmResetLocalProgress(); }, "danger-action", "half-action"));
                modal.Add(confirmRow);
            }
        }

        void BeginAccountNameEdit()
        {
            _accountNameEditing = true;
            _pendingAccountDisplayName = _runtime.SaveData.displayName;
            RenderAccountPanel();
        }

        void CommitAccountNameEdit()
        {
            var normalized = PlayerSaveData.NormalizeDisplayName(_pendingAccountDisplayName);
            if (!PlayerSaveData.IsValidDisplayName(normalized))
            {
                ShowToast("Enter 1 to 20 characters.");
                return;
            }

            _runtime.SaveData.displayName = normalized;
            _runtime.SaveData.ApplyAdminPerksIfNamed();
            _runtime.Persist();
            _pendingAccountDisplayName = normalized;
            _accountNameEditing = false;
            var profileInitial = _safeRoot.Q<Label>(className: "profile-initial");
            if (profileInitial != null) profileInitial.text = normalized.Substring(0, 1).ToUpperInvariant();
            RenderAccountPanel();
        }


        void ShowFoodPicker(FoodUiData food)
        {
            var tray = ShowSheet("food-picker-tray");
            tray.Add(Eyebrow("CHOOSE A COMPANION"));
            tray.Add(Title("Who gets the " + food.name + "?"));
            var choices = Element(null, "food-companion-grid");
            var choiceCount = 0;
            for (var i = 0; i < _data.Companions.Count; i++)
            {
                var definition = _data.Companions[i];
                var progress = _runtime.Companion(definition.id);
                if (progress == null || !progress.unlocked) continue;
                choiceCount++;
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
            PadGridRow(choices, choiceCount, 3, "food-companion-choice");
            tray.Add(choices);
            tray.Add(Action("Cancel", RemoveTransientOverlay, "secondary-action"));
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
            // Added directly to _panel (a sibling of _safeRoot, which owns the bottom nav bar)
            // rather than into _panel.popupContainer - that container renders through App UI's
            // own floating-root mechanism, entirely outside this document's normal child order,
            // so the nav bar (and anything else in _safeRoot) painted over it regardless of
            // BringToFront(). Appending here instead puts the scrim after _safeRoot in the same
            // parent, which is enough on its own to paint on top.
            _panel.Add(_overlayScrim);
        }

        void RemoveTransientOverlay()
        {
            if (_overlayScrim == null) return;
            _overlayScrim.RemoveFromHierarchy();
            _overlayScrim = null;
            SyncMapViewVisibility();
        }

        ScrollView ScreenWithHeader(string title, string subtitle, bool showBack,
            string actionIcon = null, string actionLabel = null, Action action = null, string actionClass = null,
            bool largeAction = false, string headerIcon = null)
        {
            var page = Page("content-page", true);
            page.Add(BuildTopStatusBar(false));
            var header = Row("screen-header");
            if (showBack) header.Add(IconAction("arrow-left", _assets != null ? _assets.iconBack : null, "BACK", () => HandleBack(), "back-button", "small-round-control"));
            if (!string.IsNullOrEmpty(headerIcon))
            {
                var iconWell = Element(null, "screen-header-icon-well");
                iconWell.Add(IconView(headerIcon, "screen-header-icon", Primary,
                    headerIcon == "map-pin" && _assets != null ? _assets.iconLocation : null));
                header.Add(iconWell);
            }
            var copy = Column("screen-header-copy");
            copy.Add(Title(title));
            copy.Add(Body(subtitle));
            header.Add(copy);
            if (!string.IsNullOrEmpty(actionLabel))
            {
                var chip = ActionWithIcon(actionIcon, actionIcon == "coins" ? null : _assets != null ? _assets.iconCamera : null,
                    actionLabel, action ?? (() => { }), actionClass ?? "secondary-action", largeAction ? "header-chip-large" : "header-chip");
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
            metrics.Add(StatusPill("footprints", _assets != null ? _assets.iconSteps : null, _runtime.SaveData.totalDistanceKilometres.ToString("0.0") + " km", "distance-status-pill", null, null));
            bar.Add(metrics);

            var rightGroup = Row("top-status-right-group");
            if (IsMapRoute()) rightGroup.Add(BuildNearbyLandmarkAlertButton());
            var activity = new UiButton(() => Navigate(UiRoute.ActivityDashboard)) { name = "activity-dashboard-button" };
            activity.AddToClassList("icon-button");
            activity.AddToClassList("top-hub-button");
            activity.Add(IconView("hub", "icon-image", White));
            rightGroup.Add(activity);

            var profile = new UiButton(ShowAccountPanel) { name = "settings-button" };
            profile.AddToClassList("profile-button");
            var initial = string.IsNullOrWhiteSpace(_runtime.SaveData.displayName) ? "B" : _runtime.SaveData.displayName.Substring(0, 1).ToUpperInvariant();
            profile.Add(Label(initial, "profile-initial"));
            rightGroup.Add(profile);
            bar.Add(rightGroup);
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
            var photo = LoadPhoto(journey.photoPath);
            if (photo != null) return photo;
            if (!string.IsNullOrEmpty(journey.landmarkId) && _assets != null)
                return _assets.Landmark(FindLandmarkIndex(journey.landmarkId));
            return _assets != null ? _assets.journeyOne : null;
        }

        Texture2D MissionClueImage(LandmarkUiData landmark, int fallbackIndex)
        {
            var clue = landmark == null || string.IsNullOrEmpty(landmark.id)
                ? null
                : Resources.Load<Texture2D>("UI/MissionClues/" + landmark.id);
            return clue != null ? clue : _assets != null ? _assets.Landmark(fallbackIndex) : null;
        }

        void OpenLandmarkScanner(string landmarkId, bool closeOverlay)
        {
            if (!_runtime.CanEnterLandmarkScan(landmarkId))
            {
                ShowToast("Move within 500 m of this Landmark to scan it.");
                return;
            }
            if (closeOverlay) RemoveTransientOverlay();
            _runtime.EnterLandmarkScan(landmarkId);
        }

        void OpenNearestLandmarkScanner()
        {
            if (!_runtime.TryEnterNearestLandmarkScan())
                ShowToast("Move within 500 m of a supported Landmark to scan it.");
        }

        void OpenLandmarkScannerAgain(string landmarkId)
        {
            if (!_runtime.TryEnterLandmarkScanAgain(landmarkId))
                ShowToast("This Landmark scan is not available.");
        }

        /// <summary>Loads and caches a photo from an on-disk path (a saved AR photo). Returns null if
        /// the path is empty or the file is missing/unreadable - callers decide their own fallback.</summary>
        Texture2D LoadPhoto(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_journeyPhotoCache.TryGetValue(path, out var cached) && cached != null) return cached;
            if (!File.Exists(path)) return null;
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(File.ReadAllBytes(path))) return null;
            _journeyPhotoCache[path] = texture;
            return texture;
        }

        bool IsStampCollected(string landmarkId)
        {
            foreach (var stamp in _runtime.SaveData.stamps)
                if (stamp != null && stamp.landmarkId == landmarkId) return true;
            return false;
        }

        StampData LatestStamp()
        {
            for (var i = _runtime.SaveData.stamps.Count - 1; i >= 0; i--)
                if (_runtime.SaveData.stamps[i] != null) return _runtime.SaveData.stamps[i];
            return null;
        }

        int FindLatestJourneyIndexForLandmark(string landmarkId)
        {
            for (var i = _runtime.SaveData.journeys.Count - 1; i >= 0; i--)
                if (_runtime.SaveData.journeys[i] != null && _runtime.SaveData.journeys[i].landmarkId == landmarkId)
                    return i;
            return -1;
        }

        int FindJourneyIndexForPhotoPath(string photoPath)
        {
            for (var i = _runtime.SaveData.journeys.Count - 1; i >= 0; i--)
                if (_runtime.SaveData.journeys[i] != null && _runtime.SaveData.journeys[i].photoPath == photoPath)
                    return i;
            return -1;
        }

        int UnlockedCompanionCount()
        {
            var count = 0;
            for (var i = 0; i < _data.Companions.Count; i++) if (IsUnlocked(i)) count++;
            return count;
        }

        int OwnedCompanionCount()
        {
            var count = 0;
            for (var i = 0; i < _data.Companions.Count; i++) if (IsOwned(i)) count++;
            return count;
        }

        bool IsUnlocked(int index)
        {
            var progress = _runtime.Companion(_data.Companions[index].id);
            return progress != null && progress.unlocked;
        }

        /// <summary>Distance-unlocked AND purchased/granted - the companion is actually in the
        /// player's collection (design doc "Owned" state).</summary>
        bool IsOwned(int index)
        {
            var progress = _runtime.Companion(_data.Companions[index].id);
            return progress != null && progress.owned;
        }

        int FirstOwnedCompanionIndex()
        {
            for (var i = 0; i < _data.Companions.Count; i++) if (IsOwned(i)) return i;
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
        string JourneyDisplayTitle(JourneyEntryData journey) => journey != null && !string.IsNullOrEmpty(journey.landmarkId)
            ? LandmarkName(journey.landmarkId)
            : journey?.title ?? string.Empty;
        string CompanionName(string id) { foreach (var item in _data.Companions) if (item.id == id) return item.name; return id; }
        static string StageLine(CompanionProgressData progress)
        {
            var entry = CompanionRoster.Find(progress.companionId);
            return CompanionProgressionService.StageFor(entry, progress.growthExperience) + " · " + progress.growthExperience + " EXP";
        }
        static string DateLabel(string utc) => DateTime.TryParse(utc, out var value) ? value.ToLocalTime().ToString("d MMM yyyy") : "Saved locally";
        static string StampDateLabel(string utc) => DateTime.TryParse(utc, out var value) ? value.ToLocalTime().ToString("d MMM yyyy · HH:mm") : "Saved locally";
        static float DailyGoalRatio(float distanceKilometres, float goalKilometres) => goalKilometres > 0f ? Mathf.Clamp01(distanceKilometres / goalKilometres) : 0f;

        static float GrowthRatio(CompanionRoster.Entry entry, int experience, GrowthStage stage) => stage switch
        {
            GrowthStage.Baby => entry.YoungExp > 0 ? Mathf.Clamp01((float)experience / entry.YoungExp) : 0f,
            GrowthStage.Young => entry.AdultExp > entry.YoungExp ? Mathf.Clamp01((float)(experience - entry.YoungExp) / (entry.AdultExp - entry.YoungExp)) : 0f,
            _ => 1f
        };

        static string GrowthCaption(CompanionRoster.Entry entry, int experience, GrowthStage stage) => stage switch
        {
            GrowthStage.Baby => experience + " / " + entry.YoungExp + " EXP",
            // Matches GrowthRatio above: EXP within the current stage, not the raw cumulative
            // total against the final threshold, so the number resets alongside the bar instead
            // of still reading e.g. "50 / 80" right after leveling up into Young.
            GrowthStage.Young => (experience - entry.YoungExp) + " / " + (entry.AdultExp - entry.YoungExp) + " EXP",
            _ => "Max"
        };

        static string NextStageLabel(GrowthStage stage) => stage == GrowthStage.Baby ? "Next: Young" : stage == GrowthStage.Young ? "Next: Adult" : "Fully grown";

        void ShowToast(string message)
        {
            _panel.notificationContainer.Clear();
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

        const int AutoScrollHoldMs = 1400;
        const int AutoScrollMoveMs = 900;

        /// <summary>The featured companion card's description box (.featured-description-clip in
        /// ARWalking.uss) has a fixed height so the hero card's size stays consistent across
        /// companions, but trivia sentences vary in length. When one overflows the box, this scrolls
        /// it up and down on a loop instead of clipping it outright.</summary>
        static VisualElement BuildAutoScrollingDescription(string text)
        {
            var clip = Element(null, "featured-description-clip");
            var label = Body(text); // keeps .body's colour/font; .featured-description-text only repositions/resizes it
            label.AddToClassList("featured-description-text");
            clip.Add(label);
            var started = false;
            clip.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (started) return;
                started = true;
                BeginAutoScrollIfNeeded(clip, label);
            });
            return clip;
        }

        static void BeginAutoScrollIfNeeded(VisualElement clip, Label label)
        {
            var overflow = label.resolvedStyle.height - clip.resolvedStyle.height;
            if (overflow <= 1f) return; // short trivia already fits - nothing to scroll
            label.schedule.Execute(() => AutoScrollTo(label, overflow, true)).StartingIn(AutoScrollHoldMs);
        }

        static void AutoScrollTo(Label label, float overflow, bool scrollDown)
        {
            var target = scrollDown ? -overflow : 0f;
            label.experimental.animation
                .Start(label.resolvedStyle.top, target, AutoScrollMoveMs, (element, value) => element.style.top = value)
                .OnCompleted(() => label.schedule.Execute(() => AutoScrollTo(label, overflow, !scrollDown)).StartingIn(AutoScrollHoldMs));
        }

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

        /// <summary>Collapses a long history/architecture paragraph down to its first sentence, for
        /// compact spots (like the nearby-memory panel) that only have room for a one-line blurb.</summary>
        static string FirstSentence(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var cut = text.IndexOfAny(new[] { '.', '!', '?' });
            return cut < 0 ? text.Trim() : text.Substring(0, cut + 1).Trim();
        }

        void ApplySafeArea()
        {
            if (_safeRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
            var safe = UiSafeAreaSimulation.Resolve(Screen.safeArea);
            var panelHeight = _document.rootVisualElement.resolvedStyle.height;
            var scale = float.IsNaN(panelHeight) || panelHeight <= 0f ? 1f : panelHeight / Screen.height;
            _safeRoot.style.paddingLeft = safe.xMin * scale;
            _safeRoot.style.paddingRight = (Screen.width - safe.xMax) * scale;
            _safeRoot.style.paddingTop = (Screen.height - safe.yMax) * scale;
            _safeRoot.style.paddingBottom = Mathf.Max(safe.yMin, _lastKeyboardInsetPixels) * scale;
            _lastSafeArea = safe;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }

        // Driven by the display-name field's own focus state (see BuildOnboarding), not just
        // TouchScreenKeyboard.visible - on some Android/iOS builds TouchScreenKeyboard.area reports 0 or a
        // stale value while the on-screen keyboard is still animating in, so a height-only check can leave
        // the field shifted by nothing even though the keyboard is about to cover it. Falling back to a
        // generous fixed estimate whenever the field is focused guarantees the field always clears the
        // keyboard, and real area data (once it becomes available) still wins for a tighter fit.
        float CurrentKeyboardInsetPixels()
        {
            if (_runtime == null || _runtime.Navigator == null || _runtime.Navigator.CurrentRoute != UiRoute.OnboardingSetup || _setupStep != 1 || !_displayNameFieldFocused)
                return 0f;
            var reportedHeight = TouchScreenKeyboard.visible ? Mathf.Max(0f, TouchScreenKeyboard.area.height) : 0f;
            return reportedHeight > 0f ? reportedHeight : Screen.height * 0.42f;
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
