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

        [Test]
        public void RouteCatalogContainsTwelveScreensAndFourRoots()
        {
            // The AR migration merged LandmarkArMemory + ArPhoto into one PetAr route;
            // the Activity Dashboard remains a separate Home screen.
            Assert.That(UiRouteCatalog.All.Count, Is.EqualTo(12));
            Assert.That(UiRouteCatalog.All.Distinct().Count(), Is.EqualTo(12));
            Assert.That(Enum.GetValues(typeof(UiRootTab)).Length, Is.EqualTo(4));
            Assert.That(UiRouteCatalog.RootRoute(UiRootTab.Map), Is.EqualTo(UiRoute.HomeMap));
            Assert.That(UiRouteCatalog.RootRoute(UiRootTab.Companions), Is.EqualTo(UiRoute.CompanionCollection));
            Assert.That(UiRouteCatalog.RootRoute(UiRootTab.Shop), Is.EqualTo(UiRoute.ShopFood));
            Assert.That(UiRouteCatalog.RootRoute(UiRootTab.Journey), Is.EqualTo(UiRoute.JourneyList));
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
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).growthExperience, Is.EqualTo(450));
            Assert.That(save.FindCompanion(PrototypeIds.Husky).unlocked, Is.False);
            Assert.That(save.FindCompanion(PrototypeIds.Deer).unlocked, Is.False);
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
            store.Save(save);
            var result = store.Load();
            Assert.That(result.status, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(result.save.displayName, Is.EqualTo("An"));
            Assert.That(result.save.coins, Is.EqualTo(90));
            Assert.That(result.save.totalSteps, Is.EqualTo(3200));
            Assert.That(result.save.stamps.Single().stampId, Is.EqualTo("stamp"));
            Assert.That(result.save.journeys.Single().id, Is.EqualTo("journey"));
            Assert.That(result.save.savedPhotoPaths.Single(), Is.EqualTo("photo.jpg"));
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

        [TestCase(0, GrowthStage.Baby, 0.70f)]
        [TestCase(499, GrowthStage.Baby, 0.70f)]
        [TestCase(500, GrowthStage.Young, 0.85f)]
        [TestCase(1499, GrowthStage.Young, 0.85f)]
        [TestCase(1500, GrowthStage.Adult, 1.00f)]
        public void GrowthStageBoundariesAndPlaceholderScalesAreExact(int experience, GrowthStage stage, float scale)
        {
            Assert.That(CompanionProgressionService.StageFor(experience), Is.EqualTo(stage));
            Assert.That(CompanionProgressionService.PlaceholderScaleFor(stage), Is.EqualTo(scale));
        }

        [Test]
        public void WalkRewardsOnlyPreviouslyUnlockedCompanionsAndUnlocksHuskyAtOneKilometre()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var result = new CompanionProgressionService(save).CompleteWalk(new WalkMetrics
            {
                distanceKilometres = 1.25f, hasSteps = true, steps = 1600, elapsedSeconds = 1200f
            });
            Assert.That(result.coinsAwarded, Is.EqualTo(30));
            Assert.That(save.coins, Is.EqualTo(30));
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).growthExperience, Is.EqualTo(550));
            Assert.That(save.FindCompanion(PrototypeIds.Husky).unlocked, Is.True);
            Assert.That(save.FindCompanion(PrototypeIds.Husky).growthExperience, Is.Zero, "Husky was locked before this walk");
            Assert.That(save.FindCompanion(PrototypeIds.Deer).growthExperience, Is.Zero);
            Assert.That(result.rewardedCompanionIds, Is.EquivalentTo(new[] { PrototypeIds.Corgi }));
            Assert.That(result.newlyUnlockedCompanionIds, Does.Contain(PrototypeIds.Husky));
            Assert.That(save.totalSteps, Is.EqualTo(1600));
        }

        [Test]
        public void SubKilometreWalkAddsDistanceButNoDiscreteRewards()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var result = new CompanionProgressionService(save).CompleteWalk(new WalkMetrics { distanceKilometres = .75f, elapsedSeconds = 300f });
            Assert.That(result.coinsAwarded, Is.Zero);
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).growthExperience, Is.EqualTo(450));
            Assert.That(save.totalDistanceKilometres, Is.EqualTo(.75f));
        }

        [Test]
        public void CompanionUnlockedDuringWalkDoesNotReceiveThatWalkExperience()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = new CompanionProgressionService(save);
            var eligibleAtStart = service.CaptureUnlockedCompanionIds();
            save.FindCompanion(PrototypeIds.Deer).unlocked = true;
            var result = service.CompleteWalk(new WalkMetrics { distanceKilometres = 1f }, eligibleAtStart);
            Assert.That(result.rewardedCompanionIds, Is.EquivalentTo(new[] { PrototypeIds.Corgi }));
            Assert.That(save.FindCompanion(PrototypeIds.Deer).growthExperience, Is.Zero);
        }

        [Test]
        public void FoodValidatesCoinsAndLockedCompanionsAndReportsStageChange()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = new CompanionProgressionService(save);
            Assert.That(service.PurchaseAndFeed("basic-food", PrototypeIds.Husky).success, Is.False);
            Assert.That(service.PurchaseAndFeed("basic-food", PrototypeIds.Corgi).error, Is.EqualTo("Not enough Coins."));
            save.coins = 40;
            var result = service.PurchaseAndFeed("better-food", PrototypeIds.Corgi);
            Assert.That(result.success, Is.True);
            Assert.That(result.coinsSpent, Is.EqualTo(40));
            Assert.That(save.FindCompanion(PrototypeIds.Corgi).growthExperience, Is.EqualTo(490));
            save.coins = 20;
            result = service.PurchaseAndFeed("basic-food", PrototypeIds.Corgi);
            Assert.That(result.StageChanged, Is.True);
            Assert.That(result.currentStage, Is.EqualTo(GrowthStage.Young));
        }

        [Test]
        public void CentralPostOfficeRewardUnlocksDeerAndIsIdempotent()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = new CompanionProgressionService(save);
            var first = service.CompleteLandmarkMemory(PrototypeIds.CentralPostOffice, PrototypeIds.Deer, new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc));
            var second = service.CompleteLandmarkMemory(PrototypeIds.CentralPostOffice, PrototypeIds.Deer, DateTime.UtcNow);
            Assert.That(first.newlyCompleted, Is.True);
            Assert.That(first.unlockedCompanionId, Is.EqualTo(PrototypeIds.Deer));
            Assert.That(save.FindCompanion(PrototypeIds.Deer).unlocked, Is.True);
            Assert.That(save.stamps.Select(item => item.stampId), Is.EquivalentTo(new[] { PrototypeIds.CentralPostOfficeStamp }));
            Assert.That(save.journeys.Count, Is.EqualTo(1));
            Assert.That(second.newlyCompleted, Is.False);
            Assert.That(second.unlockedCompanionId, Is.Empty);
        }

        [Test]
        public void LandmarkRewardIsDataDrivenPerLandmarkNotHardcodedToOneId()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = new CompanionProgressionService(save);

            // A landmark with no configured reward (empty companionRewardId) grants a Stamp but unlocks nothing.
            var noRewardResult = service.CompleteLandmarkMemory(PrototypeIds.IndependencePalace, string.Empty, DateTime.UtcNow);
            Assert.That(noRewardResult.newlyCompleted, Is.True);
            Assert.That(noRewardResult.stampId, Is.EqualTo(PrototypeIds.IndependencePalace + "-stamp"));
            Assert.That(noRewardResult.unlockedCompanionId, Is.Empty);

            // Any landmark id can carry any companion reward - it is no longer hardcoded to Central Post Office/Deer.
            var rewardResult = service.CompleteLandmarkMemory(PrototypeIds.NotreDameBasilica, PrototypeIds.Husky, DateTime.UtcNow);
            Assert.That(rewardResult.unlockedCompanionId, Is.EqualTo(PrototypeIds.Husky));
            Assert.That(save.FindCompanion(PrototypeIds.Husky).unlocked, Is.True);
        }

        [Test]
        public void CompleteWalkRecordsDailyActivityForWeeklyChart()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = new CompanionProgressionService(save);
            var day = new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc);
            service.CompleteWalk(new WalkMetrics { distanceKilometres = 1.5f, hasSteps = true, steps = 2000 }, service.CaptureUnlockedCompanionIds(), day);
            service.CompleteWalk(new WalkMetrics { distanceKilometres = 0.5f, hasSteps = true, steps = 700 }, service.CaptureUnlockedCompanionIds(), day.AddHours(6));
            Assert.That(save.dailyActivity.Count, Is.EqualTo(1), "Two walks on the same UTC calendar day should accumulate into one entry.");
            var entry = save.dailyActivity.Single();
            Assert.That(entry.dateIso, Is.EqualTo("2026-09-02"));
            Assert.That(entry.distanceKilometres, Is.EqualTo(2.0f).Within(0.001f));
            Assert.That(entry.hasSteps, Is.True);
            Assert.That(entry.steps, Is.EqualTo(2700));

            service.CompleteWalk(new WalkMetrics { distanceKilometres = 1f }, service.CaptureUnlockedCompanionIds(), day.AddDays(1));
            Assert.That(save.dailyActivity.Count, Is.EqualTo(2), "A walk on a different calendar day should create a separate entry.");
        }

        [Test]
        public void WeeklyActivitySummaryAlignsToMondayAndAveragesWeekToDate()
        {
            var save = PlayerSaveData.CreateNew("Mai");
            var service = new CompanionProgressionService(save);
            var today = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);
            var monday = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
            var todayIndex = (int)(today.Date - monday.Date).TotalDays;

            service.CompleteWalk(new WalkMetrics { distanceKilometres = 4f }, service.CaptureUnlockedCompanionIds(), monday);
            service.CompleteWalk(new WalkMetrics { distanceKilometres = 2f }, service.CaptureUnlockedCompanionIds(), today);

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
        public void RegeneratedCatalogAndTemporaryArtworkBindingsAreValid()
        {
            var catalog = Resources.Load<PrototypeUiCatalog>("UI/PrototypeUiCatalog");
            var library = Resources.Load<PrototypeUiAssets>("UI/PrototypeUiAssets");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.companions.Select(item => item.id),
                Is.EquivalentTo(CompanionRoster.Entries.Select(e => e.Id)));
            Assert.That(catalog.foods.Count, Is.EqualTo(2));
            Assert.That(catalog.landmarks.Count, Is.EqualTo(3));
            Assert.That(catalog.landmarks.Single(item => item.id == PrototypeIds.CentralPostOffice).imageTargetReady, Is.True);
            Assert.That(catalog.landmarks.Single(item => item.id == PrototypeIds.CentralPostOffice).companionRewardId, Is.EqualTo(PrototypeIds.Deer));
            Assert.That(catalog.landmarks.Where(item => item.id != PrototypeIds.CentralPostOffice).Select(item => item.companionRewardId),
                Is.All.Null.Or.Empty, "Only Central Post Office is configured with a companion reward today.");
            Assert.That(library, Is.Not.Null);
            Assert.That(library.companions.Length, Is.EqualTo(CompanionRoster.Entries.Length));
            Assert.That(library.archivedPlantPlaceholders.Length, Is.EqualTo(3));
            Assert.That(library.landmarks.Length, Is.EqualTo(3));
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
