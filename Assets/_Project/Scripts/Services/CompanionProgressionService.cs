using System;
using System.Collections.Generic;

namespace ARWalking.UI
{
    public sealed class CompanionProgressionService
    {
        /// <summary>Daily walking-distance goal shown by the Activity Dashboard's progress bar and weekly chart.</summary>
        public const float DailyGoalKilometres = 5f;

        readonly PlayerSaveData _save;

        public CompanionProgressionService(PlayerSaveData save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _save.RepairCollections();
        }

        public static GrowthStage StageFor(int experience)
        {
            if (experience < 500) return GrowthStage.Baby;
            if (experience < 1500) return GrowthStage.Young;
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

        public List<string> CaptureUnlockedCompanionIds()
        {
            var result = new List<string>();
            foreach (var companion in _save.companions)
                if (companion.unlocked) result.Add(companion.companionId);
            return result;
        }

        public WalkResultDto CompleteWalk(WalkMetrics metrics)
        {
            return CompleteWalk(metrics, CaptureUnlockedCompanionIds(), DateTime.UtcNow);
        }

        public WalkResultDto CompleteWalk(WalkMetrics metrics, IReadOnlyCollection<string> companionsUnlockedBeforeWalk)
        {
            return CompleteWalk(metrics, companionsUnlockedBeforeWalk, DateTime.UtcNow);
        }

        public WalkResultDto CompleteWalk(WalkMetrics metrics, IReadOnlyCollection<string> companionsUnlockedBeforeWalk, DateTime utcNow)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            if (companionsUnlockedBeforeWalk == null) throw new ArgumentNullException(nameof(companionsUnlockedBeforeWalk));
            var eligibleIds = new HashSet<string>(companionsUnlockedBeforeWalk);
            var distance = Math.Max(0f, metrics.distanceKilometres);
            var wholeKilometres = (int)Math.Floor(distance + 0.00001f);
            var experience = wholeKilometres * 100;
            var result = new WalkResultDto
            {
                distanceKilometres = distance,
                hasSteps = metrics.hasSteps,
                steps = metrics.hasSteps ? Math.Max(0, metrics.steps) : 0,
                durationSeconds = Math.Max(0f, metrics.elapsedSeconds),
                completedKilometres = wholeKilometres,
                coinsAwarded = wholeKilometres * 30,
                experiencePerEligibleCompanion = experience
            };

            foreach (var companion in _save.companions)
            {
                if (!companion.unlocked || !eligibleIds.Contains(companion.companionId)) continue;
                companion.growthExperience += experience;
                result.rewardedCompanionIds.Add(companion.companionId);
            }

            _save.coins += result.coinsAwarded;
            _save.totalDistanceKilometres += distance;
            if (metrics.hasSteps)
            {
                _save.hasTotalSteps = true;
                _save.totalSteps += result.steps;
            }

            foreach (var entry in CompanionRoster.Entries)
            {
                if (float.IsPositiveInfinity(entry.UnlockDistanceKilometres)) continue; // Landmark-reward only
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

        public FeedResultDto PurchaseAndFeed(string foodId, string companionId)
        {
            var result = new FeedResultDto { foodId = foodId, companionId = companionId };
            var companion = _save.FindCompanion(companionId);
            if (companion == null || !companion.unlocked) return Fail(result, "Choose an unlocked companion.");

            int cost;
            int experience;
            switch (foodId)
            {
                case FoodCatalogIds.RiceBall: cost = 20; experience = 15; break;
                case FoodCatalogIds.ChickenLeg: cost = 40; experience = 40; break;
                default: return Fail(result, "Unknown food item.");
            }
            if (_save.coins < cost) return Fail(result, "Not enough Coins.");

            result.previousStage = StageFor(companion.growthExperience);
            _save.coins -= cost;
            companion.growthExperience += experience;
            AddFood(foodId, 1);
            result.success = true;
            result.coinsSpent = cost;
            result.experienceGained = experience;
            result.currentStage = StageFor(companion.growthExperience);
            return result;
        }

        /// <summary>Owned quantity of a food item, shared with the AR/3D feeding minigames
        /// (see <c>UiPrototypeRuntime.FoodQuantity</c>). Never negative.</summary>
        public int FoodQuantity(string foodId) => Math.Max(0, _save.FindFood(foodId)?.quantity ?? 0);

        public void AddFood(string foodId, int amount)
        {
            if (string.IsNullOrEmpty(foodId) || amount == 0) return;
            var entry = _save.FindFood(foodId);
            if (entry == null)
            {
                entry = new FoodInventoryData { foodId = foodId, quantity = 0 };
                _save.foodInventory.Add(entry);
            }
            entry.quantity = Math.Max(0, entry.quantity + amount);
        }

        /// <summary>Spends one unit of a food item (an AR/3D throw actually picked up). Returns
        /// false when none are left, so the caller can decline the drag instead of going negative.</summary>
        public bool ConsumeFood(string foodId)
        {
            var entry = _save.FindFood(foodId);
            if (entry == null || entry.quantity <= 0) return false;
            entry.quantity -= 1;
            return true;
        }

        /// <summary>Display-only coin rate shown on the Companion detail screen's "Walking
        /// income" row for the current lead companion - does not affect the actual per-walk
        /// coin award computed in <see cref="CompleteWalk"/>.</summary>
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
                if (rewardCompanion != null && !rewardCompanion.unlocked)
                {
                    rewardCompanion.unlocked = true;
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
    }
}
