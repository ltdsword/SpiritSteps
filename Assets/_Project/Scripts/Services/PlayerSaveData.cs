using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    [Serializable]
    public sealed class CompanionProgressData
    {
        public string companionId;
        public bool unlocked;
        [Min(0)] public int growthExperience;
    }

    [Serializable]
    public sealed class JourneyEntryData
    {
        public string id;
        /// <summary>Set for Landmark-flow entries (AR Memory / stamp); null for pet-photo entries.</summary>
        public string landmarkId;
        /// <summary>Set for a photo taken of a pet outside the Landmark flow (Photo/Feed/Companion/Walk
        /// entry points into PetAr); null for Landmark-flow entries.</summary>
        public string companionId;
        public string title;
        public string summary;
        public string createdUtc;
        public float distanceKilometres;
        public bool hasSteps;
        public int steps;
        public float durationSeconds;
        /// <summary>Path to an AR Photo taken during this visit, or null/empty when none was saved.</summary>
        public string photoPath;
    }

    [Serializable]
    public sealed class StampData
    {
        public string stampId;
        public string landmarkId;
        public string collectedUtc;
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        /// <summary>v2: companion roster switched from dog/cat/rabbit to the 17-pet CorgiAR
        /// roster (see CompanionRoster); old ids are dropped on load by RepairCollections().</summary>
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public bool setupComplete;
        public string displayName;
        [Min(0)] public int coins;
        [Min(0f)] public float totalDistanceKilometres;
        public bool hasTotalSteps;
        [Min(0)] public int totalSteps;
        public List<CompanionProgressData> companions = new List<CompanionProgressData>();
        public List<StampData> stamps = new List<StampData>();
        public List<string> completedLandmarkIds = new List<string>();
        public List<JourneyEntryData> journeys = new List<JourneyEntryData>();
        public List<string> savedPhotoPaths = new List<string>();

        public static PlayerSaveData CreateNew(string displayName)
        {
            var normalized = NormalizeDisplayName(displayName);
            if (!IsValidDisplayName(normalized))
                throw new ArgumentException("Display name must contain 1 to 20 characters.", nameof(displayName));

            var save = new PlayerSaveData { setupComplete = true, displayName = normalized, coins = 0 };
            save.RepairCollections();
            return save;
        }

        public static string NormalizeDisplayName(string value) => (value ?? string.Empty).Trim();
        public static bool IsValidDisplayName(string value)
        {
            var normalized = NormalizeDisplayName(value);
            return normalized.Length >= 1 && normalized.Length <= 20;
        }

        public CompanionProgressData FindCompanion(string companionId)
        {
            return companions?.Find(item => item != null && item.companionId == companionId);
        }

        public void RepairCollections()
        {
            companions = companions ?? new List<CompanionProgressData>();
            stamps = stamps ?? new List<StampData>();
            completedLandmarkIds = completedLandmarkIds ?? new List<string>();
            journeys = journeys ?? new List<JourneyEntryData>();
            savedPhotoPaths = savedPhotoPaths ?? new List<string>();

            // Drop companion ids from an older roster (e.g. the pre-migration dog/cat/rabbit
            // save format) that no longer exist in CompanionRoster.
            var validIds = new HashSet<string>();
            foreach (var entry in CompanionRoster.Entries) validIds.Add(entry.Id);
            companions.RemoveAll(item => item == null || !validIds.Contains(item.companionId));

            foreach (var entry in CompanionRoster.Entries)
            {
                var isStarter = entry.UnlockDistanceKilometres <= 0f;
                EnsureCompanion(entry.Id, isStarter, isStarter ? 450 : 0);
            }
        }

        void EnsureCompanion(string id, bool unlocked, int experience)
        {
            if (FindCompanion(id) != null) return;
            companions.Add(new CompanionProgressData { companionId = id, unlocked = unlocked, growthExperience = experience });
        }
    }
}
