using System;
using System.IO;
using System.Linq;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;

namespace ARWalking.Tests.EditMode
{
    public sealed class UiPrototypeEditModeTests
    {
        string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "ar-walking-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
        }

        /// <summary>Every economy test needs a food/companion catalog - reuse the real seeded
        /// catalog (already validated by RegeneratedCatalogAndTemporaryArtworkBindingsAreValid)
        /// rather than hand-building a stub one.</summary>
        static CompanionProgressionService NewService(PlayerSaveData save)
        {
            var catalog = Resources.Load<PrototypeUiCatalog>("UI/PrototypeUiCatalog");
            return new CompanionProgressionService(save, new StaticUiDataProvider(catalog));
        }

        [Test]
        public void RouteCatalogContainsThirteenScreensAndFourRoots()
        {
            // The AR migration merged LandmarkArMemory + ArPhoto into one PetAr route;
            // the Activity Dashboard remains a separate Home screen.
            Assert.That(UiRouteCatalog.All.Count, Is.EqualTo(13));
            Assert.That(UiRouteCatalog.All.Distinct().Count(), Is.EqualTo(13));
            Assert.That(Enum.GetValues(typeof(UiRootTab)).Length, Is.EqualTo(4));
            Assert.That(UiRouteCatalog.RootRoute(UiRootTab.Map), Is.EqualTo(UiRoute.HomeMap));
            Assert.That(UiRouteCatalog.RootRoute(UiRootTab.Companions), Is.EqualTo(UiRoute.CompanionCollection));
            Assert.That(UiRouteCatalog.RootRoute(UiRootTab.Shop), Is.EqualTo(UiRoute.ShopFood));
            Assert.That(UiRouteCatalog.RootRoute(UiRootTab.Journey), Is.EqualTo(UiRoute.JourneyList));
            Assert.That(UiRouteCatalog.RootFor(UiRoute.Pet3D), Is.EqualTo(UiRootTab.Companions));
        }

        [Test]
        public void NavigationRootsOverlayAndBackStackAreDeterministic()
        {
            var navigation = new UiNavigationStack();
            navigation.SwitchRoot(UiRootTab.Companions);
            navigation.Push(UiRoute.CompanionDetail);
            navigation.ShowOverlay(UiOverlay.Settings);
            Assert.That(navigation.Back(), Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(UiRoute.CompanionDetail));
            Assert.That(navigation.Back(), Is.True);
            Assert.That(navigation.CurrentRoute, Is.EqualTo(UiRoute.CompanionCollection));
            Assert.That(navigation.Back(), Is.False);
            navigation.Push(UiRoute.CompanionDetail);
            navigation.ResetToSetup();
            Assert.That(navigation.CurrentRoute, Is.EqualTo(UiRoute.OnboardingSetup));
            Assert.That(navigation.Back(), Is.False);
        }

