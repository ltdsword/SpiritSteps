using System;
using System.Collections;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ARWalking.Tests.PlayMode
{
    public sealed class WebViewMapDockingPlayModeTests
    {
        sealed class RecordingWebViewBridge : IWebViewBridge
        {
            public int LeftMargin, TopMargin, RightMargin, BottomMargin;
            public bool Visible = true;
            public string LastMessageHandlerProbe;
            public string LastEvaluatedJs;
            public int EvaluateJsCallCount;
            Action<string> _onMessage;

            public bool IsInitialized => true;
            public void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded)
            {
                _onMessage = onMessage;
                onLoaded(null); // page "ready" immediately - these tests don't need real HTML staging
            }
            public void SetMargins(int left, int top, int right, int bottom)
            {
                LeftMargin = left; TopMargin = top; RightMargin = right; BottomMargin = bottom;
            }
            public void SetVisibility(bool visible) => Visible = visible;
            public void LoadURL(string url) { }
            public void EvaluateJS(string js) { LastEvaluatedJs = js; EvaluateJsCallCount++; }
            public void SimulateMarkerTap(string landmarkId) => _onMessage?.Invoke("marker," + landmarkId);
        }

        string _savePath;
        RecordingWebViewBridge _bridge;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UiPrototypeRuntime.Instance != null) { UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject); yield return null; }
            _savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ar-walking-webview-dock-" + Guid.NewGuid().ToString("N"), LocalPlayerSaveStore.FileName);
            _bridge = new RecordingWebViewBridge();
            UiPrototypeRuntime.ClearTestOverrides();
            UiPrototypeRuntime.TestSavePathOverride = _savePath;
            UiPrototypeRuntime.TestWebViewBridgeOverride = _bridge;
            SceneManager.LoadScene("Home");
            yield return WaitForScene("Home");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (UiPrototypeRuntime.Instance != null) UnityEngine.Object.Destroy(UiPrototypeRuntime.Instance.gameObject);
            yield return null;
            UiPrototypeRuntime.ClearTestOverrides();
        }

        static IEnumerator WaitForScene(string name)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
        }

        HomeUiController CreateProfile()
        {
            var home = UnityEngine.Object.FindFirstObjectByType<HomeUiController>();
            home.CompleteSetup("Docking Test");
            return home;
        }

        [UnityTest]
        public IEnumerator MapPage_UsesDockedBars_NotAbsoluteFloatingOverlay()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;

            var root = home.GetComponent<UIDocument>().rootVisualElement;
            var topBar = root.Q(className: "map-top-bar");
            Assert.That(topBar, Is.Not.Null);
            // Within(1f) - the runtime panel's ScaleWithScreenSize mode can snap sub-pixel layout values
            // to the device-pixel grid, so an exact 0.1px tolerance is flaky across test-runner window sizes.
            Assert.That(topBar.resolvedStyle.marginLeft, Is.EqualTo(44f).Within(1f));
            Assert.That(topBar.resolvedStyle.marginRight, Is.EqualTo(44f).Within(1f));
            Assert.That(topBar.resolvedStyle.marginTop, Is.EqualTo(24f).Within(1f));
            Assert.That(root.Q(className: "map-bottom-bar"), Is.Not.Null);
            Assert.That(root.Q(className: "map-top-overlay"), Is.Null, "the old floating overlay must not be built when the webview map is available");
        }

        [UnityTest]
        public IEnumerator MapPage_ComputesAndAppliesWebViewMargins()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;

            Assert.That(_bridge.TopMargin, Is.GreaterThan(0));
            Assert.That(_bridge.BottomMargin, Is.GreaterThan(0));
            Assert.That(_bridge.LeftMargin, Is.EqualTo(0));
            Assert.That(_bridge.RightMargin, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator MarkerTapped_OpensLandmarkSheet()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;

            _bridge.SimulateMarkerTap(PrototypeIds.CentralPostOffice);
            yield return null;

            // A marker tap opens the floating Landmark sheet over the map rather than pushing a route.
            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.HomeMap));
            var root = home.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q(className: "landmark-sheet"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator OpeningSettingsOverlay_HidesWebView_ClosingRestoresIt()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;
            Assert.That(_bridge.Visible, Is.True);

            home.ShowOverlay(UiOverlay.Settings);
            yield return null;
            Assert.That(_bridge.Visible, Is.False);

            // Closing goes through IAppNavigator.CloseOverlay directly (confirmed against HomeUiController's
            // actual settings-modal "Close" button, which wires to _runtime.Navigator.CloseOverlay - calling it
            // directly via the public UiPrototypeRuntime.Instance is more robust than querying for the button
            // by name, since ActionWithIcon doesn't assign it one).
            UiPrototypeRuntime.Instance.Navigator.CloseOverlay();
            yield return null;
            Assert.That(_bridge.Visible, Is.True);
        }

        [UnityTest]
        public IEnumerator MapPushes_AreSkippedWhenNothingChanged_ButRepushOnWalkStartWithATrail()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;

            var idleCallCount = _bridge.EvaluateJsCallCount;
            yield return null; yield return null; yield return null;
            Assert.That(_bridge.EvaluateJsCallCount, Is.EqualTo(idleCallCount),
                "player fix and walking state are both unchanged across these frames, so no redundant JS pushes");

            home.BeginWalk();
            yield return null;

            Assert.That(_bridge.EvaluateJsCallCount, Is.GreaterThan(idleCallCount),
                "starting a walk flips walking state even before the GPS fix itself moves, and must still push");
            // LastEvaluatedJs is a JS string literal wrapping the JSON payload, so its quotes are backslash-escaped.
            Assert.That(_bridge.LastEvaluatedJs, Does.Contain("\\\"trail\\\":[["), "the walk's starting point seeds the trail");
        }

        [UnityTest]
        public IEnumerator StartingAWalk_KeepsWebViewVisible()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;
            Assert.That(_bridge.Visible, Is.True);

            home.BeginWalk();
            yield return null;

            Assert.That(home.CurrentRoute, Is.EqualTo(UiRoute.ActiveWalk));
            Assert.That(_bridge.Visible, Is.True, "starting a walk must not hide the map WebView - ActiveWalk still renders the real map");
        }

        [UnityTest]
        public IEnumerator LeavingHomeScene_HidesNativeWebView()
        {
            var home = CreateProfile();
            yield return null; yield return null; yield return null;
            Assert.That(_bridge.Visible, Is.True);

            // A real scene change destroys Home's AppUIRoot while UiPrototypeRuntime (and therefore
            // its native WebView) survives via DontDestroyOnLoad. Reproduce that lifecycle directly.
            UnityEngine.Object.Destroy(home.gameObject);
            yield return null;

            Assert.That(_bridge.Visible, Is.False,
                "the native map surface must be hidden before an AR scene starts rendering");
        }
    }
}
