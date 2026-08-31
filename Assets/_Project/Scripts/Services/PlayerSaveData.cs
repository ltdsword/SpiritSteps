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
        public string landmarkId;
        public string title;
        public string summary;
        public string createdUtc;
        public float distanceKilometres;
        public bool hasSteps;
        public int steps;
        public float durationSeconds;
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
        public const int CurrentSchemaVersion = 1;

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

            return new PlayerSaveData
            {
                setupComplete = true,
                displayName = normalized,
                coins = 0,
                companions = new List<CompanionProgressData>
                {
                    new CompanionProgressData { companionId = PrototypeIds.Dog, unlocked = true, growthExperience = 450 },
                    new CompanionProgressData { companionId = PrototypeIds.Cat, unlocked = false, growthExperience = 0 },
                    new CompanionProgressData { companionId = PrototypeIds.Rabbit, unlocked = false, growthExperience = 0 }
                }
            };
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
            EnsureCompanion(PrototypeIds.Dog, true, 450);
            EnsureCompanion(PrototypeIds.Cat, false, 0);
            EnsureCompanion(PrototypeIds.Rabbit, false, 0);
        }

        void EnsureCompanion(string id, bool unlocked, int experience)
        {
            if (FindCompanion(id) != null) return;
            companions.Add(new CompanionProgressData { companionId = id, unlocked = unlocked, growthExperience = experience });
        }
    }
}
