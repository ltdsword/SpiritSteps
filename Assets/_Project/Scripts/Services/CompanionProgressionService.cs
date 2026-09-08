using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>
    /// Reward/economy math: walking income, growth EXP, food purchase/feeding, companion purchase,
    /// and Landmark rewards. Numbers follow docs/AR-Walking-Cultural-Exploration-Game-Progression-Shop-Tutorial-Landmark-Journey-Design.md.
    /// </summary>
    public sealed class CompanionProgressionService
    {
        /// <summary>Daily walking-distance goal shown by the Activity Dashboard's progress bar and weekly chart.</summary>
        public const float DailyGoalKilometres = 5f;

        readonly PlayerSaveData _save;
        readonly IUiDataProvider _data;

        public CompanionProgressionService(PlayerSaveData save, IUiDataProvider data)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _save.RepairCollections();
        }

        /// <summary>Global growth-stage thresholds. Kept for any caller that only has a raw EXP value
        /// and no companion context. New code that knows which companion should prefer the
        /// per-companion overload below, since rarer companions need more EXP per stage.</summary>
        public static GrowthStage StageFor(int experience)
        {
            if (experience < 500) return GrowthStage.Baby;
            if (experience < 1500) return GrowthStage.Young;
            return GrowthStage.Adult;
        }

        /// <summary>Per-companion growth stage using that companion's own Young/Adult EXP thresholds
        /// (design doc "Balance 17 Pets" table - rarer companions need more EXP per stage).</summary>
        public static GrowthStage StageFor(CompanionRoster.Entry entry, int experience)
        {
            if (experience < entry.YoungExp) return GrowthStage.Baby;
            if (experience < entry.AdultExp) return GrowthStage.Young;
            return GrowthStage.Adult;
        }

        public static float PlaceholderScaleFor(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Baby: return 0.70f;
                case GrowthStage.Young: return 0.85f;
                default: return 1.00f;
            }
        }

        /// <summary>Growth multiplier applied to a companion's base walking income (design doc section 4).</summary>
        public static float GrowthMultiplier(GrowthStage stage)
        {
            if (stage == GrowthStage.Adult) return 1.30f;
            if (stage == GrowthStage.Young) return 1.15f;
            return 1.00f;
        }

        /// <summary>Coins earned per 100 metres walked while this companion is the lead/active
        /// companion, at its current growth stage (design doc section 2).</summary>
        public static float IncomeOf(CompanionRoster.Entry entry, int experience)
        {
            var stage = StageFor(entry, experience);
            var raw = entry.BaseIncomePerHundredMetres * GrowthMultiplier(stage);
            return Mathf.Round(raw * 10f) / 10f;
        }

        public List<string> CaptureUnlockedCompanionIds()
        {
            var result = new List<string>();
            foreach (var companion in _save.companions)
                if (companion.unlocked) result.Add(companion.companionId);
            return result;
        }

        public WalkResultDto CompleteWalk(WalkMetrics metrics) => CompleteWalk(metrics, _save.leadCompanionId, DateTime.UtcNow);
        public WalkResultDto CompleteWalk(WalkMetrics metrics, string leadCompanionId) => CompleteWalk(metrics, leadCompanionId, DateTime.UtcNow);

        /// <summary>
        /// Applies one walk's rewards: coins earned by the lead/active companion only (design doc
        /// section 2 - "only Active Pet earns Coin"), plus any distance-unlock thresholds crossed.
        /// Walking itself grants no Growth EXP - EXP only comes from feeding (design doc section 4).
        /// </summary>
        public WalkResultDto CompleteWalk(WalkMetrics metrics, string leadCompanionId, DateTime utcNow)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            var distance = Math.Max(0f, metrics.distanceKilometres);
            var wholeKilometres = (int)Math.Floor(distance + 0.00001f);
            var result = new WalkResultDto
            {
                distanceKilometres = distance,
                hasSteps = metrics.hasSteps,
                steps = metrics.hasSteps ? Math.Max(0, metrics.steps) : 0,
                durationSeconds = Math.Max(0f, metrics.elapsedSeconds),
                completedKilometres = wholeKilometres,
                leadCompanionId = leadCompanionId
            };

            var leadProgress = string.IsNullOrEmpty(leadCompanionId) ? null : _save.FindCompanion(leadCompanionId);
            var leadEntry = string.IsNullOrEmpty(leadCompanionId) ? default : CompanionRoster.Find(leadCompanionId);
            if (leadProgress != null && leadProgress.owned && !string.IsNullOrEmpty(leadEntry.Id))
            {
                var incomePerHundredMetres = IncomeOf(leadEntry, leadProgress.growthExperience);
                result.coinsAwarded = Mathf.RoundToInt(incomePerHundredMetres * (distance * 1000f / 100f));
                _save.coins += result.coinsAwarded;
            }

            _save.totalDistanceKilometres += distance;
            if (metrics.hasSteps)
            {
                _save.hasTotalSteps = true;
                _save.totalSteps += result.steps;
            }

            foreach (var entry in CompanionRoster.Entries)
            {
                if (float.IsPositiveInfinity(entry.UnlockDistanceKilometres)) continue; // Landmark reward only
                var candidate = _save.FindCompanion(entry.Id);
                if (candidate == null || candidate.unlocked) continue;
                if (_save.totalDistanceKilometres < entry.UnlockDistanceKilometres) continue;
                candidate.unlocked = true;
                result.newlyUnlockedCompanionIds.Add(entry.Id);
            }

            RecordDailyActivity(distance, result.hasSteps, result.steps, utcNow);
            return result;
        }

        void RecordDailyActivity(float distanceKilometres, bool hasSteps, int steps, DateTime utcNow)
        {
            var dateKey = utcNow.ToUniversalTime().Date.ToString("yyyy-MM-dd");
            var day = _save.dailyActivity.Find(item => item != null && item.dateIso == dateKey);
            if (day == null)
            {
                day = new DailyActivityData { dateIso = dateKey };
                _save.dailyActivity.Add(day);
            }
            day.distanceKilometres += distanceKilometres;
            if (hasSteps)
            {
                day.hasSteps = true;
                day.steps += steps;
            }
        }

        /// <summary>Today's progress plus the Monday-Sunday week containing it, for the Activity Dashboard screen.</summary>
        public WeeklyActivityDto GetWeeklyActivity(DateTime utcNow)
        {
            var today = utcNow.ToUniversalTime().Date;
            var mondayOffset = ((int)today.DayOfWeek + 6) % 7; // DayOfWeek.Sunday == 0, so shift to a Monday-first week.
            var monday = today.AddDays(-mondayOffset);
            var result = new WeeklyActivityDto { dailyGoalKilometres = DailyGoalKilometres };

            var sumSoFar = 0f;
            var daysSoFar = 0;
            for (var i = 0; i < 7; i++)
            {
                var date = monday.AddDays(i);
                var entry = _save.dailyActivity.Find(item => item != null && item.dateIso == date.ToString("yyyy-MM-dd"));
                var distance = entry?.distanceKilometres ?? 0f;
                var isFuture = date > today;
                result.days[i] = new DayActivity { date = date, distanceKilometres = distance, isToday = date == today, isFuture = isFuture };
                if (!isFuture) { sumSoFar += distance; daysSoFar++; }
            }
            // Averaged over Monday..today only - zero-padding the remaining days of the week would understate progress mid-week.
            result.weeklyAverageKilometres = daysSoFar > 0 ? sumSoFar / daysSoFar : 0f;

            var todayEntry = _save.dailyActivity.Find(item => item != null && item.dateIso == today.ToString("yyyy-MM-dd"));
            result.todayDistanceKilometres = todayEntry?.distanceKilometres ?? 0f;
            result.todayHasSteps = todayEntry?.hasSteps ?? false;
            result.todaySteps = todayEntry?.steps ?? 0;
            return result;
        }

        FoodUiData FindFood(string foodId)
        {
            foreach (var food in _data.Foods) if (food.id == foodId) return food;
            return null;
        }

        /// <summary>Buys food into the player's inventory (Shop's Food section). Feeding a companion
        /// with it is a separate step - see <see cref="FeedCompanion"/> - matching the design doc's
        /// split between "buy food in Shop" and "feed a companion from Companions".</summary>
        public FoodPurchaseResultDto PurchaseFood(string foodId, int quantity)
        {
            var result = new FoodPurchaseResultDto { foodId = foodId, quantity = quantity };
            if (quantity <= 0) return Fail(result, "Choose at least one item.");
            var food = FindFood(foodId);
            if (food == null) return Fail(result, "Unknown food item.");
            var cost = food.coinCost * quantity;
            if (_save.coins < cost) return Fail(result, "Not enough Coins.");
            _save.coins -= cost;
            _save.AddFood(foodId, quantity);
            _save.everPurchasedFood = true;
            result.success = true;
            result.coinsSpent = cost;
            return result;
        }

        /// <summary>Feeds one unit of an already-purchased food from inventory to an owned companion,
        /// granting that food's Growth EXP (design doc section 4 - EXP comes only from feeding).</summary>
        public FeedResultDto FeedCompanion(string foodId, string companionId)
        {
            var result = new FeedResultDto { foodId = foodId, companionId = companionId };
            var companion = _save.FindCompanion(companionId);
            if (companion == null || !companion.owned) return Fail(result, "Choose an owned companion.");
            var food = FindFood(foodId);
            if (food == null) return Fail(result, "Unknown food item.");
            if (!_save.TryConsumeFood(foodId, 1)) return Fail(result, "You're out of " + food.name + ". Buy more from the Shop.");

            var rosterEntry = CompanionRoster.Find(companionId);
            result.previousStage = StageFor(rosterEntry, companion.growthExperience);
            companion.growthExperience += food.growthExperience;
            _save.everFedCompanion = true;
            result.success = true;
            result.experienceGained = food.growthExperience;
            result.currentStage = StageFor(rosterEntry, companion.growthExperience);
            return result;
        }

        /// <summary>Buys a distance-unlocked companion with coins (design doc section 13 - a
        /// companion must be Unlocked before it can be Bought).</summary>
        public CompanionPurchaseResultDto PurchaseCompanion(string companionId)
        {
            var result = new CompanionPurchaseResultDto { companionId = companionId };
            var companion = _save.FindCompanion(companionId);
            var entry = CompanionRoster.Find(companionId);
            if (companion == null || string.IsNullOrEmpty(entry.Id)) return Fail(result, "Unknown companion.");
            if (companion.owned) return Fail(result, "You already own this companion.");
            if (!companion.unlocked) return Fail(result, "Walk further to unlock this companion first.");
            if (_save.coins < entry.PriceCoins) return Fail(result, "Not enough Coins.");
            _save.coins -= entry.PriceCoins;
            companion.owned = true;
            result.success = true;
            result.coinsSpent = entry.PriceCoins;
            return result;
        }

        /// <summary>Sets the player's active/lead companion - the only one that earns walking income.</summary>
        public bool SetLeadCompanion(string companionId)
        {
            var companion = _save.FindCompanion(companionId);
            if (companion == null || !companion.owned) return false;
            _save.leadCompanionId = companionId;
            return true;
        }

        /// <summary>Owned quantity of a food item, shared with the AR/3D feeding minigames
        /// (see <c>UiPrototypeRuntime.FoodQuantity</c>). Never negative.</summary>
        public int FoodQuantity(string foodId) => _save.FoodQuantity(foodId);

        /// <summary>Adds food to inventory outside of a Shop purchase (e.g. a Mission/Landmark reward).</summary>
        public void AddFood(string foodId, int amount) => _save.AddFood(foodId, amount);

        /// <summary>Spends one unit of a food item (an AR/3D throw actually picked up). Returns
        /// false when none are left, so the caller can decline the drag instead of going negative.</summary>
        public bool ConsumeFood(string foodId) => _save.TryConsumeFood(foodId, 1);

        /// <summary>Display-only coin rate for a flat Baby/Young/Adult stage, kept for any AR/3D
        /// surface that only knows the stage and not the specific companion. UI that knows the
        /// companion should prefer <see cref="IncomeOf"/>, which reflects that companion's real
        /// rarity-based income and is what <see cref="CompleteWalk"/> actually pays out.</summary>
        public static float WalkingIncomePer100m(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Baby: return 3.0f;
                case GrowthStage.Young: return 5.2f;
                default: return 8.0f;
            }
        }

        public LandmarkRewardDto CompleteLandmarkMemory(string landmarkId, string companionRewardId, DateTime utcNow)
        {
            var result = new LandmarkRewardDto { landmarkId = landmarkId };
            if (string.IsNullOrWhiteSpace(landmarkId) || _save.completedLandmarkIds.Contains(landmarkId)) return result;

            result.newlyCompleted = true;
            _save.completedLandmarkIds.Add(landmarkId);
            result.stampId = landmarkId + "-stamp";
            if (!_save.stamps.Exists(item => item != null && item.stampId == result.stampId))
                _save.stamps.Add(new StampData { stampId = result.stampId, landmarkId = landmarkId, collectedUtc = utcNow.ToUniversalTime().ToString("O") });

            if (!string.IsNullOrWhiteSpace(companionRewardId))
            {
                var rewardCompanion = _save.FindCompanion(companionRewardId);
                // A Landmark Pet is granted outright (Obtained), not merely distance-Unlocked
                // (design doc section 28 distinguishes the two events).
                if (rewardCompanion != null && !rewardCompanion.owned)
                {
                    rewardCompanion.unlocked = true;
                    rewardCompanion.owned = true;
                    result.unlockedCompanionId = companionRewardId;
                }
            }

            result.journeyId = "landmark-" + landmarkId;
            _save.journeys.Add(new JourneyEntryData
            {
                id = result.journeyId,
                landmarkId = landmarkId,
                title = landmarkId == PrototypeIds.CentralPostOffice ? "Central Post Office AR Memory" : "Landmark Memory",
                summary = "Collected a Landmark Stamp after completing the AR Memory.",
                createdUtc = utcNow.ToUniversalTime().ToString("O")
            });
            return result;
        }

        static FeedResultDto Fail(FeedResultDto result, string error) { result.error = error; return result; }
        static FoodPurchaseResultDto Fail(FoodPurchaseResultDto result, string error) { result.error = error; return result; }
        static CompanionPurchaseResultDto Fail(CompanionPurchaseResultDto result, string error) { result.error = error; return result; }
    }
}
