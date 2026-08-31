using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    public static class PrototypeIds
    {
        public const string Dog = "dog";
        public const string Cat = "cat";
        public const string Rabbit = "rabbit";
        public const string IndependencePalace = "independence-palace";
        public const string CentralPostOffice = "central-post-office";
        public const string NotreDameBasilica = "notre-dame-basilica";
        public const string CentralPostOfficeStamp = "central-post-office-stamp";
    }

    public enum UiRootTab { Map, Companions, Shop, Journey }

    public enum UiRoute
    {
        OnboardingSetup,
        HomeMap,
        ActiveWalk,
        WalkResult,
        CompanionCollection,
        CompanionDetail,
        ShopFood,
        LandmarkDetail,
        LandmarkArMemory,
        ArPhoto,
        JourneyList,
        JourneyDetail
    }

    public enum UiOverlay { Settings, Permissions, Confirmation, Error }
    public enum MapMarkerType { Player, Landmark }
    public enum GrowthStage { Baby, Young, Adult }

    public interface IAppNavigator
    {
        UiRoute CurrentRoute { get; }
        UiRootTab CurrentRoot { get; }
        UiOverlay? CurrentOverlay { get; }
        bool CanGoBack { get; }
        event Action Changed;
        void SwitchRoot(UiRootTab root);
        void Push(UiRoute route);
        bool Back();
        void ShowOverlay(UiOverlay overlay);
        void CloseOverlay();
    }

    public interface IUiDataProvider
    {
        IReadOnlyList<CompanionUiData> Companions { get; }
        IReadOnlyList<FoodUiData> Foods { get; }
        IReadOnlyList<LandmarkUiData> Landmarks { get; }
    }

    public interface IMapDataProvider
    {
        IllustratedMapUiData Map { get; }
        IReadOnlyList<MapMarkerUiData> Markers { get; }
    }

    [Serializable]
    public sealed class CompanionUiData
    {
        public string id;
        public string name;
        [TextArea] public string description;
        public string imageKey;
        public string unlockHint;
    }

    [Serializable]
    public sealed class FoodUiData
    {
        public string id;
        public string name;
        [Min(0)] public int coinCost;
        [Min(0)] public int growthExperience;
        [TextArea] public string description;
    }

    [Serializable]
    public sealed class LandmarkUiData
    {
        public string id;
        public string name;
        public string localName;
        [TextArea] public string history;
        [TextArea] public string architecture;
        [TextArea] public string didYouKnow;
        public string imageKey;
        public bool imageTargetReady;
    }

    [Serializable]
    public sealed class IllustratedMapUiData
    {
        public string textureKey;
        public string regionName;
        [Range(1f, 4f)] public float minimumZoom = 1f;
        [Range(1f, 4f)] public float maximumZoom = 2.8f;
        public Vector2 initialFocus = new Vector2(0.5f, 0.5f);
    }

    [Serializable]
    public sealed class MapMarkerUiData
    {
        public string id;
        public MapMarkerType type;
        public string label;
        public Vector2 normalizedPosition;
        public string targetId;
    }

    [Serializable]
    public sealed class WalkMetrics
    {
        [Min(0f)] public float distanceKilometres;
        public bool hasSteps;
        [Min(0)] public int steps;
        [Min(0f)] public float elapsedSeconds;
    }

    [Serializable]
    public sealed class WalkResultDto
    {
        public float distanceKilometres;
        public bool hasSteps;
        public int steps;
        public float durationSeconds;
        public int completedKilometres;
        public int coinsAwarded;
        public int experiencePerEligibleCompanion;
        public List<string> rewardedCompanionIds = new List<string>();
        public List<string> newlyUnlockedCompanionIds = new List<string>();
    }

    [Serializable]
    public sealed class FeedResultDto
    {
        public bool success;
        public string error;
        public string companionId;
        public string foodId;
        public int coinsSpent;
        public int experienceGained;
        public GrowthStage previousStage;
        public GrowthStage currentStage;
        public bool StageChanged => success && previousStage != currentStage;
    }

    [Serializable]
    public sealed class LandmarkRewardDto
    {
        public bool newlyCompleted;
        public string landmarkId;
        public string stampId;
        public bool rabbitUnlocked;
        public string journeyId;
    }

    [Serializable]
    public sealed class LandmarkMapState
    {
        public bool hasPlayerPosition;
        public Vector2 playerNormalizedPosition;
        public float mapHeadingDegrees;
    }

    [Serializable]
    public sealed class LandmarkProximity
    {
        public string landmarkId;
        public float distanceMetres;
        public float directionDegrees;
        public bool isWithinUnlockRadius;
    }

    public interface IWalkMetricsProvider
    {
        bool IsWalking { get; }
        void StartWalk();
        WalkMetrics GetLiveMetrics();
        WalkMetrics StopWalk();
    }

    public interface ILandmarkMapProvider
    {
        LandmarkMapState GetMapState();
        LandmarkProximity GetLandmarkProximity(string landmarkId);
    }
}
