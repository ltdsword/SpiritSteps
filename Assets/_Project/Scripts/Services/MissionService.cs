using System;
using System.Collections.Generic;

namespace ARWalking.UI
{
    /// <summary>
    /// Drives the single floating Mission Card on the Map screen. Tutorial, Walking-milestone, and
    /// Landmark missions all flow through one <see cref="CurrentMission"/>/<see cref="ClaimCurrent"/>
    /// pair and render through the same card component (design doc section 48) - only one mission
    /// is ever "active" at a time: the tutorial chain first, then the next unclaimed walking
    /// milestone, then a nearby undiscovered Landmark if one is in range.
    /// </summary>
    public sealed class MissionService
    {
        /// <summary>Condensed from the design doc's 5-step tutorial (section 18-22): the "First
        /// Companion" step is skipped here because the starter companion is already granted during
        /// onboarding, before any mission card could show it.</summary>
        static readonly MissionDefinition[] Tutorial =
        {
            new MissionDefinition { id = "tutorial-walk", type = MissionType.Tutorial, iconKey = "🚶", title = "Walk Together", description = "Walk 500m with your companion.", targetValue = 0.5f, rewardCoins = 5 },
            new MissionDefinition { id = "tutorial-shop", type = MissionType.Tutorial, iconKey = "🍙", title = "A Snack for Your Friend", description = "Buy a food item from the Shop.", targetValue = 1f },
            new MissionDefinition { id = "tutorial-feed", type = MissionType.Tutorial, iconKey = "🍙", title = "Snack Time", description = "Feed your companion.", targetValue = 1f, rewardFoodId = "rice-ball", rewardFoodQuantity = 1 },
            new MissionDefinition { id = "tutorial-grow", type = MissionType.Tutorial, iconKey = "🌱", title = "Grow Together", description = "Feed your companion until it becomes Young.", targetValue = 1f, rewardCoins = 50 },
        };

        /// <summary>Lifetime walking-distance milestones (design doc section 24-26).</summary>
        static readonly (float km, string title, int coins, string foodId, int foodQty)[] Milestones =
        {
            (1f, "First Kilometer", 50, "rice-ball", 1),
            (10f, "10 Kilometers", 150, "chicken-leg", 1),
            (50f, "50 Kilometers", 500, "chicken-leg", 2),
            (100f, "100 Kilometers", 1000, null, 0),
            (500f, "500 Kilometers", 5000, null, 0),
        };

        readonly PlayerSaveData _save;
        readonly IUiDataProvider _data;
        readonly ILandmarkMapProvider _mapProvider;

        public MissionService(PlayerSaveData save, IUiDataProvider data, ILandmarkMapProvider mapProvider)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _data = data;
            _mapProvider = mapProvider;
        }

        MissionProgressData FindOrCreate(string missionId)
        {
            var entry = _save.missions.Find(m => m != null && m.missionId == missionId);
            if (entry == null)
            {
                entry = new MissionProgressData { missionId = missionId };
                _save.missions.Add(entry);
            }
            return entry;
        }

        /// <summary>The single mission the Map's Mission Card should show right now, or null if
        /// there is nothing active (tutorial and every milestone claimed, no landmark nearby).</summary>
        public MissionUiState CurrentMission()
        {
            if (!_save.tutorialComplete)
            {
                var allClaimed = true;
                foreach (var def in Tutorial)
                {
                    var progress = FindOrCreate(def.id);
                    if (progress.status == MissionStatus.Claimed) continue;
                    allClaimed = false;
                    UpdateTutorialProgress(def, progress);
                    return ToUiState(def, progress);
                }
                if (allClaimed) _save.tutorialComplete = true;
            }

            foreach (var milestone in Milestones)
            {
                var missionId = MilestoneId(milestone.km);
                var progress = FindOrCreate(missionId);
                if (progress.status == MissionStatus.Claimed) continue;
                progress.currentValue = _save.totalDistanceKilometres;
                if (progress.currentValue >= milestone.km) progress.status = MissionStatus.Completed;
                return new MissionUiState
                {
                    missionId = missionId,
                    type = MissionType.Walking,
                    iconKey = "🏃",
                    title = milestone.title,
                    description = "Walk a total of " + milestone.km.ToString("0") + " km.",
                    currentValue = progress.currentValue,
                    targetValue = milestone.km,
                    status = progress.status,
                    rewardLabel = RewardLabel(milestone.coins, milestone.foodId, milestone.foodQty)
                };
            }

            if (_data != null && _mapProvider != null)
            {
                foreach (var landmark in _data.Landmarks)
                {
                    if (_save.completedLandmarkIds.Contains(landmark.id)) continue;
                    var proximity = _mapProvider.GetLandmarkProximity(landmark.id);
                    if (!proximity.isWithinUnlockRadius) continue;
                    return new MissionUiState
                    {
                        missionId = "landmark-" + landmark.id,
                        type = MissionType.Landmark,
                        iconKey = "📍",
                        title = "Landmark Nearby",
                        description = "You've arrived near " + landmark.name + ". Open it to start the AR Memory.",
                        currentValue = 1f,
                        targetValue = 1f,
                        status = MissionStatus.Completed,
                        rewardLabel = "Explore",
                        landmarkId = landmark.id,
                        landmarkName = landmark.name
                    };
                }
            }

            return null;
        }

