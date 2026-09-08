using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    [Serializable]
    public sealed class CompanionProgressData
    {
        public string companionId;
        /// <summary>Total walking distance requirement has been met (design doc "Unlocked" state).
        /// Does not by itself mean the player has the companion - see <see cref="owned"/>.</summary>
        public bool unlocked;
        /// <summary>Purchased with coins (or granted free/by a Landmark reward) - design doc "Owned"
        /// state. A companion must be both <see cref="unlocked"/> and <see cref="owned"/> to appear
        /// in the Companions tab / be selectable as lead.</summary>
        public bool owned;
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

    /// <summary>One calendar day's walked distance/steps, keyed by UTC date ("yyyy-MM-dd"). Feeds the Activity Dashboard's weekly chart.</summary>
    [Serializable]
    public sealed class DailyActivityData
    {
        public string dateIso;
        [Min(0f)] public float distanceKilometres;
        public bool hasSteps;
        [Min(0)] public int steps;
    }

    /// <summary>How many of one food item the player is carrying. A list (not a Dictionary) because
    /// Unity's JsonUtility cannot serialize dictionaries.</summary>
    [Serializable]
    public sealed class FoodInventoryEntry
    {
        public string foodId;
        [Min(0)] public int quantity;
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        /// <summary>v2: companion roster switched from dog/cat/rabbit to the 17-pet CorgiAR roster
        /// (see CompanionRoster). v3: split "unlocked" (distance requirement met) from a new "owned"
        /// (purchased/granted) flag, added leadCompanionId, foodInventory, and missions - see
        /// docs/AR-Walking-Cultural-Exploration-Game-Progression-Shop-Tutorial-Landmark-Journey-Design.md.
        /// Old ids/format are repaired on load by RepairCollections().</summary>
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public bool setupComplete;
        public string displayName;
        [Min(0)] public int coins;
        [Min(0f)] public float totalDistanceKilometres;
        public bool hasTotalSteps;
        [Min(0)] public int totalSteps;
        public List<CompanionProgressData> companions = new List<CompanionProgressData>();
        /// <summary>The player's chosen active/lead companion - the only one that earns walking
        /// income (design doc section 2). Falls back to the starter if unset/invalid.</summary>
        public string leadCompanionId;
        public List<FoodInventoryEntry> foodInventory = new List<FoodInventoryEntry>();
        public List<MissionProgressData> missions = new List<MissionProgressData>();
        /// <summary>All tutorial missions have been claimed - once true, the Mission Card moves on
        /// to Walking milestones/Landmark missions and never re-checks the tutorial chain.</summary>
        public bool tutorialComplete;
        /// <summary>Set once the player has ever bought food, for the tutorial's Shop-visit step.</summary>
        public bool everPurchasedFood;
        /// <summary>Set once the player has ever fed a companion, for the tutorial's Feed step.</summary>
        public bool everFedCompanion;
        public List<StampData> stamps = new List<StampData>();
        public List<string> completedLandmarkIds = new List<string>();
        public List<JourneyEntryData> journeys = new List<JourneyEntryData>();
        public List<string> savedPhotoPaths = new List<string>();
        public List<DailyActivityData> dailyActivity = new List<DailyActivityData>();

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

        public int FoodQuantity(string foodId)
        {
            var entry = foodInventory?.Find(item => item != null && item.foodId == foodId);
            return entry?.quantity ?? 0;
        }

        public void AddFood(string foodId, int amount)
        {
            if (amount == 0) return;
            var entry = foodInventory.Find(item => item != null && item.foodId == foodId);
            if (entry == null)
            {
                entry = new FoodInventoryEntry { foodId = foodId, quantity = 0 };
                foodInventory.Add(entry);
            }
            entry.quantity = Mathf.Max(0, entry.quantity + amount);
        }

        /// <summary>Attempts to remove <paramref name="amount"/> of a food item; fails without side
        /// effects if the player doesn't have enough.</summary>
        public bool TryConsumeFood(string foodId, int amount)
        {
            var entry = foodInventory.Find(item => item != null && item.foodId == foodId);
            if (entry == null || entry.quantity < amount) return false;
            entry.quantity -= amount;
            return true;
        }

        public void RepairCollections()
        {
            var isPreOwnedSplitSave = schemaVersion < 3;

            companions = companions ?? new List<CompanionProgressData>();
            foodInventory = foodInventory ?? new List<FoodInventoryEntry>();
            missions = missions ?? new List<MissionProgressData>();
            stamps = stamps ?? new List<StampData>();
            completedLandmarkIds = completedLandmarkIds ?? new List<string>();
            journeys = journeys ?? new List<JourneyEntryData>();
            savedPhotoPaths = savedPhotoPaths ?? new List<string>();
            dailyActivity = dailyActivity ?? new List<DailyActivityData>();

            // Drop companion ids from an older roster (e.g. the pre-migration dog/cat/rabbit
            // save format) that no longer exist in CompanionRoster.
            var validIds = new HashSet<string>();
            foreach (var entry in CompanionRoster.Entries) validIds.Add(entry.Id);
            companions.RemoveAll(item => item == null || !validIds.Contains(item.companionId));

            foreach (var entry in CompanionRoster.Entries)
            {
                var isStarter = entry.UnlockDistanceKilometres <= 0f;
                EnsureCompanion(entry.Id, isStarter, isStarter, 0);
            }

            if (isPreOwnedSplitSave)
            {
                // Pre-v3 saves had no purchase step - "unlocked" alone meant the player already had
                // the companion. Carry that forward as "owned" so nobody loses a companion they had.
                foreach (var companion in companions)
                    if (companion.unlocked) companion.owned = true;
            }

            var lead = string.IsNullOrEmpty(leadCompanionId) ? null : FindCompanion(leadCompanionId);
            if (lead == null || !lead.owned)
                leadCompanionId = CompanionRoster.Entries[0].Id;
        }

        void EnsureCompanion(string id, bool unlocked, bool owned, int experience)
        {
            if (FindCompanion(id) != null) return;
            companions.Add(new CompanionProgressData { companionId = id, unlocked = unlocked, owned = owned, growthExperience = experience });
        }
    }
}
