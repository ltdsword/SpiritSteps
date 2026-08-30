using System.Collections;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ARWalking.Tests.PlayMode
{
    public sealed class UiPrototypePlayModeTests
    {
        [UnityTest]
        public IEnumerator BootOpensHomeWithAppUiDocument()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Home");
            Assert.That(Object.FindFirstObjectByType<HomeUiController>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator FiveRootDestinationsAndCrossSceneReturnWork()
        {
            SceneManager.LoadScene("Home");
            yield return WaitForScene("Home");
            var home = Object.FindFirstObjectByType<HomeUiController>();
            home.SelectRoot(UiRootTab.Map); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.HomeMap));
            home.SelectRoot(UiRootTab.Garden); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.SeedlingGrowth));
            home.SelectRoot(UiRootTab.Journal); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.JourneyJournal));
            home.SelectRoot(UiRootTab.Book); Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.SpiritCollection));
            home.SelectRoot(UiRootTab.WalkAr);
            yield return WaitForScene("Walk");
            var walk = Object.FindFirstObjectByType<WalkUiController>();
            Assert.That(walk, Is.Not.Null);
            Assert.That(walk.CurrentRoute, Is.EqualTo(UiRoute.ArCompanion));
            walk.ExitToHome();
            yield return WaitForScene("Home");
            Assert.That(Object.FindFirstObjectByType<HomeUiController>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PrimaryHomeFlowsAndBackStackWork()
        {
            SceneManager.LoadScene("Home");
            yield return WaitForScene("Home");
            var home = Object.FindFirstObjectByType<HomeUiController>();
            home.SelectRoot(UiRootTab.Map);
            home.Navigate(UiRoute.ActiveWalk);
            home.Navigate(UiRoute.WalkSummary);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.WalkSummary));
            Assert.That(home.HandleBack(), Is.True);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.ActiveWalk));
            home.SelectRoot(UiRootTab.Garden);
            home.Navigate(UiRoute.HatchReveal);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.HatchReveal));
            home.SelectRoot(UiRootTab.Journal);
            home.Navigate(UiRoute.JourneyDetail);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.JourneyDetail));
            home.SelectRoot(UiRootTab.Book);
            home.Navigate(UiRoute.SpiritDetail);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.SpiritDetail));
            home.SelectRoot(UiRootTab.Map);
            home.Navigate(UiRoute.LandmarkMemory);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.LandmarkMemory));
        }

        [UnityTest]
        public IEnumerator ArPhotoPreviewSaveAndBackWork()
        {
            SceneManager.LoadScene("Walk");
            yield return WaitForScene("Walk");
            var walk = Object.FindFirstObjectByType<WalkUiController>();
            var before = UiPrototypeRuntime.Instance.SavedPhotoCount;
            walk.OpenPhoto();
            yield return null;
            Assert.That(walk.CurrentRoute, Is.EqualTo(UiRoute.ArPhoto));
            walk.SavePhoto();
            Assert.That(walk.CurrentRoute, Is.EqualTo(UiRoute.ArCompanion));
            Assert.That(UiPrototypeRuntime.Instance.SavedPhotoCount, Is.EqualTo(before + 1));
        }

        [UnityTest]
        public IEnumerator OverlayClosesBeforeRouteBack()
        {
            SceneManager.LoadScene("Home");
            yield return WaitForScene("Home");
            var home = Object.FindFirstObjectByType<HomeUiController>();
            home.SelectRoot(UiRootTab.Map);
            home.Navigate(UiRoute.ActiveWalk);
            home.ShowOverlay(UiOverlay.Settings);
            Assert.That(home.HandleBack(), Is.True);
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.ActiveWalk));
            Assert.That(UiPrototypeRuntime.Instance.Navigator.CurrentOverlay, Is.Null);
        }

        static IEnumerator WaitForScene(string sceneName)
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
            yield return null;
            yield return null;
        }
    }
}
