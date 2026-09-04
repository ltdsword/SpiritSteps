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
            Assert.That(result.coinsAwarded, Is.EqualTo(30));
            home.SelectRoot(UiRootTab.Companions); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.CompanionCollection));
            home.Navigate(UiRoute.CompanionDetail); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.CompanionDetail));
            home.SelectRoot(UiRootTab.Shop); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.ShopFood));
            var feed = home.Feed("basic-food", PrototypeIds.Corgi);
            Assert.That(feed.success, Is.True);
            Assert.That(UiPrototypeRuntime.Instance.SaveData.coins, Is.EqualTo(10));
            home.SelectRoot(UiRootTab.Journey); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.JourneyList));
            yield return null;
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
        public IEnumerator V0ScreenAnatomyIsPresentAcrossEveryPrimaryRoute()
        {
            var home = CreateProfile();
            var root = home.GetComponent<UIDocument>().rootVisualElement;

            home.SelectRoot(UiRootTab.Map);
            yield return null;
            Assert.That(root.Q(className: "map-viewport"), Is.Not.Null);
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
            Assert.That(starter.stage, Is.EqualTo(GrowthStage.Baby), "The starter begins at 450 EXP, still under the 500 Baby/Young boundary.");
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

        HomeUiController CreateProfile()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home, Is.Not.Null);
            Assert.That(home.CompleteSetup("Test Walker"), Is.True);
            return home;
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
