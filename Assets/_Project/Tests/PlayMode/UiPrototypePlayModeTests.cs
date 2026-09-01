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
            var feed = home.Feed("basic-food", PrototypeIds.Dog);
            Assert.That(feed.success, Is.True);
            Assert.That(UiPrototypeRuntime.Instance.SaveData.coins, Is.EqualTo(10));
            home.SelectRoot(UiRootTab.Journey); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.JourneyList));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NavigationAndHeaderControlsUseImageIcons()
        {
            var home = CreateProfile();
            yield return null;
            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var mapIcon = root.Q<UnityEngine.UIElements.Button>("nav-map")?.Q<UnityEngine.UIElements.Image>("nav-icon");
            var settingsIcon = root.Q<UnityEngine.UIElements.Button>("settings-button")?.Q<UnityEngine.UIElements.Image>("icon-image");
            Assert.That(mapIcon, Is.Not.Null);
            Assert.That(mapIcon.image, Is.Not.Null);
            Assert.That(mapIcon.tintColor, Is.EqualTo(Color.black));
            Assert.That(settingsIcon, Is.Not.Null);
            Assert.That(settingsIcon.image, Is.Not.Null);
            Assert.That(settingsIcon.tintColor, Is.EqualTo(Color.black));
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

            Assert.That(home.HandleBack(), Is.True);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.HomeMap));
        }

        [UnityTest]
        public IEnumerator CentralPostOfficeScanStampRabbitJourneyAndIdempotenceWork()
        {
            var home = CreateProfile();
            UiPrototypeRuntime.Instance.SelectedLandmarkIndex = 1;
            home.Navigate(UiRoute.LandmarkDetail);
            UiPrototypeRuntime.Instance.EnterLandmarkAr();
            yield return WaitForScene("Walk");
            var ar = UnityEngine.Object.FindFirstObjectByType<WalkUiController>();
            Assert.That(ar.CurrentRoute, Is.EqualTo(UiRoute.LandmarkArMemory));
            var arBackIcon = ar.GetComponent<UIDocument>().rootVisualElement
                .Q<UnityEngine.UIElements.Button>("ar-exit")?.Q<UnityEngine.UIElements.Image>("icon-image");
            Assert.That(arBackIcon, Is.Not.Null);
            Assert.That(arBackIcon.image, Is.Not.Null);
            Assert.That(arBackIcon.tintColor, Is.EqualTo(Color.black));
            ar.SimulateImageTargetRecognition(); ar.NextMemoryPage(); ar.NextMemoryPage();
            var first = ar.CollectStamp();
            var second = ar.CollectStamp();
            Assert.That(first.newlyCompleted, Is.True);
            Assert.That(first.unlockedCompanionId, Is.EqualTo(PrototypeIds.Rabbit));
            Assert.That(second.newlyCompleted, Is.False);
            Assert.That(UiPrototypeRuntime.Instance.SaveData.FindCompanion(PrototypeIds.Rabbit).unlocked, Is.True);
            Assert.That(UiPrototypeRuntime.Instance.SaveData.journeys.Count, Is.EqualTo(1));
            Assert.That(UiPrototypeRuntime.Instance.SaveData.stamps.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ArPhotoSaveAndRestartReloadPersistData()
        {
            var home = CreateProfile();
            UiPrototypeRuntime.Instance.SelectedLandmarkIndex = 1;
            home.Navigate(UiRoute.LandmarkDetail);
            UiPrototypeRuntime.Instance.EnterLandmarkAr();
            yield return WaitForScene("Walk");
            var ar = UnityEngine.Object.FindFirstObjectByType<WalkUiController>();
            ar.SimulateImageTargetRecognition(); ar.NextMemoryPage(); ar.NextMemoryPage(); ar.CollectStamp();
            ar.OpenPhoto(); yield return null;
            Assert.That(ar.CurrentRoute, Is.EqualTo(UiRoute.ArPhoto));
            ar.SavePhoto();
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
            Assert.That(UiPrototypeRuntime.Instance.SaveData.FindCompanion(PrototypeIds.Rabbit).unlocked, Is.True);
        }

        [UnityTest]
        public IEnumerator CompanionVisualStateReflectsUnlockAndGrowthStage()
        {
            CreateProfile();
            var runtime = UiPrototypeRuntime.Instance;

            var dog = runtime.GetCompanionVisualState(PrototypeIds.Dog);
            Assert.That(dog.unlocked, Is.True);
            Assert.That(dog.stage, Is.EqualTo(GrowthStage.Baby), "Dog starts at 450 EXP, still under the 500 Baby/Young boundary.");
            Assert.That(dog.scale, Is.EqualTo(0.70f));

            var cat = runtime.GetCompanionVisualState(PrototypeIds.Cat);
            Assert.That(cat.unlocked, Is.False);
            Assert.That(cat.scale, Is.Zero, "A locked companion has no meaningful display scale.");

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