        [Test]
        public void FirstLaunchDefaultsAndDisplayNameValidationAreCorrect()
        {
            Assert.That(PlayerSaveData.IsValidDisplayName("  Mai  "), Is.True);
            Assert.That(PlayerSaveData.IsValidDisplayName("   "), Is.False);
            Assert.That(PlayerSaveData.IsValidDisplayName(new string('a', 21)), Is.False);
            var save = PlayerSaveData.CreateNew("  Mai  ");
            Assert.That(save.displayName, Is.EqualTo("Mai"));
            Assert.That(save.setupComplete, Is.True);
            Assert.That(save.coins, Is.Zero);
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).unlocked, Is.True);
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).owned, Is.True, "The starter companion is owned outright, not just distance-unlocked.");
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).growthExperience, Is.Zero, "Walking grants no Growth EXP - only feeding does - so the starter begins at 0.");
            Assert.That(save.FindCompanion(PrototypeIds.Husky).unlocked, Is.False);
            Assert.That(save.FindCompanion(PrototypeIds.Husky).owned, Is.False);
            Assert.That(save.FindCompanion(PrototypeIds.Deer).unlocked, Is.False);
            Assert.That(save.leadCompanionId, Is.EqualTo(PrototypeIds.Corgi), "The starter is the default lead/active companion.");
        }

        [Test]
        public void LocalSaveRoundTripPreservesAllProfileCollections()
        {
            var path = Path.Combine(_temporaryDirectory, LocalPlayerSaveStore.FileName);
            var store = new LocalPlayerSaveStore(path);
            var save = PlayerSaveData.CreateNew("An");
            save.coins = 90; save.totalDistanceKilometres = 2.5f; save.hasTotalSteps = true; save.totalSteps = 3200;
            save.stamps.Add(new StampData { stampId="stamp", landmarkId="landmark" }); save.completedLandmarkIds.Add("landmark"); save.savedPhotoPaths.Add("photo.jpg");
            save.journeys.Add(new JourneyEntryData { id="journey", title="Test" });
            save.AddFood(FoodCatalogIds.RiceBall, 3);
            store.Save(save);
            var result = store.Load();
            Assert.That(result.status, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(result.save.displayName, Is.EqualTo("An"));
            Assert.That(result.save.coins, Is.EqualTo(90));
            Assert.That(result.save.totalSteps, Is.EqualTo(3200));
            Assert.That(result.save.stamps.Single().stampId, Is.EqualTo("stamp"));
            Assert.That(result.save.journeys.Single().id, Is.EqualTo("journey"));
            Assert.That(result.save.savedPhotoPaths.Single(), Is.EqualTo("photo.jpg"));
            // CreateNew grants 5 starter Rice Balls; this adds 3 more on top of that starting stock.
            Assert.That(result.save.FoodQuantity(FoodCatalogIds.RiceBall), Is.EqualTo(8));
        }

        [Test]
        public void CorruptSaveIsPreservedAndRecoveryReturnsNoProfile()
        {
            var path = Path.Combine(_temporaryDirectory, LocalPlayerSaveStore.FileName);
            File.WriteAllText(path, "{ definitely not valid json");
            var result = new LocalPlayerSaveStore(path).Load();
            Assert.That(result.status, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(result.save, Is.Null);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(result.backupPath, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(result.backupPath), Is.True);
        }

        [Test]
        public void PreV3SaveMigratesUnlockedCompanionsToOwnedAndDefaultsLead()
        {
            // Simulates a save written before the Unlocked/Owned split (schemaVersion 2): under
            // that schema "unlocked" alone meant the player already had the companion, so migration
            // must carry that forward as "owned" rather than making the player re-buy pets they had.
            var save = new PlayerSaveData { schemaVersion = 2, setupComplete = true, displayName = "Legacy" };
            save.companions.Add(new CompanionProgressData { companionId = PrototypeIds.Corgi, unlocked = true, growthExperience = 450 });
            save.companions.Add(new CompanionProgressData { companionId = PrototypeIds.Husky, unlocked = true, growthExperience = 0 });
            save.RepairCollections();
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).owned, Is.True);
            Assert.That(save.FindCompanion(PrototypeIds.Husky).owned, Is.True);
            Assert.That(save.FindCompanion(PrototypeIds.Fox).owned, Is.False, "A companion that was never unlocked under the old schema stays un-owned.");
            Assert.That(save.leadCompanionId, Is.EqualTo(PrototypeIds.Corgi));
        }

        [TestCase(0, GrowthStage.Baby, 0.70f)]
        [TestCase(499, GrowthStage.Baby, 0.70f)]
        [TestCase(500, GrowthStage.Young, 0.85f)]
        [TestCase(1499, GrowthStage.Young, 0.85f)]
        [TestCase(1500, GrowthStage.Adult, 1.00f)]
        public void GlobalGrowthStageBoundariesAndPlaceholderScalesAreExact(int experience, GrowthStage stage, float scale)
        {
            Assert.That(CompanionProgressionService.StageFor(experience), Is.EqualTo(stage));
            Assert.That(CompanionProgressionService.PlaceholderScaleFor(stage), Is.EqualTo(scale));
        }

        [Test]
        public void PerCompanionGrowthStageUsesThatCompanionsOwnExpThresholds()
        {
            var corgi = CompanionRoster.Find(PrototypeIds.Corgi); // Young=40, Adult=80
            Assert.That(CompanionProgressionService.StageFor(corgi, 0), Is.EqualTo(GrowthStage.Baby));
            Assert.That(CompanionProgressionService.StageFor(corgi, 40), Is.EqualTo(GrowthStage.Young));
            Assert.That(CompanionProgressionService.StageFor(corgi, 80), Is.EqualTo(GrowthStage.Adult));

            var horse = CompanionRoster.Find(PrototypeIds.Horse); // Legendary - Young=330, Adult=825
            Assert.That(CompanionProgressionService.StageFor(horse, 300), Is.EqualTo(GrowthStage.Baby));
            Assert.That(CompanionProgressionService.StageFor(horse, 500), Is.EqualTo(GrowthStage.Young));
            Assert.That(CompanionProgressionService.StageFor(horse, 825), Is.EqualTo(GrowthStage.Adult));
        }

        [TestCase(GrowthStage.Baby, 1.00f)]
        [TestCase(GrowthStage.Young, 1.15f)]
        [TestCase(GrowthStage.Adult, 1.30f)]
        public void IncomeOfAppliesTheGrowthMultiplierToBaseIncome(GrowthStage stage, float multiplier)
        {
            Assert.That(CompanionProgressionService.GrowthMultiplier(stage), Is.EqualTo(multiplier));
            var husky = CompanionRoster.Find(PrototypeIds.Husky); // base 4.5
            var experience = stage == GrowthStage.Adult ? husky.AdultExp : stage == GrowthStage.Young ? husky.YoungExp : 0;
            var expected = Mathf.Round(husky.BaseIncomePerHundredMetres * multiplier * 10f) / 10f;
            Assert.That(CompanionProgressionService.IncomeOf(husky, experience), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void CompleteWalkPaysOnlyTheLeadCompanionAndGrantsNoWalkingExp()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var result = NewService(save).CompleteWalk(new WalkMetrics
            {
                distanceKilometres = 1.25f, hasSteps = true, steps = 1600, elapsedSeconds = 1200f
            });
            // Corgi (lead, Baby, base 4.0 coins/100m): 4.0 * (1.25km -> 12.5 hundred-metre units) = 50.
            Assert.That(result.coinsAwarded, Is.EqualTo(50));
            Assert.That(result.leadCompanionId, Is.EqualTo(PrototypeIds.Corgi));
            Assert.That(save.coins, Is.EqualTo(50));
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).growthExperience, Is.Zero, "Walking never grants Growth EXP - only feeding does.");
            Assert.That(save.FindCompanion(PrototypeIds.Husky).unlocked, Is.True, "1.25 km total distance crosses Husky's 1 km unlock threshold.");
            Assert.That(save.FindCompanion(PrototypeIds.Husky).owned, Is.False, "Being distance-unlocked does not make a companion owned - it must still be bought.");
            Assert.That(result.newlyUnlockedCompanionIds, Does.Contain(PrototypeIds.Husky));
            Assert.That(save.totalSteps, Is.EqualTo(1600));
        }

        [Test]
        public void SubKilometreWalkStillEarnsProportionalCoins()
        {
            // Unlike the old flat "whole kilometres only" formula, the design-doc formula
            // (Coins = Distance/100m x Income) pays out continuously, not just at whole-km marks.
            var save = PlayerSaveData.CreateNew("Mai");
            var result = NewService(save).CompleteWalk(new WalkMetrics { distanceKilometres = .75f, elapsedSeconds = 300f });
            Assert.That(result.coinsAwarded, Is.EqualTo(30)); // 4.0 * 7.5 hundred-metre units
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).growthExperience, Is.Zero);
            Assert.That(save.totalDistanceKilometres, Is.EqualTo(.75f));
        }

        [Test]
        public void OnlyTheLeadCompanionEarnsWalkingIncomeNotEveryOwnedCompanion()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = NewService(save);
            save.coins = 100;
            save.FindCompanion(PrototypeIds.Husky).unlocked = true;
            Assert.That(service.PurchaseCompanion(PrototypeIds.Husky).success, Is.True); // owned, but Corgi remains lead
            var coinsBeforeWalk = save.coins;

            var result = service.CompleteWalk(new WalkMetrics { distanceKilometres = 1f });
            Assert.That(result.leadCompanionId, Is.EqualTo(PrototypeIds.Corgi));
            Assert.That(save.coins, Is.EqualTo(coinsBeforeWalk + 40)); // Corgi: 4.0 * 10 units
            Assert.That(save.FindCompanion(PrototypeIds.Husky).growthExperience, Is.Zero, "A non-lead owned companion earns nothing from this walk.");
        }

        [Test]
        public void PurchaseFoodThenFeedGrantsExpAndReportsStageChange()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = NewService(save);
            save.foodInventory.Clear(); // Isolate this test from the starter-treat grant RepairCollections just applied.

            Assert.That(service.FeedCompanion(FoodCatalogIds.RiceBall, PrototypeIds.Husky).error, Is.EqualTo("Choose an owned companion."));
            Assert.That(service.PurchaseFood(FoodCatalogIds.RiceBall, 1).error, Is.EqualTo("Not enough Coins."));

            save.coins = 50;
            var purchase = service.PurchaseFood(FoodCatalogIds.ChickenLeg, 1);
            Assert.That(purchase.success, Is.True);
            Assert.That(purchase.coinsSpent, Is.EqualTo(50));
            Assert.That(save.coins, Is.Zero);
            Assert.That(save.FoodQuantity(FoodCatalogIds.ChickenLeg), Is.EqualTo(1));

            var feed = service.FeedCompanion(FoodCatalogIds.ChickenLeg, PrototypeIds.Corgi);
            Assert.That(feed.success, Is.True);
            Assert.That(feed.experienceGained, Is.EqualTo(40));
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).growthExperience, Is.EqualTo(40));
            Assert.That(feed.StageChanged, Is.True, "Corgi's Young threshold is exactly 40 EXP.");
            Assert.That(feed.currentStage, Is.EqualTo(GrowthStage.Young));
            Assert.That(save.FoodQuantity(FoodCatalogIds.ChickenLeg), Is.Zero, "Feeding consumes the inventory unit.");

            Assert.That(service.FeedCompanion(FoodCatalogIds.ChickenLeg, PrototypeIds.Corgi).error, Does.StartWith("You're out of"));
        }

        [Test]
        public void PurchaseCompanionRequiresDistanceUnlockThenChargesCoins()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = NewService(save);

            Assert.That(service.PurchaseCompanion(PrototypeIds.Husky).error, Is.EqualTo("Walk further to unlock this companion first."));

            save.FindCompanion(PrototypeIds.Husky).unlocked = true;
            Assert.That(service.PurchaseCompanion(PrototypeIds.Husky).error, Is.EqualTo("Not enough Coins."));

            save.coins = 100;
            var result = service.PurchaseCompanion(PrototypeIds.Husky);
            Assert.That(result.success, Is.True);
            Assert.That(result.coinsSpent, Is.EqualTo(100));
            Assert.That(save.coins, Is.Zero);
            Assert.That(save.FindCompanion(PrototypeIds.Husky).owned, Is.True);
            Assert.That(service.PurchaseCompanion(PrototypeIds.Husky).error, Is.EqualTo("You already own this companion."));
        }

        [Test]
        public void SetLeadCompanionRequiresOwnershipAndPersistsChoice()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = NewService(save);
            Assert.That(service.SetLeadCompanion(PrototypeIds.Husky), Is.False, "Husky isn't owned yet.");
            Assert.That(save.leadCompanionId, Is.EqualTo(PrototypeIds.Corgi));

            save.coins = 100;
            save.FindCompanion(PrototypeIds.Husky).unlocked = true;
            Assert.That(service.PurchaseCompanion(PrototypeIds.Husky).success, Is.True);
            Assert.That(service.SetLeadCompanion(PrototypeIds.Husky), Is.True);
            Assert.That(save.leadCompanionId, Is.EqualTo(PrototypeIds.Husky));
        }

        [Test]
        public void CentralPostOfficeRewardGrantsDeerAsOwnedAndIsIdempotent()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = NewService(save);
            var first = service.CompleteLandmarkMemory(PrototypeIds.CentralPostOffice, PrototypeIds.Deer, new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc));
            var second = service.CompleteLandmarkMemory(PrototypeIds.CentralPostOffice, PrototypeIds.Deer, DateTime.UtcNow);
            Assert.That(first.newlyCompleted, Is.True);
            Assert.That(first.unlockedCompanionId, Is.EqualTo(PrototypeIds.Deer));
            Assert.That(save.FindCompanion(PrototypeIds.Deer).unlocked, Is.True);
            Assert.That(save.FindCompanion(PrototypeIds.Deer).owned, Is.True, "A Landmark Pet is Obtained outright, not merely Unlocked (design doc section 28).");
            Assert.That(save.stamps.Select(item => item.stampId), Is.EquivalentTo(new[] { PrototypeIds.CentralPostOfficeStamp }));
            Assert.That(save.journeys.Count, Is.EqualTo(1));
            Assert.That(second.newlyCompleted, Is.False);
            Assert.That(second.unlockedCompanionId, Is.Empty);
        }

        [Test]
        public void LandmarkRewardIsDataDrivenPerLandmarkNotHardcodedToOneId()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = NewService(save);

            // A landmark with no configured reward (empty companionRewardId) grants a Stamp but unlocks nothing.
            var noRewardResult = service.CompleteLandmarkMemory(PrototypeIds.IndependencePalace, string.Empty, DateTime.UtcNow);
            Assert.That(noRewardResult.newlyCompleted, Is.True);
            Assert.That(noRewardResult.stampId, Is.EqualTo(PrototypeIds.IndependencePalace + "-stamp"));
            Assert.That(noRewardResult.unlockedCompanionId, Is.Empty);

            // Any landmark id can carry any companion reward - it is no longer hardcoded to Central Post Office/Deer.
            var rewardResult = service.CompleteLandmarkMemory(PrototypeIds.NotreDameBasilica, PrototypeIds.Husky, DateTime.UtcNow);
            Assert.That(rewardResult.unlockedCompanionId, Is.EqualTo(PrototypeIds.Husky));
            Assert.That(save.FindCompanion(PrototypeIds.Husky).unlocked, Is.True);
            Assert.That(save.FindCompanion(PrototypeIds.Husky).owned, Is.True);
            Assert.That(save.journeys.Single(item => item.landmarkId == PrototypeIds.NotreDameBasilica).title,
                Is.EqualTo("Notre-Dame Basilica"));
        }

        [Test]
        public void CompleteWalkRecordsDailyActivityForWeeklyChart()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = NewService(save);
            var day = new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc);
            service.CompleteWalk(new WalkMetrics { distanceKilometres = 1.5f, hasSteps = true, steps = 2000 }, save.leadCompanionId, day);
            service.CompleteWalk(new WalkMetrics { distanceKilometres = 0.5f, hasSteps = true, steps = 700 }, save.leadCompanionId, day.AddHours(6));
            Assert.That(save.dailyActivity.Count, Is.EqualTo(1), "Two walks on the same UTC calendar day should accumulate into one entry.");
            var entry = save.dailyActivity.Single();
            Assert.That(entry.dateIso, Is.EqualTo("2026-09-02"));
            Assert.That(entry.distanceKilometres, Is.EqualTo(2.0f).Within(0.001f));
            Assert.That(entry.hasSteps, Is.True);
            Assert.That(entry.steps, Is.EqualTo(2700));

            service.CompleteWalk(new WalkMetrics { distanceKilometres = 1f }, save.leadCompanionId, day.AddDays(1));
            Assert.That(save.dailyActivity.Count, Is.EqualTo(2), "A walk on a different calendar day should create a separate entry.");
        }

        [Test]
        public void WeeklyActivitySummaryAlignsToMondayAndAveragesWeekToDate()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = NewService(save);
            var today = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);
            var monday = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
            var todayIndex = (int)(today.Date - monday.Date).TotalDays;

            service.CompleteWalk(new WalkMetrics { distanceKilometres = 4f }, save.leadCompanionId, monday);
            service.CompleteWalk(new WalkMetrics { distanceKilometres = 2f }, save.leadCompanionId, today);

            var weekly = service.GetWeeklyActivity(today);
            Assert.That(weekly.days.Length, Is.EqualTo(7));
            Assert.That(weekly.days[0].date.Date, Is.EqualTo(monday.Date));
            Assert.That(weekly.days[0].date.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(weekly.days[6].date.Date, Is.EqualTo(monday.AddDays(6).Date));
            Assert.That(weekly.days[0].distanceKilometres, Is.EqualTo(4f));
            Assert.That(weekly.days[todayIndex].isToday, Is.True);
            Assert.That(weekly.days[todayIndex].distanceKilometres, Is.EqualTo(2f));
            Assert.That(weekly.days.Count(item => item.isFuture), Is.EqualTo(6 - todayIndex), "Every day after today in this calendar week is future.");
            Assert.That(weekly.todayDistanceKilometres, Is.EqualTo(2f));
            Assert.That(weekly.dailyGoalKilometres, Is.EqualTo(CompanionProgressionService.DailyGoalKilometres));
            Assert.That(weekly.weeklyAverageKilometres, Is.EqualTo(6f / (todayIndex + 1)).Within(0.001f), "Average is over Monday..today only, not zero-padded across the full week.");
        }

        [Test]
        public void MockAndFriendProviderStubsSatisfyUnitContracts()
        {
            VerifyWalkProvider(new DeterministicWalkMetricsProvider());
            VerifyWalkProvider(new FriendWalkProviderStub());
            VerifyMapProvider(new DeterministicLandmarkMapProvider());
            VerifyMapProvider(new FriendMapProviderStub());
        }

        [Test]
        public void FirstTutorialWalkMissionCompletesAtExactlyTwentyMetres()
        {
            var save = PlayerSaveData.CreateNew("Mission Test");
            var catalog = Resources.Load<PrototypeUiCatalog>("UI/PrototypeUiCatalog");
            var missions = new MissionService(save, new StaticUiDataProvider(catalog), new DeterministicLandmarkMapProvider());

            save.totalDistanceKilometres = 0.019f;
            var active = missions.CurrentMission();
            Assert.That(active.missionId, Is.EqualTo("tutorial-walk"));
            Assert.That(active.targetValue, Is.EqualTo(0.02f));
            Assert.That(active.description, Is.EqualTo("Walk 20m with your companion."));
            Assert.That(active.status, Is.EqualTo(MissionStatus.Active));

            save.totalDistanceKilometres = 0.02f;
            Assert.That(missions.CurrentMission().status, Is.EqualTo(MissionStatus.Completed));
        }

        [Test]
        public void TutorialOrderIsWalkThenLevelUpThenShop_AndWalkingMilestonesDoubleFromFiveKm()
        {
            var save = PlayerSaveData.CreateNew("Mission Order Test");
            var catalog = Resources.Load<PrototypeUiCatalog>("UI/PrototypeUiCatalog");
            var dataProvider = new StaticUiDataProvider(catalog);
            var missions = new MissionService(save, dataProvider, new DeterministicLandmarkMapProvider());
            var progression = new CompanionProgressionService(save, dataProvider);
            var starterId = CompanionRoster.Entries[0].Id;

            save.totalDistanceKilometres = 0.02f;
            var walk = missions.CurrentMission();
            Assert.That(walk.missionId, Is.EqualTo("tutorial-walk"));
            Assert.That(walk.rewardLabel, Is.EqualTo("3x rice-ball"));
            var riceBallsBeforeClaim = save.FoodQuantity("rice-ball");
            Assert.That(missions.ClaimCurrent(), Is.True);
            Assert.That(save.FoodQuantity("rice-ball"), Is.EqualTo(riceBallsBeforeClaim + 3), "Fresh saves already start with a few rice balls (see PlayerSaveData.RepairCollections) - the mission adds 3 more on top.");

            var levelUp = missions.CurrentMission();
            Assert.That(levelUp.missionId, Is.EqualTo("tutorial-levelup"));
            Assert.That(missions.CurrentMission().status, Is.Not.EqualTo(MissionStatus.Completed), "The Corgi hasn't been fed yet - levelling up must not be free.");
            // Feeding exactly the 3 rice balls the walk mission just granted (15 EXP each) is enough
            // to cross the Corgi's 40 EXP Young threshold - matches the "feed all 3 rice balls" design.
            for (var i = 0; i < 3; i++) Assert.That(progression.FeedCompanion(FoodCatalogIds.RiceBall, starterId).success, Is.True);
            Assert.That(missions.CurrentMission().status, Is.EqualTo(MissionStatus.Completed));
            Assert.That(missions.ClaimCurrent(), Is.True);
            Assert.That(save.coins, Is.EqualTo(20));

            var shop = missions.CurrentMission();
            Assert.That(shop.missionId, Is.EqualTo("tutorial-shop"));
            save.everPurchasedFood = true;
            Assert.That(missions.CurrentMission().status, Is.EqualTo(MissionStatus.Completed));
            Assert.That(missions.ClaimCurrent(), Is.True);
            Assert.That(save.coins, Is.EqualTo(30));
            Assert.That(save.tutorialComplete, Is.True);

            var firstMilestone = missions.CurrentMission();
            Assert.That(firstMilestone.type, Is.EqualTo(MissionType.Walking));
            Assert.That(firstMilestone.targetValue, Is.EqualTo(5f));
            Assert.That(firstMilestone.rewardLabel, Is.EqualTo("20 coins"));
            save.totalDistanceKilometres = 5f;
            Assert.That(missions.ClaimCurrent(), Is.True);
            Assert.That(save.coins, Is.EqualTo(50));

            var secondMilestone = missions.CurrentMission();
            Assert.That(secondMilestone.targetValue, Is.EqualTo(10f), "Milestones double: 5km -> 10km.");
        }

        [Test]
        public void RegeneratedCatalogAndTemporaryArtworkBindingsAreValid()
        {
            var catalog = Resources.Load<PrototypeUiCatalog>("UI/PrototypeUiCatalog");
            var library = Resources.Load<PrototypeUiAssets>("UI/PrototypeUiAssets");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.companions.Select(item => item.id),
                Is.EquivalentTo(CompanionRoster.Entries.Select(e => e.Id)));
            Assert.That(catalog.foods.Count, Is.EqualTo(2));
            Assert.That(catalog.landmarks.Count, Is.EqualTo(4));
            var geoCatalog = Resources.Load<LandmarkGeoCatalog>("UI/LandmarkGeoCatalog");
            Assert.That(geoCatalog, Is.Not.Null);
            Assert.That(geoCatalog.landmarks.All(item =>
                item.unlockRadiusMeters == LandmarkGeoData.DefaultUnlockRadiusMeters), Is.True,
                "Every production Landmark must use the shared 500 m mission/scan radius.");
            var independencePalace = catalog.landmarks.Single(item => item.id == PrototypeIds.IndependencePalace);
            Assert.That(independencePalace.name, Is.EqualTo("Independence Palace"));
            Assert.That(independencePalace.history, Is.Not.Empty);
            Assert.That(independencePalace.architecture, Is.Not.Empty);
            Assert.That(independencePalace.imageTargetReady, Is.False);
            var palaceMarker = catalog.markers.Single(item => item.targetId == PrototypeIds.IndependencePalace);
            Assert.That(palaceMarker.label, Is.EqualTo("Independence Palace"));
            Assert.That(palaceMarker.normalizedPosition, Is.EqualTo(new Vector2(0.32f, 0.45f)));
            Assert.That(catalog.landmarks.Single(item => item.id == PrototypeIds.CentralPostOffice).imageTargetReady, Is.True);
            Assert.That(catalog.landmarks.Single(item => item.id == PrototypeIds.CentralPostOffice).companionRewardId, Is.EqualTo(PrototypeIds.Deer));
            var notreDame = catalog.landmarks.Single(item => item.id == PrototypeIds.NotreDameBasilica);
            Assert.That(notreDame.imageTargetReady, Is.True);
            Assert.That(notreDame.companionRewardId, Is.EqualTo(PrototypeIds.Bull));
            Assert.That(notreDame.missionClue, Does.Contain("illustrated image"));
            Assert.That(notreDame.missionHint, Is.EqualTo("Near the entrance"));
            var landmark81 = catalog.landmarks.Single(item => item.id == PrototypeIds.Landmark81);
            Assert.That(landmark81.imageTargetReady, Is.True);
            Assert.That(landmark81.companionRewardId, Is.EqualTo(PrototypeIds.Stag));
            Assert.That(landmark81.missionClue, Does.Contain("at sunset"));
            Assert.That(landmark81.missionHint, Is.EqualTo("Around the building"));
            Assert.That(Resources.Load<Texture2D>("UI/MissionClues/" + PrototypeIds.NotreDameBasilica), Is.Not.Null);
            Assert.That(Resources.Load<Texture2D>("UI/MissionClues/" + PrototypeIds.Landmark81), Is.Not.Null);
            Assert.That(catalog.markers.Single(item => item.targetId == PrototypeIds.Landmark81).label, Is.EqualTo("Landmark 81"));
            Assert.That(library, Is.Not.Null);
            Assert.That(library.companions.Length, Is.EqualTo(CompanionRoster.Entries.Length));
            Assert.That(library.archivedPlantPlaceholders.Length, Is.EqualTo(3));
            Assert.That(library.landmarks.Length, Is.EqualTo(4));
            Assert.That(new[]
            {
                library.iconAr, library.iconBack, library.iconCalendar, library.iconCamera,
                library.iconClose, library.iconCompass, library.iconHelp, library.iconJourney,
                library.iconLocation, library.iconMap, library.iconCompanions, library.iconSettings,
                library.iconShop, library.iconSteps
            }.All(icon => icon != null), Is.True, "Every preserved prototype icon must have a stable named binding.");
        }

        static void VerifyWalkProvider(IWalkMetricsProvider provider)
        {
            provider.StartWalk();
            Assert.That(provider.IsWalking, Is.True);
            Assert.That(IntegrationProviderContract.IsValid(provider.GetLiveMetrics()), Is.True);
            Assert.That(IntegrationProviderContract.IsValid(provider.StopWalk()), Is.True);
            Assert.That(provider.IsWalking, Is.False);
        }

        static void VerifyMapProvider(ILandmarkMapProvider provider)
        {
            Assert.That(IntegrationProviderContract.IsValid(provider.GetMapState()), Is.True);
            Assert.That(IntegrationProviderContract.IsValid(provider.GetLandmarkProximity(PrototypeIds.CentralPostOffice)), Is.True);
        }

        sealed class FriendWalkProviderStub : IWalkMetricsProvider
        {
            public bool IsWalking { get; private set; }
            public void StartWalk() { IsWalking = true; }
            public WalkMetrics GetLiveMetrics() => new WalkMetrics { distanceKilometres=.2f, hasSteps=false, elapsedSeconds=60f };
            public WalkMetrics StopWalk() { IsWalking = false; return new WalkMetrics { distanceKilometres=1f, hasSteps=true, steps=1300, elapsedSeconds=600f }; }
        }

        sealed class FriendMapProviderStub : ILandmarkMapProvider
        {
            public LandmarkMapState GetMapState() => new LandmarkMapState { hasPlayerPosition=true, playerNormalizedPosition=new Vector2(.5f, .5f), mapHeadingDegrees=90f };
            public LandmarkProximity GetLandmarkProximity(string landmarkId) => new LandmarkProximity { landmarkId=landmarkId, distanceMetres=25f, directionDegrees=45f, isWithinUnlockRadius=true };
        }
    }
}
