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
        /// <summary>The onboarding tutorial chain, in the order the Mission Card presents them: walk
        /// 20m (reward: 3 rice balls - exactly enough Growth EXP to push the starter Corgi from Baby
        /// to Young, so the very next step is guaranteed completable), level up the companion to Young
        /// (reward: 20 coins), then buy a food item from the Shop (reward: 10 coins). The starter
        /// companion itself is already granted during onboarding, before any mission card could show
        /// it, so there's no separate "get a companion" tutorial step here.</summary>
        static readonly MissionDefinition[] Tutorial =
        {
            new MissionDefinition { id = "tutorial-walk", type = MissionType.Tutorial, iconKey = "🚶", title = "Walk Together", description = "Walk 20m with your companion.", targetValue = 0.02f, rewardFoodId = "rice-ball", rewardFoodQuantity = 3 },
            new MissionDefinition { id = "tutorial-levelup", type = MissionType.Tutorial, iconKey = "🌱", title = "Level up your companion", description = "Feed your companion until it reaches Young.", targetValue = 1f, rewardCoins = 20 },
            new MissionDefinition { id = "tutorial-shop", type = MissionType.Tutorial, iconKey = "🛍️", title = "A Snack for Your Friend", description = "Buy a food item from the Shop.", targetValue = 1f, rewardCoins = 10 },
        };

        /// <summary>Post-tutorial walking milestones: an unbounded doubling series starting at 5 km
        /// (5, 10, 20, 40, ...), each worth a flat 20 coins - the player always has exactly one next
        /// target, forever, rather than running out after a fixed table.</summary>
        const float FirstMilestoneKm = 5f;
        const int MilestoneRewardCoins = 20;
        /// <summary>Safety cap on how many doublings to search for an unclaimed milestone - doubling
        /// from 5km blows past any realistic lifetime distance long before this many steps.</summary>
        const int MaxMilestoneLookahead = 64;

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

            var milestoneKm = FirstMilestoneKm;
            for (var i = 0; i < MaxMilestoneLookahead; i++)
            {
                var missionId = MilestoneId(milestoneKm);
                var progress = FindOrCreate(missionId);
                if (progress.status != MissionStatus.Claimed)
                {
                    progress.currentValue = _save.totalDistanceKilometres;
                    if (progress.currentValue >= milestoneKm) progress.status = MissionStatus.Completed;
                    return new MissionUiState
                    {
                        missionId = missionId,
                        type = MissionType.Walking,
                        iconKey = "🏃",
                        title = milestoneKm.ToString("0") + " Kilometers",
                        description = "Walk a total of " + milestoneKm.ToString("0") + " km.",
                        currentValue = progress.currentValue,
                        targetValue = milestoneKm,
                        status = progress.status,
                        rewardLabel = RewardLabel(MilestoneRewardCoins, null, 0)
                    };
                }
                milestoneKm *= 2f;
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
                _save.coins += MilestoneRewardCoins;
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
                case "tutorial-levelup":
                    var starterEntry = CompanionRoster.Entries[0];
                    var starterProgress = _save.FindCompanion(starterEntry.Id);
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
