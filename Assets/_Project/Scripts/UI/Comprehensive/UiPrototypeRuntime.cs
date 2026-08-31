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

        public UiNavigationStack Navigator { get; private set; }
        public IUiDataProvider Data { get; private set; }
        public IMapDataProvider MapData { get; private set; }
        public PrototypeUiAssets Assets { get; private set; }
        public PlayerSaveData SaveData { get; private set; }
        public SaveLoadResult InitialLoadResult { get; private set; }
        public IWalkMetricsProvider WalkProvider { get; private set; }
        public ILandmarkMapProvider LandmarkMapProvider { get; private set; }
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
            WalkProvider = TestWalkProviderOverride ?? new DeterministicWalkMetricsProvider();
            LandmarkMapProvider = TestMapProviderOverride ?? new DeterministicLandmarkMapProvider();
            _saveStore = new LocalPlayerSaveStore(TestSavePathOverride);
            InitialLoadResult = _saveStore.Load();
            SaveData = InitialLoadResult.save;
            if (SaveData != null) _progression = new CompanionProgressionService(SaveData);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            var landmarkId = SelectedLandmarkIndex >= 0 && SelectedLandmarkIndex < Data.Landmarks.Count
                ? Data.Landmarks[SelectedLandmarkIndex].id
                : null;
            var journey = string.IsNullOrEmpty(landmarkId) ? null : FindLatestJourneyForLandmark(landmarkId);
            if (journey != null) journey.photoPath = path;
            Persist();
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

        public void EnterLandmarkAr()
        {
            Navigator.Push(UiRoute.LandmarkArMemory);
            SceneManager.LoadScene("Walk");
        }

        public void ReturnFromArToHome()
        {
            if (Navigator.CurrentRoute == UiRoute.ArPhoto) Navigator.Back();
            if (Navigator.CurrentRoute == UiRoute.LandmarkArMemory) Navigator.Back();
            SceneManager.LoadScene("Home");
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
        }
    }
}
