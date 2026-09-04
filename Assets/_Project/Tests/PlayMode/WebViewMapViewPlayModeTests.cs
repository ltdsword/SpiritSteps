using System;
using System.Collections;
using System.Collections.Generic;
using ARWalking.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ARWalking.Tests.PlayMode
{
    public sealed class WebViewMapViewPlayModeTests
    {
        sealed class FakeWebViewBridge : IWebViewBridge
        {
            public Action<string> OnMessage;
            public Action<string> OnError;
            public Action<string> OnLoaded;
            public bool IsInitialized { get; set; } = true;
            public bool Visible;
            public string LastLoadedUrl;
            public string LastEvaluatedJs;
            public int LeftMargin, TopMargin, RightMargin, BottomMargin;

            public void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded)
            {
                OnMessage = onMessage; OnError = onError; OnLoaded = onLoaded;
            }
            public void SetMargins(int left, int top, int right, int bottom)
            {
                LeftMargin = left; TopMargin = top; RightMargin = right; BottomMargin = bottom;
            }
            public void SetVisibility(bool visible) => Visible = visible;
            public void LoadURL(string url) => LastLoadedUrl = url;
            public void EvaluateJS(string js) => LastEvaluatedJs = js;
        }

        GameObject _host;
        WebViewMapView _view;
        FakeWebViewBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("webview-map-view-test-host");
            _view = _host.AddComponent<WebViewMapView>();
            _bridge = new FakeWebViewBridge();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.Destroy(_host);

        [UnityTest]
        public IEnumerator Initialize_StagesAndLoadsTheBundledHtmlPage()
        {
            _view.Initialize(_bridge);
            yield return null; yield return null;
            _bridge.OnLoaded?.Invoke(null);
            yield return null;

            Assert.That(_bridge.LastLoadedUrl, Does.Contain("spiritsteps_map.html"));
        }

        [UnityTest]
        public IEnumerator OnMarkerTapped_ParsesBridgeMessage_RaisesEventWithLandmarkId()
        {
            _view.Initialize(_bridge);
            yield return null;
            string tappedId = null;
            _view.OnMarkerTapped += id => tappedId = id;

            _bridge.OnMessage("marker,central-post-office");

            Assert.That(tappedId, Is.EqualTo("central-post-office"));
        }

        [UnityTest]
        public IEnumerator Render_BeforePageReady_QueuesState_FlushesOnLoad()
        {
            _view.Initialize(_bridge);
            yield return null;

            var player = new GeoPoint(10.7798, 106.6997);
            _view.Render(player, new List<WebViewMapMarker> { new WebViewMapMarker("central-post-office", "Central Post Office", player) });
            Assert.That(_bridge.LastEvaluatedJs, Is.Null, "must not push state before the page reports it's loaded");

            _bridge.OnLoaded?.Invoke(null);
            yield return null;

            Assert.That(_bridge.LastEvaluatedJs, Does.Contain("mapBridge.update"));
            Assert.That(_bridge.LastEvaluatedJs, Does.Contain("central-post-office"));
            Assert.That(_bridge.LastEvaluatedJs, Does.Contain("10.7798"));
        }

        [UnityTest]
        public IEnumerator SetMargins_ForwardsDirectlyToTheBridge()
        {
            _view.Initialize(_bridge);
            yield return null;

            _view.SetMargins(0, 220, 0, 400);

            Assert.That((_bridge.LeftMargin, _bridge.TopMargin, _bridge.RightMargin, _bridge.BottomMargin), Is.EqualTo((0, 220, 0, 400)));
        }

        [UnityTest]
        public IEnumerator SetActive_ForwardsToBridgeVisibility()
        {
            _view.Initialize(_bridge);
            yield return null;

            _view.SetActive(false);
            Assert.That(_bridge.Visible, Is.False);
            _view.SetActive(true);
            Assert.That(_bridge.Visible, Is.True);
        }

        [UnityTest]
        public IEnumerator BridgeReportsError_BecomesUnavailable()
        {
            var becameUnavailable = false;
            _view.AvailabilityChanged += () => becameUnavailable = !_view.IsAvailable;
            _view.Initialize(_bridge);
            yield return null;

            _bridge.OnError("simulated: WebView init failed");

            Assert.That(_view.IsAvailable, Is.False);
            Assert.That(becameUnavailable, Is.True);
        }
    }
}
