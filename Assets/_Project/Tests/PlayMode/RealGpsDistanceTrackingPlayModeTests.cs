using System;
using System.Collections;
using System.IO;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ARWalking.Tests.PlayMode
{
    // Closes the gap flagged in GeoProviderEditModeTests' own header comment: "GPS/step hardware itself is
    // not exercised here... see the merge checklist... for the required on-device Play Mode verification."
    // That on-device verification never got written, so RealWalkMetricsProvider's live wiring to
    // DeviceLocationService/DeviceStepCounterService had zero automated coverage. These tests leave
    // TestWalkProviderOverride unset so UiPrototypeRuntime builds the real production pipeline
    // (RealWalkMetricsProvider + DeviceLocationService + DeviceStepCounterService), then drive the Editor
    // simulation paths a human tester would use - holding an arrow key for GPS, tapping space for steps - via
    // synthetic Input System events, proving both accumulate end-to-end without a device or a real walk.
    //
    // Derives from InputTestFixture: without it, injected KeyboardState events raced against the Input
    // System's own automatic per-frame poll of real (empty) hardware state, which could silently clear the
    // simulated key-down between frames - these tests passed in isolation but flaked to "0 movement" when run
    // alongside the rest of the suite. InputTestFixture severs the tie to real hardware input for the test's
    // duration, making the injected state the only input source and the simulation deterministic.
    public sealed class RealGpsDistanceTrackingPlayModeTests : InputTestFixture
    {
        // Mirrors UiPrototypePlayModeTests' bridge double: the WebView isn't what this test cares about, so it
        // fails immediately and HomeUiController falls back to the illustrated map (no HTML staging needed).
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
            _savePath = Path.Combine(Path.GetTempPath(), "ar-walking-real-gps-" + Guid.NewGuid().ToString("N"), LocalPlayerSaveStore.FileName);
            UiPrototypeRuntime.ClearTestOverrides();
            UiPrototypeRuntime.TestSavePathOverride = _savePath;
            UiPrototypeRuntime.TestMapProviderOverride = new DeterministicLandmarkMapProvider();
            UiPrototypeRuntime.TestWebViewBridgeOverride = new DeterministicFailingWebViewBridge();
            // TestWalkProviderOverride is deliberately left null - the real RealWalkMetricsProvider must run.
            SceneManager.LoadScene("Home");
            yield return WaitForScene("Home");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ReleaseArrowKeys();
            if (UiPrototypeRuntime.Instance != null) UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject);
            yield return null;
            UiPrototypeRuntime.ClearTestOverrides();
            var directory = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator HoldingUpArrow_DuringAWalk_AccumulatesRealDistanceViaGps()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home, Is.Not.Null);
            Assert.That(home.CompleteSetup("Real GPS Test"), Is.True);
            yield return null;

            var runtime = UiPrototypeRuntime.Instance;
            Assert.That(runtime.WalkProvider, Is.InstanceOf<RealWalkMetricsProvider>(),
                "this test only proves anything if the real provider (not a test double) is wired up");

            runtime.LocationService.Activate();
            yield return null;
            Assert.That(runtime.LocationService.HasFix, Is.True, "the Editor GPS simulator should report a fix immediately");

            home.BeginWalk();
            yield return null;
            var before = runtime.WalkProvider.GetLiveMetrics();
            Assert.That(before.distanceKilometres, Is.EqualTo(0f).Within(0.0001f), "no movement yet");

            const int heldFrames = 20;
            PressUpArrow();
            for (var i = 0; i < heldFrames; i++) yield return null;
            ReleaseArrowKeys();
            yield return null;

            var after = runtime.WalkProvider.GetLiveMetrics();
            // DeviceLocationService moves ~8m per frame the key is held (see editorSimulatedStart stepping).
            var expectedMinKm = (heldFrames - 3) * 8.0 / 1000.0;
            Assert.That(after.distanceKilometres, Is.GreaterThan((float)expectedMinKm),
                $"expected roughly {heldFrames * 8}m of accumulated distance from {heldFrames} held frames, got {after.distanceKilometres * 1000f:F1}m");

            var result = home.FinishWalk();
            Assert.That(result.distanceKilometres, Is.EqualTo(after.distanceKilometres).Within(0.0001f),
                "FinishWalk's final snapshot must match the last live reading when no further movement occurred");
        }

        [UnityTest]
        public IEnumerator TappingSpace_DuringAWalk_IncrementsRealStepCount_AndTicksTheUiLive()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            Assert.That(home, Is.Not.Null);
            Assert.That(home.CompleteSetup("Real Step Test"), Is.True);
            yield return null;

            var runtime = UiPrototypeRuntime.Instance;
            Assert.That(runtime.WalkProvider, Is.InstanceOf<RealWalkMetricsProvider>(),
                "this test only proves anything if the real provider (not a test double) is wired up");

            home.BeginWalk();
            yield return null;
            var before = runtime.WalkProvider.GetLiveMetrics();
            Assert.That(before.hasSteps, Is.True, "the Editor step counter simulation should report a trustworthy count");
            Assert.That(before.steps, Is.EqualTo(0));

            // DeviceStepCounterService's Editor simulation increments on the space bar's rising edge
            // (wasPressedThisFrame), so each tap needs a press frame followed by a release frame.
            const int taps = 7;
            for (var i = 0; i < taps; i++)
            {
                TapSpace();
                yield return null;
                ReleaseSpace();
                yield return null;
            }

            var after = runtime.WalkProvider.GetLiveMetrics();
            Assert.That(after.steps, Is.EqualTo(taps), $"expected {taps} discrete space-bar taps to register as {taps} steps");

            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var stepsLabel = root.Q(className: "walk-summary-row")?.Q(className: "blossom-value")?.Q<Label>(className: "metric-value");
            Assert.That(stepsLabel, Is.Not.Null, "the walk-control-card's steps mini-metric must be present during an active walk");
            Assert.That(stepsLabel.text, Is.EqualTo(taps.ToString("N0")), "the on-screen steps label must reflect the real step counter live");

            var result = home.FinishWalk();
            Assert.That(result.steps, Is.EqualTo(after.steps),
                "FinishWalk's final snapshot must match the last live reading when no further steps occurred");
        }

        // No manual InputSystem.Update() here, deliberately: InputTestFixture already syncs one InputSystem
        // update to each real player-loop frame. DeviceStepCounterService's step count is edge-triggered
        // (wasPressedThisFrame), so it must transition during that same frame's automatic sync - an extra
        // manual Update() call right after queuing consumes the rising edge one tick early, before
        // DeviceStepCounterService.Update() ever sees it, so steps silently stayed at 0.
        static void TapSpace()
        {
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        }

        static void ReleaseSpace()
        {
            if (Keyboard.current == null) return;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        }

        static void PressUpArrow()
        {
            var keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
            InputSystem.Update();
        }

        static void ReleaseArrowKeys()
        {
            if (Keyboard.current == null) return;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.Update();
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
