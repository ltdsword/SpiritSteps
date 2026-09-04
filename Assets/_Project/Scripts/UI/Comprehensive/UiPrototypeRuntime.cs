using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARWalking.UI
{
    [DisallowMultipleComponent]
    public sealed class UiPrototypeRuntime : MonoBehaviour
    {
        public static UiPrototypeRuntime Instance { get; private set; }
        public static string TestSavePathOverride { get; set; }
        public static IWalkMetricsProvider TestWalkProviderOverride { get; set; }
        public static ILandmarkMapProvider TestMapProviderOverride { get; set; }
        public static IWebViewBridge TestWebViewBridgeOverride { get; set; }

        public UiNavigationStack Navigator { get; private set; }
        public IUiDataProvider Data { get; private set; }
        public IMapDataProvider MapData { get; private set; }
        public PrototypeUiAssets Assets { get; private set; }
        public PlayerSaveData SaveData { get; private set; }
        public SaveLoadResult InitialLoadResult { get; private set; }
        public IWalkMetricsProvider WalkProvider { get; private set; }
        public ILandmarkMapProvider LandmarkMapProvider { get; private set; }
        public WebViewMapView MapView { get; private set; }
        public LandmarkGeoCatalog GeoCatalog { get; private set; }
        public DeviceLocationService LocationService => SharedLocationService;
        public WalkResultDto LastWalkResult { get; private set; }
        public LandmarkRewardDto LastLandmarkReward { get; private set; }
        public int SelectedCompanionIndex { get; set; }
        public int SelectedLandmarkIndex { get; set; } = 1;
        public int SelectedJourneyIndex { get; set; }
        public bool HasProfile => SaveData != null && SaveData.setupComplete;

        LocalPlayerSaveStore _saveStore;
        CompanionProgressionService _progression;
        List<string> _walkEligibleCompanionIds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeBeforeScene() => EnsureExists();

        public static UiPrototypeRuntime EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<UiPrototypeRuntime>();
            if (existing != null) return existing;
            return new GameObject("UiPrototypeRuntime").AddComponent<UiPrototypeRuntime>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Navigator = new UiNavigationStack();
            var catalog = Resources.Load<PrototypeUiCatalog>("UI/PrototypeUiCatalog");
            Assets = Resources.Load<PrototypeUiAssets>("UI/PrototypeUiAssets");
            if (catalog == null)
            {
                Debug.LogError("PrototypeUiCatalog is missing from Resources/UI.");
                enabled = false;
                return;
            }
            Data = new StaticUiDataProvider(catalog);
            MapData = new StaticMapDataProvider(catalog);
            WalkProvider = TestWalkProviderOverride ?? BuildRealWalkProvider();
            LandmarkMapProvider = TestMapProviderOverride ?? BuildRealMapProvider(catalog);
            GeoCatalog = Resources.Load<LandmarkGeoCatalog>("UI/LandmarkGeoCatalog");
            InitializeMapView();
            _saveStore = new LocalPlayerSaveStore(TestSavePathOverride);
            InitialLoadResult = _saveStore.Load();
            SaveData = InitialLoadResult.save;
            if (SaveData != null) _progression = new CompanionProgressionService(SaveData);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        DeviceLocationService _sharedLocationService;
        DeviceLocationService SharedLocationService => _sharedLocationService ??= gameObject.AddComponent<DeviceLocationService>();

        IWalkMetricsProvider BuildRealWalkProvider() =>
            new RealWalkMetricsProvider(SharedLocationService, gameObject.AddComponent<DeviceStepCounterService>());

        ILandmarkMapProvider BuildRealMapProvider(PrototypeUiCatalog catalog)
        {
            var geoCatalog = Resources.Load<LandmarkGeoCatalog>("UI/LandmarkGeoCatalog");
            var projection = BuildCalibratedProjection(geoCatalog, catalog);
            if (geoCatalog == null || projection == null)
            {
                Debug.LogWarning("LandmarkGeoCatalog is missing or under-calibrated (needs 3 non-collinear " +
                    "isMapCalibrationAnchor landmarks whose id matches a PrototypeUiCatalog marker's targetId). " +
                    "Falling back to the deterministic map provider.");
                return new DeterministicLandmarkMapProvider();
            }
            return new RealLandmarkMapProvider(SharedLocationService, geoCatalog, projection);
        }

        static GeoToMapProjection BuildCalibratedProjection(LandmarkGeoCatalog geoCatalog, PrototypeUiCatalog uiCatalog)
        {
            if (geoCatalog == null) return null;
            var anchors = new List<(GeoPoint geo, Vector2 map)>();
            foreach (var landmark in geoCatalog.landmarks)
            {
                if (!landmark.isMapCalibrationAnchor) continue;
                MapMarkerUiData marker = null;
                foreach (var candidate in uiCatalog.markers)
                    if (candidate.targetId == landmark.id) { marker = candidate; break; }
                if (marker == null) continue;
                anchors.Add((landmark.Location, marker.normalizedPosition));
            }
            if (anchors.Count != 3) return null;
            try { return new GeoToMapProjection(anchors[0].geo, anchors[0].map, anchors[1].geo, anchors[1].map, anchors[2].geo, anchors[2].map); }
            catch (ArgumentException) { return null; }
        }

        void InitializeMapView()
        {
            MapView = gameObject.AddComponent<WebViewMapView>();
            try
            {
                var bridge = TestWebViewBridgeOverride ?? new GreeWebViewBridge(gameObject);
                MapView.Initialize(bridge);
            }
            catch (Exception e)
            {
                // A failure here must never abort the rest of Awake() - SaveData/InitialLoadResult below this
                // call are unrelated to the map and the whole app must not break because the map couldn't start.
                Debug.LogWarning($"WebView map failed to initialize ({e.Message}); falling back to the static illustrated map.");
            }
        }

        public bool CompleteSetup(string displayName)
        {
            if (!PlayerSaveData.IsValidDisplayName(displayName)) return false;
            SaveData = PlayerSaveData.CreateNew(displayName);
            _progression = new CompanionProgressionService(SaveData);
            Persist();
            Navigator.SwitchRoot(UiRootTab.Map);
            return true;
        }

        public void StartWalk()
        {
            RequireProfile();
            _walkEligibleCompanionIds = _progression.CaptureUnlockedCompanionIds();
            WalkProvider.StartWalk();
            Navigator.Push(UiRoute.ActiveWalk);
        }

        public WalkResultDto FinishWalk()
        {
            RequireProfile();
            var eligible = _walkEligibleCompanionIds ?? _progression.CaptureUnlockedCompanionIds();
            LastWalkResult = _progression.CompleteWalk(WalkProvider.StopWalk(), eligible);
            _walkEligibleCompanionIds = null;
            Persist();
            Navigator.Push(UiRoute.WalkResult);
            return LastWalkResult;
        }

        public FeedResultDto PurchaseAndFeed(string foodId, string companionId)
        {
            RequireProfile();
            var result = _progression.PurchaseAndFeed(foodId, companionId);
            if (result.success) Persist();
            return result;
        }

        public LandmarkRewardDto CompleteLandmarkMemory(string landmarkId)
        {
            RequireProfile();
            var companionRewardId = FindLandmark(landmarkId)?.companionRewardId;
            LastLandmarkReward = _progression.CompleteLandmarkMemory(landmarkId, companionRewardId, DateTime.UtcNow);
            if (LastLandmarkReward.newlyCompleted) Persist();
            return LastLandmarkReward;
        }

        /// <summary>Mock/demo path: fabricates a file path without writing real image data.</summary>
        public string SaveArPhoto(string path = null)
        {
            RequireProfile();
            var value = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Application.persistentDataPath, "mock-ar-photo-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".jpg")
                : path;
            RecordPhoto(value);
            return value;
        }

        /// <summary>
        /// AR/3D integration hook: pass the real captured frame (encoded PNG bytes) here once a teammate's
        /// AR Photo capture is implemented. Writes the file, records it, and links it to the current Landmark's
        /// Journey entry if one exists - see docs/AR-3D-INTEGRATION-CONTRACT.md.
        /// </summary>
        public string SaveArPhoto(byte[] pngBytes)
        {
            RequireProfile();
            if (pngBytes == null || pngBytes.Length == 0) throw new ArgumentException("pngBytes must not be empty.", nameof(pngBytes));
            var value = Path.Combine(Application.persistentDataPath, "ar-photo-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + ".png");
            File.WriteAllBytes(value, pngBytes);
            RecordPhoto(value);
            return value;
        }

        void RecordPhoto(string path)
        {
            if (!SaveData.savedPhotoPaths.Contains(path)) SaveData.savedPhotoPaths.Add(path);

            var landmarkId = PetArSceneContext.LandmarkId;
            if (!string.IsNullOrEmpty(landmarkId))
            {
                var journey = FindLatestJourneyForLandmark(landmarkId);
                if (journey != null) journey.photoPath = path;
            }
            else
            {
                RecordPetPhoto(path);
            }
            Persist();
        }

        /// <summary>
        /// Links an AR photo taken outside the Landmark flow (Photo/Feed/Companion/Walk entry
        /// points) to a Journey entry for that pet + day - creating one if this is the first
        /// photo of that pet taken today.
        /// </summary>
        void RecordPetPhoto(string path)
        {
            var companionId = PetArSceneContext.PetId;
            if (string.IsNullOrEmpty(companionId)) return;

            var today = DateTime.UtcNow;
            JourneyEntryData journey = null;
            foreach (var candidate in SaveData.journeys)
            {
                if (candidate == null || candidate.companionId != companionId) continue;
                if (!DateTime.TryParse(candidate.createdUtc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var createdUtc)) continue;
                if (createdUtc.Date != today.Date) continue;
                journey = candidate;
            }

            if (journey == null)
            {
                journey = new JourneyEntryData
                {
                    id = "pet-" + companionId + "-" + today.ToString("yyyyMMdd"),
                    companionId = companionId,
                    title = CompanionName(companionId) + " in AR",
                    summary = "Photo taken in AR on " + today.ToString("yyyy-MM-dd") + ".",
                    createdUtc = today.ToString("O")
                };
                SaveData.journeys.Add(journey);
            }
            journey.photoPath = path;
        }

        string CompanionName(string companionId)
        {
            foreach (var companion in Data.Companions)
                if (companion.id == companionId) return companion.name;
            return companionId;
        }

        JourneyEntryData FindLatestJourneyForLandmark(string landmarkId)
        {
            JourneyEntryData match = null;
            foreach (var journey in SaveData.journeys)
                if (journey != null && journey.landmarkId == landmarkId) match = journey;
            return match;
        }

        LandmarkUiData FindLandmark(string landmarkId)
        {
            foreach (var landmark in Data.Landmarks)
                if (landmark.id == landmarkId) return landmark;
            return null;
        }

        /// <summary>
        /// AR/3D integration hook: the species/growth-stage/scale AR and 3D preview code should render for a
        /// companion, without touching save data directly - see docs/AR-3D-INTEGRATION-CONTRACT.md.
        /// </summary>
        public CompanionVisualState GetCompanionVisualState(string companionId)
        {
            var progress = Companion(companionId);
            var unlocked = progress != null && progress.unlocked;
            var stage = unlocked ? CompanionProgressionService.StageFor(progress.growthExperience) : GrowthStage.Baby;
            return new CompanionVisualState
            {
                companionId = companionId,
                unlocked = unlocked,
                stage = stage,
                scale = unlocked ? CompanionProgressionService.PlaceholderScaleFor(stage) : 0f
            };
        }

        /// <summary>
        /// AR/3D integration hook: call this when the player taps the AR companion, so UI code (e.g. a toast
        /// reaction) can respond without the AR scene needing to know about the UI - see docs/AR-3D-INTEGRATION-CONTRACT.md.
        /// </summary>
        public event Action<string> CompanionTapped;
        public void NotifyCompanionTapped(string companionId) => CompanionTapped?.Invoke(companionId);

        public void ResetLocalProgress()
        {
            _saveStore.Reset();
            SaveData = null;
            _progression = null;
            LastWalkResult = null;
            LastLandmarkReward = null;
            InitialLoadResult = new SaveLoadResult { status = SaveLoadStatus.Missing };
            Navigator.ResetToSetup();
        }

        /// <summary>
        /// Single entry point into the shared AR scene for every AR feature (Home/Map AR Photo,
        /// Companion "View in AR", Feed "View in AR", Walk's pet tap, and Landmark AR Memory).
        /// Only the context differs - see docs/AR-3D-INTEGRATION-CONTRACT.md.
        /// </summary>
        public void EnterPetAr(string petId, bool isPhotoMode,
            PendingPetInteraction interaction = PendingPetInteraction.None, string landmarkId = null)
        {
            RequireProfile();
            PetArSceneContext.PetId = petId;
            PetArSceneContext.IsPhotoMode = isPhotoMode;
            PetArSceneContext.Interaction = interaction;
            PetArSceneContext.LandmarkId = landmarkId;
            PetArSceneContext.ReturnRoot = Navigator.CurrentRoot;
            Navigator.Push(UiRoute.PetAr);
            SceneManager.LoadScene("PetAr");
        }

        public void ReturnFromPetAr()
        {
            if (Navigator.CurrentRoute == UiRoute.PetAr) Navigator.Back();
            SceneManager.LoadScene("Home");
        }

        /// <summary>The companion to show when an entry point (e.g. Walk) has no explicit pet
        /// selection of its own: the first unlocked companion in roster order, falling back to
        /// the starter if somehow none are unlocked yet.</summary>
        public string PrimaryCompanionId()
        {
            foreach (var entry in CompanionRoster.Entries)
            {
                var progress = Companion(entry.Id);
                if (progress != null && progress.unlocked) return entry.Id;
            }
            return CompanionRoster.Entries[0].Id;
        }

        public void ReturnHome(UiRootTab root = UiRootTab.Map)
        {
            Navigator.SwitchRoot(root);
            SceneManager.LoadScene("Home");
        }

        public CompanionProgressData Companion(string id) => SaveData?.FindCompanion(id);

        public void Persist()
        {
            if (SaveData != null) _saveStore.Save(SaveData);
        }

        void RequireProfile()
        {
            if (!HasProfile) throw new InvalidOperationException("A local profile must be created first.");
        }

        public static void ClearTestOverrides()
        {
            TestSavePathOverride = null;
            TestWalkProviderOverride = null;
            TestMapProviderOverride = null;
            TestWebViewBridgeOverride = null;
        }
    }
}
