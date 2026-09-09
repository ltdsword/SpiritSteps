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
            // Corgi (default lead, Baby stage) earns its base 4.0 coins/100m - see CompanionProgressionService.IncomeOf.
            Assert.That(coinsLabel.text, Is.EqualTo("+" + Mathf.RoundToInt(4.0f * (1.234f * 1000f / 100f))));

            var stepsLabel = root.Q(className: "walk-summary-row").Q(className: "blossom-value").Q<Label>(className: "metric-value");
            Assert.That(stepsLabel.text, Is.EqualTo("1,650"));
        }

        [UnityTest]
        public IEnumerator SessionMetrics_StartAtZero_CommitToLifetimeTotals_ThenReset()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home.CompleteSetup("Session Totals Test"), Is.True);
            var save = UiPrototypeRuntime.Instance.SaveData;
            save.coins = 100;
            save.totalDistanceKilometres = 2f;
            save.hasTotalSteps = true;
            save.totalSteps = 1000;
            save.dailyActivity.Add(new DailyActivityData
            {
                dateIso = CompanionProgressionService.LocalNow(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                distanceKilometres = 2f,
                hasSteps = true,
                steps = 1000
            });

            // Force the Map to rebuild after seeding non-zero lifetime and daily values.
            home.SelectRoot(UiRootTab.Companions);
            home.SelectRoot(UiRootTab.Map);
            yield return null;

            var root = home.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q(className: "coin-status-pill").Q<Label>(className: "status-value").text, Is.EqualTo("100"));
            Assert.That(root.Q(className: "distance-status-pill").Q<Label>(className: "status-value").text, Is.EqualTo("2.0 km"));
            AssertSessionMetrics(root, "0.0", "+0", "0");

            home.BeginWalk();
            _walkProvider.Live = new WalkMetrics { distanceKilometres = 0.1f, hasSteps = true, steps = 42, elapsedSeconds = 60f };
            yield return null;
            AssertSessionMetrics(root, "0.1", "+4", "42");

            var result = home.FinishWalk();
            Assert.That(result.coinsAwarded, Is.EqualTo(4));
            Assert.That(save.coins, Is.EqualTo(104));
            Assert.That(save.totalDistanceKilometres, Is.EqualTo(2.1f).Within(0.0001f));
            Assert.That(save.totalSteps, Is.EqualTo(1042));

            home.SelectRoot(UiRootTab.Map);
            yield return null;
            Assert.That(root.Q(className: "coin-status-pill").Q<Label>(className: "status-value").text, Is.EqualTo("104"));
            Assert.That(root.Q(className: "distance-status-pill").Q<Label>(className: "status-value").text, Is.EqualTo("2.1 km"));
            AssertSessionMetrics(root, "0.0", "+0", "0");
        }

        static void AssertSessionMetrics(VisualElement root, string distance, string coins, string steps)
        {
            Assert.That(root.Q<Label>(className: "walk-distance-value").text, Is.EqualTo(distance));
            Assert.That(root.Q(className: "walk-summary-row").Q(className: "sun-value").Q<Label>(className: "metric-value").text, Is.EqualTo(coins));
            Assert.That(root.Q(className: "walk-summary-row").Q(className: "blossom-value").Q<Label>(className: "metric-value").text, Is.EqualTo(steps));
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
