using System;
using System.Collections;
using System.IO;
using System.Linq;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ARWalking.Tests.PlayMode
{
    public sealed class UiPrototypePlayModeTests
    {
        // Real providers (walk/map/webview bridge) are UiPrototypeRuntime's default unless overridden - these
        // tests assume the deterministic mock behavior (e.g. a fixed coinsAwarded, or the v0 illustrated-map
        // markup this suite's anatomy test checks for), so every test in this suite pins all three explicitly.
        // Init deliberately calls onError immediately: an unresolved bridge (IsAvailable staying true forever)
        // would route BuildMap() into the real WebView path instead of the illustrated-map fallback these tests
        // expect.
        sealed class DeterministicFailingWebViewBridge : IWebViewBridge
        {
            public bool IsInitialized => true;
            public void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded) => onError("test: no network");
            public void SetMargins(int left, int top, int right, int bottom) { }
            public void SetVisibility(bool visible) { }
            public void LoadURL(string url) { }
            public void EvaluateJS(string js) { }
        }

        string _savePath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UiPrototypeRuntime.Instance != null)
            {
                UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject);
                yield return null;
            }
            _savePath = Path.Combine(Path.GetTempPath(), "ar-walking-play-" + Guid.NewGuid().ToString("N"), LocalPlayerSaveStore.FileName);
            UiPrototypeRuntime.ClearTestOverrides();
            UiPrototypeRuntime.TestSavePathOverride = _savePath;
            UiPrototypeRuntime.TestWalkProviderOverride = new DeterministicWalkMetricsProvider();
            UiPrototypeRuntime.TestMapProviderOverride = new DeterministicLandmarkMapProvider();
            UiPrototypeRuntime.TestWebViewBridgeOverride = new DeterministicFailingWebViewBridge();
            SceneManager.LoadScene("Home");
            yield return WaitForScene("Home");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (UiPrototypeRuntime.Instance != null) UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject);
            yield return null;
            UiPrototypeRuntime.ClearTestOverrides();
            var directory = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator FirstLaunchSetupCreatesProfileAndOpensHome()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.OnboardingSetup));
            Assert.That(home.CompleteSetup("  Lan  "), Is.True);
            yield return null;
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.HomeMap));
            Assert.That(UiPrototypeRuntime.Instance.SaveData.displayName, Is.EqualTo("Lan"));
            Assert.That(File.Exists(_savePath), Is.True);
        }

        [UnityTest]
        public IEnumerator FourTabsWalkResultCompanionDetailAndFeedWork()
        {
            var home = CreateProfile();
            home.SelectRoot(UiRootTab.Map); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.HomeMap));
            home.BeginWalk(); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.ActiveWalk));
            var result = home.FinishWalk(); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.WalkResult));
            // Corgi is the default lead companion (Baby, base 4.0 coins/100m); the deterministic
            // walk provider's default result is 1 km, so 4.0 * 10 hundred-metre units = 40.
            Assert.That(result.coinsAwarded, Is.EqualTo(40));
            Assert.That(result.leadCompanionId, Is.EqualTo(PrototypeIds.Corgi));
            home.SelectRoot(UiRootTab.Companions); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.CompanionCollection));
            home.Navigate(UiRoute.CompanionDetail); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.CompanionDetail));
            home.SelectRoot(UiRootTab.Shop); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.ShopFood));
            var feed = home.Feed(FoodCatalogIds.RiceBall, PrototypeIds.Corgi);
            Assert.That(feed.success, Is.True);
            Assert.That(UiPrototypeRuntime.Instance.SaveData.coins, Is.EqualTo(20));
            home.SelectRoot(UiRootTab.Journey); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.JourneyList));
            yield return null;
        }

        [UnityTest]
        public IEnumerator JourneyScanCreatesStandaloneScannerUiAfterSceneTransition()
        {
            var home = CreateProfile();
            home.SelectRoot(UiRootTab.Journey);
            yield return null;

            UiPrototypeRuntime.Instance.EnterLandmarkScan(PrototypeIds.NotreDameBasilica);
            yield return WaitForScene("LandmarkScan");
            yield return null;

            var scanUi = GameObject.Find("Landmark Scan UI");
            Assert.That(scanUi, Is.Not.Null,
                "Opening Scan from Journey must bootstrap its UI after the LandmarkScan scene loads.");
            var root = scanUi.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q("landmark-scan-page"), Is.Not.Null);
            Assert.That(root.Q("ar-scanning-frame"), Is.Not.Null);
            Assert.That(LandmarkScanSceneContext.RequestedLandmarkId, Is.EqualTo(PrototypeIds.NotreDameBasilica));
            Assert.That(root.Q("landmark-scan-title-pill").Q<Label>().text, Is.EqualTo("Scan Notre-Dame Basilica"));

            GameObject xrOrigin = GameObject.Find("XR Origin");
            xrOrigin.SendMessage("SimulateRecognitionForEditor", SendMessageOptions.RequireReceiver);
            yield return null;
            Assert.That(root.Q("landmark-scan-result-sheet"), Is.Null,
                "Recognition alone must show the 3D landmark model first, not the info card yet.");
            Assert.That(root.Q("ar-scanning-frame"), Is.Null,
                "The scanning frame should be gone once the target is recognized.");

            xrOrigin.SendMessage("SimulateContentTapForEditor", SendMessageOptions.RequireReceiver);
            yield return null;
            Assert.That(root.Q("landmark-scan-result-sheet"), Is.Not.Null,
                "Tapping the recognized landmark model must reveal the cultural-memory sheet.");
            var rewardImage = root.Q<UnityEngine.UIElements.Image>("landmark-reward-image");
            Assert.That(rewardImage, Is.Not.Null);
            Assert.That(rewardImage.image, Is.Not.Null, "The Bull reward must be visible after recognition.");
        }

        [UnityTest]
        public IEnumerator Landmark81ScanShowsItsStoryAndStagReward()
        {
            var home = CreateProfile();
            home.SelectRoot(UiRootTab.Journey);
            yield return null;

            UiPrototypeRuntime.Instance.EnterLandmarkScan();
            yield return WaitForScene("LandmarkScan");
            yield return null;

            GameObject xrOrigin = GameObject.Find("XR Origin");
            Assert.That(xrOrigin, Is.Not.Null);
            xrOrigin.SendMessage("SimulateLandmark81RecognitionForEditor", SendMessageOptions.RequireReceiver);
            yield return null;
            xrOrigin.SendMessage("SimulateContentTapForEditor", SendMessageOptions.RequireReceiver);
            yield return null;

            var scanUi = GameObject.Find("Landmark Scan UI");
            Assert.That(scanUi, Is.Not.Null);
            var root = scanUi.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q("landmark-scan-result-sheet"), Is.Not.Null);
            Assert.That(root.Q<UnityEngine.UIElements.Button>("landmark-scan-result-close"), Is.Not.Null);
            Assert.That(root.Q<Label>("landmark-history").text, Does.Contain("461.2 metres"));
            Assert.That(root.Q<Label>("landmark-reward-name").text, Is.EqualTo("Stag"));
            Assert.That(root.Q<UnityEngine.UIElements.Image>("landmark-reward-image").image, Is.Not.Null,
                "The Stag reward must be visible after recognizing Landmark 81.");

            scanUi.SendMessage("CloseInfoCard", SendMessageOptions.RequireReceiver);
            yield return null;
            Assert.That(root.Q("landmark-scan-result-sheet"), Is.Null,
                "Closing the info sheet should return to the unobstructed tracked model view.");
            Assert.That(root.Q(className: "ar-scan-controls"), Is.Not.Null);
            xrOrigin.SendMessage("SimulateContentTapForEditor", SendMessageOptions.RequireReceiver);
            yield return null;
            Assert.That(root.Q("landmark-scan-result-sheet"), Is.Not.Null,
                "Tapping the tracked model again should reopen the Landmark information.");

            scanUi.SendMessage("CollectReward", SendMessageOptions.RequireReceiver);
            yield return null;
            Assert.That(root.Q("landmark-memory-unlocked-sheet"), Is.Not.Null,
                "Claiming a new Landmark reward should offer an optional companion photo before leaving the scanner.");
            Assert.That(root.Q<UnityEngine.UIElements.Button>("take-memory-photo")?.text, Is.EqualTo("Take a Photo"));
            Assert.That(root.Q<UnityEngine.UIElements.Button>("maybe-later")?.text, Is.EqualTo("Maybe Later"));
            Assert.That(UiPrototypeRuntime.Instance.SaveData.stamps.Single().landmarkId, Is.EqualTo(PrototypeIds.Landmark81));
            Assert.That(UiPrototypeRuntime.Instance.SaveData.journeys.Single().landmarkId, Is.EqualTo(PrototypeIds.Landmark81));

            string previousLeadPetId = UiPrototypeRuntime.Instance.PrimaryCompanionId();
            scanUi.SendMessage("TakeMemoryPhoto", SendMessageOptions.RequireReceiver);
            yield return null;
            Assert.That(root.Q("landmark-photo-pet-choice-sheet"), Is.Not.Null);
            Assert.That(root.Q<UnityEngine.UIElements.Button>("use-reward-pet-for-photo")?.text, Is.EqualTo("Use Stag"));
            Assert.That(root.Q<UnityEngine.UIElements.Button>("keep-current-lead-for-photo")?.text,
                Is.EqualTo("Keep " + UiPrototypeRuntime.Instance.Data.Companions
                    .Single(companion => companion.id == previousLeadPetId).name));

            scanUi.SendMessage("UseRewardPetForPhoto", SendMessageOptions.RequireReceiver);
            yield return WaitForScene("PetAr");
            Assert.That(PetArSceneContext.PetId, Is.EqualTo(PrototypeIds.Stag));
            Assert.That(UiPrototypeRuntime.Instance.LeadCompanionId, Is.EqualTo(PrototypeIds.Stag),
                "Accepting the prompt should set the newly received companion as lead before opening AR.");
            Assert.That(PetArSceneContext.IsPhotoMode, Is.True);
            Assert.That(PetArSceneContext.LandmarkId, Is.EqualTo(PrototypeIds.Landmark81),
                "The captured photo must link back to the Landmark 81 Journey entry.");
            Assert.That(UnityEngine.Object.FindFirstObjectByType<WalkUiController>().HasLandmarkMemory, Is.False,
                "Photo follow-up must show the companion camera without reopening the Landmark story overlay.");
            PetArSceneContext.PetId = null;
            PetArSceneContext.IsPhotoMode = false;
            PetArSceneContext.LandmarkId = null;
        }

        [UnityTest]
        public IEnumerator LatestLandmarkStampStaysAtTopOfJourneyAndPhotoRemainsInSeparateGallery()
        {
            var home = CreateProfile();
            UiPrototypeRuntime.Instance.CompleteLandmarkMemory(PrototypeIds.Landmark81);
            PetArSceneContext.LandmarkId = PrototypeIds.Landmark81;
            UiPrototypeRuntime.Instance.SaveArPhoto(Path.Combine(Path.GetDirectoryName(_savePath), "landmark-81-memory.png"));

            home.SelectRoot(UiRootTab.Journey);
            yield return null;

            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var latestStamp = root.Q("latest-landmark-stamp");
            var stats = root.Q(className: "journey-stats");
            var photoGrid = root.Q("journey-photo-grid");
            Assert.That(latestStamp, Is.Not.Null);
            Assert.That(latestStamp.Q(className: "latest-stamp-image"), Is.Not.Null);
            Assert.That(latestStamp.Q(className: "latest-stamp-photo-state"), Is.Not.Null);
            Assert.That(stats, Is.Not.Null);
            Assert.That(latestStamp.parent.IndexOf(latestStamp), Is.LessThan(stats.parent.IndexOf(stats)),
                "The latest Stamp should appear at the top of Journey, before the summary counters.");
            Assert.That(photoGrid, Is.Not.Null);
            Assert.That(photoGrid.parent, Is.Not.SameAs(latestStamp),
                "The photo gallery must remain a separate Journey section, not become the Stamp itself.");

            home.Navigate(UiRoute.JourneyDetail);
            yield return null;
            Assert.That(root.Q(className: "history-card"), Is.Not.Null,
                "A Stamp detail should contain its Landmark history without another navigation step.");
            Assert.That(root.Q(className: "architecture-card"), Is.Not.Null);
            Assert.That(root.Q(className: "fact-card"), Is.Not.Null);
            Assert.That(root.Q<UnityEngine.UIElements.Button>("scan-again"), Is.Not.Null,
                "A completed Stamp should offer the dedicated Landmark scanner for an optional revisit.");
            Assert.That(root.Q<UnityEngine.UIElements.Button>("open-landmark"), Is.Null,
                "Stamp details should not route back through the obsolete Discover/AR Memory flow.");
            PetArSceneContext.LandmarkId = null;
        }

        [UnityTest]
        public IEnumerator WalkResultWithManyNewlyUnlockedCompanionsKeepsRewardsScrollable()
        {
            var home = CreateProfile();
            // Pre-load distance just under the highest unlock threshold (HorseWhite, 106 km) so the
            // deterministic provider's default 1 km walk crosses every distance-unlock threshold in
            // the roster (except the starter, already unlocked, and the infinite-threshold Deer).
            UiPrototypeRuntime.Instance.SaveData.totalDistanceKilometres = 105f;

            home.BeginWalk();
            var result = home.FinishWalk();
            yield return null;
            yield return null;

            var expectedUnlockCount = CompanionRoster.Entries.Count(
                e => !float.IsPositiveInfinity(e.UnlockDistanceKilometres) && e.UnlockDistanceKilometres > 0f);
            Assert.That(result.newlyUnlockedCompanionIds.Count, Is.EqualTo(expectedUnlockCount));
            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var scroll = root.Q<ScrollView>("walk-result-scroll");
            Assert.That(scroll, Is.Not.Null, "A large reward list must scroll instead of being clipped by the page.");
            Assert.That(root.Query(className: "info-row").ToList().Count, Is.EqualTo(expectedUnlockCount));
            Assert.That(scroll.contentContainer.Q<UnityEngine.UIElements.Button>("collect-&-continue"), Is.Not.Null,
                "The collect action must remain reachable at the end of the scrollable summary.");
            Assert.That(scroll.contentContainer.layout.height, Is.GreaterThan(scroll.contentViewport.layout.height),
                "The full-unlock fixture should exercise real vertical overflow.");
            Assert.That(scroll.verticalScroller.highValue, Is.GreaterThan(0f),
                "The overflowing result must expose a usable vertical scroll range.");
        }

        [UnityTest]
        public IEnumerator NavigationAndHeaderControlsUseV0VectorIconsAndProfileAvatar()
        {
            var home = CreateProfile();
            yield return null;
            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var mapIcon = root.Q<UnityEngine.UIElements.Button>("nav-map")?.Q<UnityEngine.UIElements.Image>("nav-icon");
            var profileInitial = root.Q<UnityEngine.UIElements.Button>("settings-button")?.Q<UnityEngine.UIElements.Label>(className: "profile-initial");
            Assert.That(mapIcon, Is.Not.Null);
            Assert.That(mapIcon.vectorImage, Is.Not.Null);
            Assert.That(mapIcon.tintColor.r, Is.EqualTo(84f / 255f).Within(0.001f));
            Assert.That(mapIcon.tintColor.g, Is.EqualTo(190f / 255f).Within(0.001f));
            Assert.That(mapIcon.tintColor.b, Is.EqualTo(107f / 255f).Within(0.001f));
            Assert.That(mapIcon.tintColor.a, Is.EqualTo(1f).Within(0.001f));
            Assert.That(profileInitial, Is.Not.Null);
            Assert.That(profileInitial.text, Is.EqualTo("T"));
        }

        [UnityTest]
        public IEnumerator ActivityDashboardOpensFromHomeMapHeaderAndBackReturnsToMap()
        {
            var home = CreateProfile();
            home.SelectRoot(UiRootTab.Map);
            yield return null;
            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var dashboardButton = root.Q<UnityEngine.UIElements.Button>("activity-dashboard-button");
            Assert.That(dashboardButton, Is.Not.Null, "Home Map should expose an entry point into the Activity Dashboard.");

            home.Navigate(UiRoute.ActivityDashboard);
            yield return null;
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.ActivityDashboard));
            var scroll = home.GetComponent<UIDocument>().rootVisualElement.Q<ScrollView>("screen-scroll");
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.Q("daily-activity-ring"), Is.Not.Null, "The v0-style Activity screen should retain its circular daily record.");

            Assert.That(home.HandleBack(), Is.True);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.HomeMap));
        }

        [UnityTest]
        public IEnumerator MissionClaimShowsRewardToastAndAdvancesToTheNextMission()
        {
            var home = CreateProfile();
            UiPrototypeRuntime.Instance.SaveData.totalDistanceKilometres = 0.1f;
            home.SelectRoot(UiRootTab.Companions);
            home.SelectRoot(UiRootTab.Map);
            yield return null;

            Assert.That(home.ClaimCurrentMission(), Is.True);
            yield return null;

            var root = home.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(UiPrototypeRuntime.Instance.SaveData.coins, Is.EqualTo(5));
            Assert.That(root.Q<Label>(className: "toast")?.text, Is.EqualTo("Reward claimed · 5 coins"));
            Assert.That(root.Q<Label>(className: "mission-title")?.text, Is.EqualTo("A Snack for Your Friend"));
        }

        [UnityTest]
        public IEnumerator AccountNameIsReadOnlyUntilPencilThenCheckmarkPersistsIt()
        {
            var home = CreateProfile();
            home.ShowAccount();
            yield return null;

            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var field = root.Q<TextField>("account-name-field");
            Assert.That(field, Is.Not.Null);
            Assert.That(field.isReadOnly, Is.True);
            var pencil = root.Q<Button>("account-name-edit-button");
            Assert.That(pencil, Is.Not.Null);
            Assert.That(pencil.ClassListContains("account-name-pencil-button"), Is.True);
            Assert.That(pencil.Q<UnityEngine.UIElements.Image>("icon-image")?.vectorImage, Is.Not.Null);

            InvokePrivate(home, "BeginAccountNameEdit");
            yield return null;
            field = root.Q<TextField>("account-name-field");
            Assert.That(field.isReadOnly, Is.False);
            field.value = "  New Walker  ";
            var save = root.Q<Button>("account-name-edit-button");
            Assert.That(save.ClassListContains("account-name-save-button"), Is.True);
            InvokePrivate(home, "CommitAccountNameEdit");
            yield return null;

            Assert.That(UiPrototypeRuntime.Instance.SaveData.displayName, Is.EqualTo("New Walker"));
            Assert.That(root.Q<TextField>("account-name-field").isReadOnly, Is.True);
            Assert.That(root.Q<Label>(className: "profile-initial")?.text, Is.EqualTo("N"));

            // Unchecked text is only a draft and must be discarded when the panel closes.
            InvokePrivate(home, "BeginAccountNameEdit");
            yield return null;
            root.Q<TextField>("account-name-field").value = "Discard Me";
            home.CloseFloatingOverlay();
            home.ShowAccount();
            yield return null;
            Assert.That(root.Q<TextField>("account-name-field").value, Is.EqualTo("New Walker"));
        }

        [UnityTest]
        public IEnumerator V0ScreenAnatomyIsPresentAcrossEveryPrimaryRoute()
        {
            var home = CreateProfile();
            var root = home.GetComponent<UIDocument>().rootVisualElement;

            home.SelectRoot(UiRootTab.Map);
            yield return null;
            Assert.That(root.Q(className: "map-viewport"), Is.Not.Null);
            // Within(1f) - the runtime panel's ScaleWithScreenSize mode can snap sub-pixel layout values
            // to the device-pixel grid, so an exact 0.1px tolerance is flaky across test-runner window sizes.
            Assert.That(root.Q(className: "map-top-status-bar").resolvedStyle.left, Is.EqualTo(44f).Within(1f));
            Assert.That(root.Q(className: "walk-control-card"), Is.Not.Null);
            Assert.That(root.Q(className: "bottom-nav"), Is.Not.Null);

            home.SelectRoot(UiRootTab.Companions);
            yield return null;
            Assert.That(root.Q(className: "featured-companion"), Is.Not.Null);
            Assert.That(root.Q(className: "owned-companion-grid"), Is.Not.Null);

            home.SelectRoot(UiRootTab.Shop);
            yield return null;
            var foodArt = root.Q<UnityEngine.UIElements.Image>(className: "food-art");
            Assert.That(foodArt, Is.Not.Null);
            Assert.That(foodArt.image, Is.Not.Null, "The Unity shop should use the food artwork extracted from the v0 reference.");
            Assert.That(foodArt.scaleMode, Is.EqualTo(ScaleMode.ScaleAndCrop));
            Assert.That(foodArt.parent.ClassListContains("food-art-well"), Is.True);
            Assert.That(foodArt.worldBound.width, Is.EqualTo(foodArt.parent.contentRect.width).Within(1f), "Food art should fill its allocated frame width.");
            Assert.That(foodArt.worldBound.height, Is.EqualTo(foodArt.parent.contentRect.height).Within(1f), "Food art should fill its allocated frame height.");

            home.SelectRoot(UiRootTab.Journey);
            yield return null;
            Assert.That(root.Q(className: "journey-stats"), Is.Not.Null);
            Assert.That(root.Q(className: "journey-scan-action"), Is.Not.Null);
            Assert.That(root.Q(className: "passport-card"), Is.Not.Null);

            home.Navigate(UiRoute.LandmarkDetail);
            yield return null;
            Assert.That(root.Q(className: "landmark-hero-card"), Is.Not.Null);
            Assert.That(root.Query(className: "story-section").ToList().Count, Is.EqualTo(3));

            home.Navigate(UiRoute.ActivityDashboard);
            yield return null;
            Assert.That(root.Q("daily-activity-ring"), Is.Not.Null);
            Assert.That(root.Q("weekly-activity-chart"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator CentralPostOfficeScanStampDeerJourneyAndIdempotenceWork()
        {
            var home = CreateProfile();
            UiPrototypeRuntime.Instance.EnterPetAr(PrototypeIds.Corgi, false, PendingPetInteraction.None, PrototypeIds.CentralPostOffice);
            yield return WaitForScene("PetAr");
            var ar = UnityEngine.Object.FindFirstObjectByType<WalkUiController>();
            Assert.That(ar.CurrentRoute, Is.EqualTo(UiRoute.PetAr));
            var arBackIcon = ar.GetComponent<UIDocument>().rootVisualElement
                .Q<UnityEngine.UIElements.Button>("ar-exit")?.Q<UnityEngine.UIElements.Image>("icon-image");
            Assert.That(arBackIcon, Is.Not.Null);
            Assert.That(arBackIcon.vectorImage, Is.Not.Null);
            Assert.That(arBackIcon.tintColor, Is.EqualTo(Color.white));
            ar.SimulateImageTargetRecognition(); ar.NextMemoryPage(); ar.NextMemoryPage();
            var first = ar.CollectStamp();
            var second = ar.CollectStamp();
            Assert.That(first.newlyCompleted, Is.True);
            Assert.That(first.unlockedCompanionId, Is.EqualTo(PrototypeIds.Deer));
            Assert.That(second.newlyCompleted, Is.False);
            Assert.That(UiPrototypeRuntime.Instance.SaveData.FindCompanion(PrototypeIds.Deer).unlocked, Is.True);
            Assert.That(UiPrototypeRuntime.Instance.SaveData.journeys.Count, Is.EqualTo(1));
            Assert.That(UiPrototypeRuntime.Instance.SaveData.stamps.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ArPhotoSaveAndRestartReloadPersistData()
        {
            var home = CreateProfile();
            UiPrototypeRuntime.Instance.EnterPetAr(PrototypeIds.Corgi, true, PendingPetInteraction.None, PrototypeIds.CentralPostOffice);
            yield return WaitForScene("PetAr");
            var ar = UnityEngine.Object.FindFirstObjectByType<WalkUiController>();
            ar.SimulateImageTargetRecognition(); ar.NextMemoryPage(); ar.NextMemoryPage(); ar.CollectStamp();

            // The real capture button lives on CorgiAR's uGUI HUD (ArPhotoCapture.Capture());
            // this exercises the same hand-off hook it calls, without needing a live AR camera frame.
            UiPrototypeRuntime.Instance.SaveArPhoto(new byte[] { 1, 2, 3, 4 });
            Assert.That(UiPrototypeRuntime.Instance.SaveData.savedPhotoPaths.Count, Is.EqualTo(1));
            Assert.That(UiPrototypeRuntime.Instance.SaveData.journeys.Single().photoPath,
                Is.EqualTo(UiPrototypeRuntime.Instance.SaveData.savedPhotoPaths.Single()),
                "Saving an AR Photo while viewing a Landmark should link it to that Landmark's Journey entry.");

            UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject);
            yield return null;
            SceneManager.LoadScene("Home");
            yield return WaitForScene("Home");
            Assert.That(UiPrototypeRuntime.Instance.SaveData.savedPhotoPaths.Count, Is.EqualTo(1));
            Assert.That(UiPrototypeRuntime.Instance.SaveData.journeys.Count, Is.EqualTo(1));
            Assert.That(UiPrototypeRuntime.Instance.SaveData.journeys.Single().photoPath, Is.Not.Null.And.Not.Empty);
            Assert.That(UiPrototypeRuntime.Instance.SaveData.FindCompanion(PrototypeIds.Deer).unlocked, Is.True);
        }

        [UnityTest]
        public IEnumerator ArPhotoOfAPlainPetViewLinksToAPerPetPerDayJourneyEntry()
        {
            var home = CreateProfile();
            UiPrototypeRuntime.Instance.EnterPetAr(PrototypeIds.Corgi, true);
            yield return WaitForScene("PetAr");

            UiPrototypeRuntime.Instance.SaveArPhoto(new byte[] { 1, 2, 3, 4 });
            Assert.That(UiPrototypeRuntime.Instance.SaveData.journeys.Count, Is.EqualTo(1));
            var journey = UiPrototypeRuntime.Instance.SaveData.journeys.Single();
            Assert.That(journey.companionId, Is.EqualTo(PrototypeIds.Corgi));
            Assert.That(journey.landmarkId, Is.Null.Or.Empty);

            // A second photo of the same pet on the same day updates the same entry rather than
            // creating a duplicate Journey record.
            UiPrototypeRuntime.Instance.SaveArPhoto(new byte[] { 5, 6, 7, 8 });
            Assert.That(UiPrototypeRuntime.Instance.SaveData.journeys.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CompanionVisualStateReflectsUnlockAndGrowthStage()
        {
            CreateProfile();
            var runtime = UiPrototypeRuntime.Instance;

            var starter = runtime.GetCompanionVisualState(PrototypeIds.Corgi);
            Assert.That(starter.unlocked, Is.True);
            Assert.That(starter.stage, Is.EqualTo(GrowthStage.Baby), "The starter begins at 0 EXP, well under its own Young threshold.");
            Assert.That(starter.scale, Is.EqualTo(0.70f));

            var husky = runtime.GetCompanionVisualState(PrototypeIds.Husky);
            Assert.That(husky.unlocked, Is.False);
            Assert.That(husky.scale, Is.Zero, "A locked companion has no meaningful display scale.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ResetRequiresConfirmationPathAndReturnsToSetup()
        {
            var home = CreateProfile();
            home.ShowOverlay(UiOverlay.Settings);
            home.ShowOverlay(UiOverlay.Confirmation);
            Assert.That(UiPrototypeRuntime.Instance.Navigator.CurrentOverlay, Is.EqualTo(UiOverlay.Confirmation));
            home.ConfirmResetLocalProgress();
            yield return null;
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.OnboardingSetup));
            Assert.That(UiPrototypeRuntime.Instance.HasProfile, Is.False);
            Assert.That(File.Exists(_savePath), Is.False);
        }

        [UnityTest]
        public IEnumerator OverlayAndScreenBackStackBehaveInOrder()
        {
            var home = CreateProfile();
            home.SelectRoot(UiRootTab.Companions);
            home.Navigate(UiRoute.CompanionDetail);
            home.ShowOverlay(UiOverlay.Settings);
            Assert.That(home.HandleBack(), Is.True);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.CompanionDetail));
            Assert.That(UiPrototypeRuntime.Instance.Navigator.CurrentOverlay, Is.Null);
            Assert.That(home.HandleBack(), Is.True);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.CompanionCollection));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompanionPetButtonOpensNonArPlaygroundAndReturnsToCompanions()
        {
            var home = CreateProfile();
            home.SelectRoot(UiRootTab.Companions);
            yield return null;

            // "Feed" is the companion card's entry point into the Pet3D meadow (the old
            // standalone "Pet" button was folded into it).
            var root = home.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<UnityEngine.UIElements.Button>("feed"), Is.Not.Null);

            UiPrototypeRuntime.Instance.EnterPet3D(PrototypeIds.Corgi);
            yield return WaitForScene(Pet3DSceneContext.SceneName);

            Assert.That(Pet3DSceneContext.IsActive, Is.True);
            Assert.That(UiPrototypeRuntime.Instance.Navigator.CurrentRoute, Is.EqualTo(UiRoute.Pet3D));
            Assert.That(GameObject.Find("Pet 3D App Bridge"), Is.Not.Null);
            Assert.That(GameObject.Find("Pet Preview Ground"), Is.Not.Null);
            var companion = GameObject.Find("Corgi Companion");
            Assert.That(companion, Is.Not.Null);
            Assert.That(companion.GetComponent("PetGrowthController"), Is.Not.Null,
                "The meadow pet must receive feeding-driven Baby/Young/Adult growth.");
            Assert.That(companion.GetComponent("PetGrowthVfx"), Is.Not.Null,
                "The meadow pet must show a visual effect when its growth stage changes.");
            Assert.That(Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                    .Any(component => component.gameObject.scene.IsValid() &&
                                      component.GetType().Name == "Pet3DModeController"), Is.True,
                "The meadow must use its own non-AR mode controller instead of modifying the PetAr controller.");
            GameObject bridge = GameObject.Find("Pet 3D App Bridge");
            var meadowDocument = bridge.GetComponent<UIDocument>();
            Assert.That(meadowDocument.panelSettings,
                Is.SameAs(Resources.Load<PanelSettings>("UI/ARWalkingArPanelSettings")),
                "The meadow must serialize the exact same PanelSettings used by PetAr.");
            Assert.That(meadowDocument.sortingOrder, Is.EqualTo(100f));
            var meadowRoot = meadowDocument.rootVisualElement;
            Assert.That(meadowRoot.Q("pet-3d-glass-hud"), Is.Not.Null,
                "The meadow must render through UI Toolkit instead of the legacy uGUI HUD.");
            Assert.That(meadowRoot.Q<UnityEngine.UIElements.Button>("pet-3d-exit"), Is.Not.Null);
            var changePet = meadowRoot.Q<UnityEngine.UIElements.Button>("pet-3d-change-pet");
            Assert.That(changePet, Is.Not.Null);
            Assert.That(changePet.ClassListContains("ar-change-pet-card"), Is.True,
                "The meadow change-pet control must share the AR component class.");
            Assert.That(changePet.ClassListContains("pet3d-change-pet-card"), Is.False,
                "The meadow change-pet card must use the AR dimensions without a 3D-only width override.");
            var meadowBack = meadowRoot.Q<UnityEngine.UIElements.Button>("pet-3d-exit");
            Assert.That(meadowBack.resolvedStyle.width, Is.EqualTo(82f).Within(1f));
            Assert.That(meadowBack.resolvedStyle.height, Is.EqualTo(82f).Within(1f));
            var meadowBackIcon = meadowBack.Q("icon-image");
            Assert.That(meadowBackIcon, Is.Not.Null,
                "The meadow Back icon must expose the same UI Toolkit name used by AR's ID selector.");
            Assert.That(meadowBackIcon.resolvedStyle.width, Is.EqualTo(38f).Within(1f));
            Assert.That(meadowBackIcon.resolvedStyle.height, Is.EqualTo(38f).Within(1f));
            Assert.That(meadowBack.parent.parent.ClassListContains("ar-page"), Is.True,
                "The meadow Back button must use the same layout hierarchy as AR.");
            var meadowTopBar = meadowRoot.Q("pet-3d-top-bar");
            Assert.That(meadowTopBar.resolvedStyle.left, Is.EqualTo(32f).Within(1f));
            Assert.That(meadowTopBar.resolvedStyle.top, Is.EqualTo(28f).Within(1f));
            var openAr = meadowRoot.Q<UnityEngine.UIElements.Button>("pet-3d-open-ar");
            Assert.That(openAr, Is.Not.Null);
            Assert.That(openAr.text, Is.EqualTo(string.Empty));
            Assert.That(openAr.Q<Label>(className: "pet3d-ar-mode-label")?.text, Is.EqualTo("AR"));
            Assert.That(openAr.resolvedStyle.width, Is.EqualTo(82f).Within(1f));
            Assert.That(openAr.worldBound.xMax, Is.EqualTo(meadowTopBar.worldBound.xMax).Within(5f),
                "The AR mode switch must stay at the top-right edge of the shared top bar.");
            var meadowStatusRow = meadowRoot.Q<VisualElement>(className: "pet3d-status-row-with-mode-switch");
            Assert.That(meadowStatusRow.worldBound.xMax, Is.LessThanOrEqualTo(openAr.worldBound.xMin + 1f),
                "The status pill must reserve enough right-side space for the AR switch.");

            var interactionCircles = meadowRoot.Query<VisualElement>(className: "ar-interaction-circle").ToList();
            Assert.That(interactionCircles.Count, Is.EqualTo(3));
            foreach (VisualElement circle in interactionCircles)
                Assert.That(circle.resolvedStyle.width, Is.EqualTo(152f).Within(1f),
                    "The meadow actions should be slightly larger than the shared AR base size.");
            Assert.That(meadowRoot.Q("pet-3d-food-quantity"), Is.Not.Null);
            Assert.That(meadowRoot.Q<UnityEngine.UIElements.Button>("pet-3d-switch-food"), Is.Not.Null);
            Assert.That(meadowRoot.Q("pet-3d-pet-picker"), Is.Not.Null);

            var legacyCanvas = GameObject.Find("Corgi AR HUD").GetComponent<Canvas>();
            Assert.That(legacyCanvas.enabled, Is.False,
                "The old uGUI HUD must not render underneath the UI Toolkit meadow HUD.");
            var foodDriver = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .Single(component => component.gameObject.scene.IsValid() &&
                                     component.GetType().Name == "FoodDragThrowUI");
            var toolkitFoodIcon = meadowRoot.Q<VisualElement>(className: "pet3d-food-circle")
                .Q<UnityEngine.UIElements.Image>(className: "ar-interaction-icon-food");
            Sprite previousFoodIcon = toolkitFoodIcon.sprite;
            foodDriver.GetType().GetMethod("SelectNextFood")?.Invoke(foodDriver, null);
            yield return null;
            Assert.That(toolkitFoodIcon.sprite, Is.Not.SameAs(previousFoodIcon),
                "The UI Toolkit food action must follow the original food-selection logic.");
            var arSession = Resources.FindObjectsOfTypeAll<GameObject>()
                .First(candidate => candidate.scene.IsValid() && candidate.name == "AR Session");
            Assert.That(arSession.activeInHierarchy, Is.False,
                "The phone playground entry must use the meadow and must not start AR.");

            MonoBehaviour meadowBinder = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .Single(component => component.gameObject.scene.IsValid() &&
                                     component.GetType().Name == "PetBinder");
            string selectedMeadowPet = meadowBinder.GetType().GetProperty("CurrentId")
                ?.GetValue(meadowBinder) as string;
            UiPrototypeRuntime.Instance.SwitchPet3DToAr(selectedMeadowPet);
            yield return WaitForScene("PetAr");
            Assert.That(Pet3DSceneContext.IsActive, Is.False);
            Assert.That(PetArSceneContext.PetId, Is.EqualTo(selectedMeadowPet),
                "Switching modes must carry the currently bound meadow pet into AR.");
            Assert.That(UiPrototypeRuntime.Instance.Navigator.CurrentRoute, Is.EqualTo(UiRoute.PetAr));

            UiPrototypeRuntime.Instance.ReturnFromPetAr();
            yield return WaitForScene(Pet3DSceneContext.SceneName);
            Assert.That(Pet3DSceneContext.IsActive, Is.True);
            Assert.That(Pet3DSceneContext.PetId, Is.EqualTo(selectedMeadowPet));
            Assert.That(UiPrototypeRuntime.Instance.Navigator.CurrentRoute, Is.EqualTo(UiRoute.Pet3D),
                "Back from AR must restore the meadow route it was opened from.");

            UiPrototypeRuntime.Instance.ReturnFromPet3D();
            yield return WaitForScene("Home");
            Assert.That(Pet3DSceneContext.IsActive, Is.False);
            Assert.That(UiPrototypeRuntime.Instance.Navigator.CurrentRoute, Is.EqualTo(UiRoute.CompanionCollection));
        }

        HomeUiController CreateProfile()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home, Is.Not.Null);
            Assert.That(home.CompleteSetup("Test Walker"), Is.True);
            return home;
        }

        static void InvokePrivate(HomeUiController home, string methodName)
        {
            var method = typeof(HomeUiController).GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(home, null);
        }

        static IEnumerator WaitForScene(string sceneName)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
            yield return null;
            yield return null;
        }
    }
}