        /// <summary>Claims the current mission's reward if it's Completed. Landmark missions have no
        /// claim step - they resolve by opening the Landmark sheet/AR flow instead.</summary>
        public bool ClaimCurrent()
        {
            var state = CurrentMission();
            if (state == null || state.status != MissionStatus.Completed || state.type == MissionType.Landmark) return false;

            var progress = FindOrCreate(state.missionId);
            progress.status = MissionStatus.Claimed;

            if (state.type == MissionType.Tutorial)
            {
                var def = Array.Find(Tutorial, d => d.id == state.missionId);
                if (def != null)
                {
                    _save.coins += def.rewardCoins;
                    if (!string.IsNullOrEmpty(def.rewardFoodId)) _save.AddFood(def.rewardFoodId, def.rewardFoodQuantity);
                }
                var stillActive = false;
                foreach (var d in Tutorial) if (FindOrCreate(d.id).status != MissionStatus.Claimed) stillActive = true;
                if (!stillActive) _save.tutorialComplete = true;
            }
            else if (state.type == MissionType.Walking)
            {
                foreach (var milestone in Milestones)
                {
                    if (MilestoneId(milestone.km) != state.missionId) continue;
                    _save.coins += milestone.coins;
                    if (!string.IsNullOrEmpty(milestone.foodId)) _save.AddFood(milestone.foodId, milestone.foodQty);
                    break;
                }
            }

            return true;
        }

        void UpdateTutorialProgress(MissionDefinition def, MissionProgressData progress)
        {
            switch (def.id)
            {
                case "tutorial-walk":
                    progress.currentValue = _save.totalDistanceKilometres;
                    if (progress.currentValue >= def.targetValue) progress.status = MissionStatus.Completed;
                    break;
                case "tutorial-shop":
                    progress.currentValue = _save.everPurchasedFood ? 1f : 0f;
                    if (_save.everPurchasedFood) progress.status = MissionStatus.Completed;
                    break;
                case "tutorial-feed":
                    progress.currentValue = _save.everFedCompanion ? 1f : 0f;
                    if (_save.everFedCompanion) progress.status = MissionStatus.Completed;
                    break;
                case "tutorial-grow":
                    var starterId = CompanionRoster.Entries[0].Id;
                    var starterEntry = CompanionRoster.Entries[0];
                    var starterProgress = _save.FindCompanion(starterId);
                    var stage = CompanionProgressionService.StageFor(starterEntry, starterProgress?.growthExperience ?? 0);
                    progress.currentValue = stage == GrowthStage.Baby ? 0f : 1f;
                    if (stage != GrowthStage.Baby) progress.status = MissionStatus.Completed;
                    break;
            }
        }

        static MissionUiState ToUiState(MissionDefinition def, MissionProgressData progress) => new MissionUiState
        {
            missionId = def.id,
            type = def.type,
            iconKey = def.iconKey,
            title = def.title,
            description = def.description,
            currentValue = progress.currentValue,
            targetValue = def.targetValue,
            status = progress.status,
            rewardLabel = RewardLabel(def.rewardCoins, def.rewardFoodId, def.rewardFoodQuantity)
        };

        static string RewardLabel(int coins, string foodId, int foodQty)
        {
            var parts = new List<string>();
            if (coins > 0) parts.Add(coins + " coins");
            if (!string.IsNullOrEmpty(foodId) && foodQty > 0) parts.Add(foodQty + "x " + foodId);
            return parts.Count > 0 ? string.Join(" + ", parts) : string.Empty;
        }

        static string MilestoneId(float km) => "milestone-" + km.ToString("0") + "km";
    }
}
