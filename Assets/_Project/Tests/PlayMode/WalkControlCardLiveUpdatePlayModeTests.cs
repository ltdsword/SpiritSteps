using System;
using System.Collections;
using System.IO;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ARWalking.Tests.PlayMode
{
    // Regression coverage for: the walk-control-card's distance/steps/coins numbers only refreshed on
    // Render() (route/overlay changes), so they stayed frozen for the whole walk even though
    // RealWalkMetricsProvider was correctly accumulating distance underneath. HomeUiController.Update() now
    // rewrites those labels directly every frame while UiRoute.ActiveWalk is showing - this test proves the
    // label text changes from a plain Update() tick, with no navigation/Render() trigger in between.
    public sealed class WalkControlCardLiveUpdatePlayModeTests
    {
        sealed class MutableWalkMetricsProvider : IWalkMetricsProvider
        {
            public WalkMetrics Live = new WalkMetrics { distanceKilometres = 0f, hasSteps = true, steps = 0, elapsedSeconds = 0f };
            public bool IsWalking { get; private set; }
            public void StartWalk() => IsWalking = true;
            public WalkMetrics GetLiveMetrics() => IsWalking ? Live : new WalkMetrics();
            public WalkMetrics StopWalk() { IsWalking = false; return Live; }
        }

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
        MutableWalkMetricsProvider _walkProvider;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UiPrototypeRuntime.Instance != null)
            {
                UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject);
                yield return null;
            }
            _savePath = Path.Combine(Path.GetTempPath(), "ar-walking-walk-card-live-" + Guid.NewGuid().ToString("N"), LocalPlayerSaveStore.FileName);
            _walkProvider = new MutableWalkMetricsProvider();
            UiPrototypeRuntime.ClearTestOverrides();
            UiPrototypeRuntime.TestSavePathOverride = _savePath;
            UiPrototypeRuntime.TestWalkProviderOverride = _walkProvider;
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
        public IEnumerator DistanceStepsAndCoins_TickLive_DuringActiveWalk_WithoutNavigating()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home.CompleteSetup("Live Tick Test"), Is.True);
            yield return null;

            home.BeginWalk();
            yield return null;

            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var distanceLabel = root.Q<Label>(className: "walk-distance-value");
            Assert.That(distanceLabel, Is.Not.Null);
            Assert.That(distanceLabel.text, Is.EqualTo("0.0"));

            // Mutate the provider directly - no navigation, no overlay, no Render() trigger of any kind.
            _walkProvider.Live = new WalkMetrics { distanceKilometres = 1.234f, hasSteps = true, steps = 1650, elapsedSeconds = 300f };
            yield return null;

            Assert.That(distanceLabel.text, Is.EqualTo("1.2"), "distance must tick live from a plain Update() frame");

            var coinsLabel = root.Q(className: "walk-summary-row").Q(className: "sun-value").Q<Label>(className: "metric-value");
            Assert.That(coinsLabel.text, Is.EqualTo("+" + Mathf.FloorToInt(1.234f * 20f)));

            var stepsLabel = root.Q(className: "walk-summary-row").Q(className: "blossom-value").Q<Label>(className: "metric-value");
            Assert.That(stepsLabel.text, Is.EqualTo("1,650"));
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
