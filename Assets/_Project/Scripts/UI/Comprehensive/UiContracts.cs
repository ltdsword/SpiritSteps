using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>
    /// Companion ids match <c>CorgiAR.PetCatalog.Entries</c> ids exactly - this is the only
    /// join between the ARWalking and CorgiAR assemblies (CorgiAR has no reference back to
    /// ARWalking, so <see cref="CorgiAR.PetArContextBinder"/> resolves the id string directly
    /// against its own PetBinder bindings).
    /// </summary>
    public static class PrototypeIds
    {
        public const string Corgi = "corgi";
        public const string Pug = "pug";
        public const string Chihuahua = "chihuahua";
        public const string ShibaKit = "cur";
        public const string GermanShepherd = "germanshepherd";
        public const string Fox = "uaa_fox";
        public const string Husky = "uaa_husky";
        public const string Wolf = "uaa_wolf";
        public const string Shiba = "uaa_shiba";
        public const string Alpaca = "uaa_alpaca";
        public const string Deer = "uaa_deer";
        public const string Stag = "uaa_stag";
        public const string Donkey = "uaa_donkey";
        public const string Bull = "uaa_bull";
        public const string Cow = "uaa_cow";
        public const string Horse = "uaa_horse";
        public const string HorseWhite = "uaa_horse_white";

        /// <summary>Every companion id in unlock-progression order (starter first).</summary>
        public static readonly string[] AllCompanions =
        {
            Corgi, Husky, Fox, Wolf, Pug, Chihuahua, ShibaKit, GermanShepherd, Shiba,
            Alpaca, Deer, Stag, Donkey, Bull, Cow, Horse, HorseWhite
        };

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
        PetAr,
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
        /// <summary>Total walking distance required to unlock this companion; 0 = starter (unlocked from the start).</summary>
        [Min(0f)] public float unlockDistanceKilometres;
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
        /// <summary>Companion id unlocked by completing this Landmark's AR Memory, or empty for none.</summary>
        public string companionRewardId;
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
        /// <summary>Companion id unlocked by this completion, or empty when none was unlocked.</summary>
        public string unlockedCompanionId = string.Empty;
        public string journeyId;
    }

    /// <summary>
    /// AR/3D integration contract: the current visual state of one companion (species, growth stage, scale).
    /// AR/3D code should read this instead of touching save data directly - see docs/AR-3D-INTEGRATION-CONTRACT.md.
    /// </summary>
    [Serializable]
    public sealed class CompanionVisualState
    {
        public string companionId;
        public bool unlocked;
        public GrowthStage stage;
        /// <summary>Placeholder model scale for the current stage; 0 when the companion is locked.</summary>
        public float scale;
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
