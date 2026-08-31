using System;
using System.Collections.Generic;

namespace ARWalking.UI
{
    public sealed class CompanionProgressionService
    {
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
            return CompleteWalk(metrics, CaptureUnlockedCompanionIds());
        }

        public WalkResultDto CompleteWalk(WalkMetrics metrics, IReadOnlyCollection<string> companionsUnlockedBeforeWalk)
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

            var cat = _save.FindCompanion(PrototypeIds.Cat);
            if (!cat.unlocked && _save.totalDistanceKilometres >= 1f)
            {
                cat.unlocked = true;
                result.newlyUnlockedCompanionIds.Add(PrototypeIds.Cat);
            }
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
                case "basic-food": cost = 20; experience = 20; break;
                case "better-food": cost = 40; experience = 40; break;
                default: return Fail(result, "Unknown food item.");
            }
            if (_save.coins < cost) return Fail(result, "Not enough Coins.");

            result.previousStage = StageFor(companion.growthExperience);
            _save.coins -= cost;
            companion.growthExperience += experience;
            result.success = true;
            result.coinsSpent = cost;
            result.experienceGained = experience;
            result.currentStage = StageFor(companion.growthExperience);
            return result;
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
