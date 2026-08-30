using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    public enum UiRootTab
    {
        Map,
        Garden,
        WalkAr,
        Journal,
        Book
    }

    public enum UiRoute
    {
        OnboardingPermissions,
        HomeMap,
        ActiveWalk,
        WalkSummary,
        SpiritCollection,
        SpiritDetail,
        SeedlingGrowth,
        HatchReveal,
        ArCompanion,
        ArPhoto,
        LandmarkMemory,
        JourneyJournal,
        JourneyDetail
    }

    public enum UiOverlay
    {
        Settings,
        Permissions,
        SyncStatus,
        Collectibles,
        Confirmation,
        Error,
        EmptyState
    }

    public enum MapMarkerType
    {
        PlayerSpirit,
        Landmark,
        Seedling,
        CulturalCollectible,
        ArDiscoveryHint
    }

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
        IReadOnlyList<SpiritUiData> Spirits { get; }
        IReadOnlyList<SeedlingUiData> Seedlings { get; }
        IReadOnlyList<WalkUiData> Walks { get; }
        IReadOnlyList<LandmarkUiData> Landmarks { get; }
        IReadOnlyList<JourneyUiData> Journeys { get; }
        IReadOnlyList<PhotoUiData> Photographs { get; }
        IReadOnlyList<CollectibleUiData> Collectibles { get; }
    }

    public interface IMapDataProvider
    {
        IllustratedMapUiData Map { get; }
        IReadOnlyList<MapMarkerUiData> Markers { get; }
    }

    [Serializable]
    public sealed class SpiritUiData
    {
        public string id;
        public string name;
        public string culturalTitle;
        [TextArea] public string description;
        public string imageKey;
        public bool collected;
        public bool isSelected;
    }

    [Serializable]
    public sealed class SeedlingUiData
    {
        public string id;
        public string name;
        public string locationName;
        public string imageKey;
        [Min(0)] public int currentSteps;
        [Min(1)] public int requiredSteps = 1;
        public bool ready;

        public float Progress => Mathf.Clamp01((float)currentSteps / Mathf.Max(1, requiredSteps));
    }

    [Serializable]
    public sealed class WalkUiData
    {
        public string id;
        public string dateLabel;
        public string placeName;
        [Min(0)] public int steps;
        [Min(0)] public int durationMinutes;
        [Min(0)] public float distanceKilometres;
        [Min(0)] public int discoveries;
    }

    [Serializable]
    public sealed class LandmarkUiData
    {
        public string id;
        public string name;
        public string subtitle;
        [TextArea] public string memoryText;
        public string imageKey;
        public bool discovered;
    }

    [Serializable]
    public sealed class JourneyUiData
    {
        public string id;
        public string title;
        public string dateLabel;
        public string summary;
        public string imageKey;
        public string landmarkId;
        public int steps;
        public int memories;
    }

    [Serializable]
    public sealed class PhotoUiData
    {
        public string id;
        public string title;
        public string dateLabel;
        public string imageKey;
        public bool saved;
    }

    [Serializable]
    public sealed class CollectibleUiData
    {
        public string id;
        public string name;
        public string category;
        public string imageKey;
        public bool collected;
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
}
