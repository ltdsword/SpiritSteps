using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ARWalking.Tests.PlayMode
{
    // Regression coverage for the Map screen's nearby-landmark "!" alert button and the "Find & scan
    // this memory" panel it opens (docs/images/map.png, docs/images/memory_floating_panel.png) - an
    // independent signal from the Mission Card's own single-slot Landmark entry, so it must show for
    // any undiscovered landmark in range regardless of what the Mission Card is currently displaying.
    // Triggers within a fixed 500m radius (distinct from each landmark's own, usually smaller, AR
    // unlock radius) and prefers the nearest undiscovered landmark when more than one is in range.
    public sealed class NearbyLandmarkAlertPlayModeTests
    {
        sealed class DeterministicFailingWebViewBridge : IWebViewBridge
        {
            public bool IsInitialized => true;
            public void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded) => onError("test: no network");
            public void SetMargins(int left, int top, int right, int bottom) { }
            public void SetVisibility(bool visible) { }
            public void LoadURL(string url) { }
            public void EvaluateJS(string js) { }
        }

        // Mutable so a single instance, captured once by the runtime at scene-load time, can still
        // have its distances changed live from within a test - reassigning the static
        // TestMapProviderOverride field mid-test would NOT retroactively rewire the already-constructed
        // runtime's captured provider reference, since that's only read once at Awake().
        sealed class MutableLandmarkMapProvider : ILandmarkMapProvider
        {
            public readonly Dictionary<string, float> DistancesMetres = new Dictionary<string, float>();

            public LandmarkMapState GetMapState() => new LandmarkMapState { hasPlayerPosition = true, playerNormalizedPosition = new Vector2(0.5f, 0.5f), mapHeadingDegrees = 0f };
            public LandmarkProximity GetLandmarkProximity(string landmarkId)
            {
                var distance = DistancesMetres.TryGetValue(landmarkId, out var value) ? value : 5000f;
                return new LandmarkProximity { landmarkId = landmarkId, distanceMetres = distance, directionDegrees = 0f, isWithinUnlockRadius = distance <= 100f };
            }
        }

        string _savePath;
        MutableLandmarkMapProvider _mapProvider;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UiPrototypeRuntime.Instance != null)
            {
                UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject);
                yield return null;
            }
            _savePath = Path.Combine(Path.GetTempPath(), "ar-walking-nearby-alert-" + Guid.NewGuid().ToString("N"), LocalPlayerSaveStore.FileName);
            _mapProvider = new MutableLandmarkMapProvider();
            UiPrototypeRuntime.ClearTestOverrides();
            UiPrototypeRuntime.TestSavePathOverride = _savePath;
            UiPrototypeRuntime.TestWebViewBridgeOverride = new DeterministicFailingWebViewBridge();
            UiPrototypeRuntime.TestMapProviderOverride = _mapProvider;
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

        static IEnumerator WaitForScene(string sceneName)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AlertButtonShowsForALandmarkInRange_AndOpensTheMemoryPanel()
        {
            _mapProvider.DistancesMetres[PrototypeIds.CentralPostOffice] = 80f;

            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home.CompleteSetup("Nearby Alert Test"), Is.True);
            home.SelectRoot(UiRootTab.Map);
            yield return null; yield return null;

            Assert.That(home.NearbyLandmarkAlertId, Is.EqualTo(PrototypeIds.CentralPostOffice));

            var root = home.GetComponent<UIDocument>().rootVisualElement;
            // "nearby-landmark-alert-button" is the button's name (IconAction's 5th param), not a class.
            var alertButton = root.Q(name: "nearby-landmark-alert-button");
            Assert.That(alertButton, Is.Not.Null);
            Assert.That(alertButton.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            home.ShowNearbyMemory();
            yield return null;

            Assert.That(root.Q(className: "memory-panel"), Is.Not.Null);
            Assert.That(root.Q<Label>(className: "memory-panel-landmark-name")?.text, Is.EqualTo("Central Post Office"));
            Assert.That(root.Q<Button>("scan"), Is.Not.Null, "the panel's Scan action must exist to lead into LandmarkScan");
        }

        [UnityTest]
        public IEnumerator AlertButtonIsHidden_WhenNoLandmarkIsInRange()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home.CompleteSetup("Nearby Alert Far"), Is.True);
            home.SelectRoot(UiRootTab.Map);
            yield return null; yield return null;

            Assert.That(home.NearbyLandmarkAlertId, Is.Null);
            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var alertButton = root.Q(name: "nearby-landmark-alert-button");
            Assert.That(alertButton, Is.Not.Null);
            Assert.That(alertButton.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator AlertPrefersTheNearestUndiscoveredLandmark_WhenMultipleAreInRange()
        {
            _mapProvider.DistancesMetres[PrototypeIds.CentralPostOffice] = 300f;
            _mapProvider.DistancesMetres[PrototypeIds.NotreDameBasilica] = 150f; // nearest
            _mapProvider.DistancesMetres[PrototypeIds.IndependencePalace] = 490f;

            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home.CompleteSetup("Nearby Alert Nearest"), Is.True);
            home.SelectRoot(UiRootTab.Map);
            yield return null; yield return null;

            Assert.That(home.NearbyLandmarkAlertId, Is.EqualTo(PrototypeIds.NotreDameBasilica));
        }

        [UnityTest]
        public IEnumerator AlertRadiusIsFiveHundredMetres_ExclusiveBeyondThat()
        {
            _mapProvider.DistancesMetres[PrototypeIds.CentralPostOffice] = 500f; // inclusive at the boundary

            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home.CompleteSetup("Nearby Alert Bound"), Is.True);
            home.SelectRoot(UiRootTab.Map);
            yield return null; yield return null;
            Assert.That(home.NearbyLandmarkAlertId, Is.EqualTo(PrototypeIds.CentralPostOffice));

            _mapProvider.DistancesMetres[PrototypeIds.CentralPostOffice] = 501f;
            yield return null; yield return null;
            Assert.That(home.NearbyLandmarkAlertId, Is.Null);
        }
    }
}
