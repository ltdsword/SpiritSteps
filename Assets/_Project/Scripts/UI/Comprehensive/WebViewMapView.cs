using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ARWalking.UI
{
    /// <summary>Owns a native WebView (via an injected IWebViewBridge - GreeWebViewBridge in production)
    /// hosting Assets/StreamingAssets/spiritsteps_map.html. Positioned in raw screen pixels via SetMargins,
    /// independent of Unity's UI Toolkit layout - the docked top/bottom bars in HomeUiController compute those
    /// margins (see WebViewMapMargins) and this class only ever forwards them to the bridge.</summary>
    public sealed class WebViewMapView : MonoBehaviour
    {
        const string HtmlFileName = "spiritsteps_map.html";

        public bool IsAvailable { get; private set; } = true;
        public event Action AvailabilityChanged;
        public event Action<string> OnMarkerTapped;

        IWebViewBridge _bridge;
        bool _pageReady;
        string _pendingStateJson;

        public void Initialize(IWebViewBridge bridge)
        {
            _bridge = bridge;
            StartCoroutine(SetUp());
        }

        public void SetMargins(int left, int top, int right, int bottom) => _bridge?.SetMargins(left, top, right, bottom);
        public void SetActive(bool active) => _bridge?.SetVisibility(active);

        public void Render(GeoPoint player, IReadOnlyList<WebViewMapMarker> markers)
        {
            var json = BuildStateJson(player, markers);
            if (!_pageReady) { _pendingStateJson = json; return; }
            PushState(json);
        }

        IEnumerator SetUp()
        {
            _bridge.Init(OnMessageFromBridge, OnBridgeError, _ => OnPageLoaded());

            while (!_bridge.IsInitialized) yield return null;

            string stagedUrl = null;
            Exception stagingError = null;
            yield return StageHtmlAsset(url => stagedUrl = url, e => stagingError = e);
            if (stagingError != null)
            {
                OnBridgeError($"failed to stage {HtmlFileName}: {stagingError.Message}");
                yield break;
            }

            _bridge.LoadURL(stagedUrl);
        }

        // Cross-platform StreamingAssets loading, following gree/unity-webview's own documented sample pattern:
        // on Android, streamingAssetsPath is a jar:// URL that needs UnityWebRequest; elsewhere it's a plain
        // file path.
        IEnumerator StageHtmlAsset(Action<string> onStaged, Action<Exception> onError)
        {
            string src = Path.Combine(Application.streamingAssetsPath, HtmlFileName);
            string dst = Path.Combine(Application.temporaryCachePath, HtmlFileName);
            byte[] bytes;

            if (src.Contains("://"))
            {
                using var req = UnityWebRequest.Get(src);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError(new Exception(req.error));
                    yield break;
                }
                bytes = req.downloadHandler.data;
            }
            else
            {
                bytes = File.ReadAllBytes(src);
            }

            File.WriteAllBytes(dst, bytes);
            onStaged("file://" + dst.Replace(" ", "%20"));
        }

        void OnPageLoaded()
        {
            _pageReady = true;
            if (_pendingStateJson != null) { PushState(_pendingStateJson); _pendingStateJson = null; }
        }

        void OnBridgeError(string message)
        {
            Debug.LogWarning($"[WebViewMapView] {message}");
            if (!IsAvailable) return;
            IsAvailable = false;
            AvailabilityChanged?.Invoke();
        }

        void OnMessageFromBridge(string message)
        {
            var parts = message.Split(',');
            if (parts.Length != 2 || parts[0] != "marker") return;
            OnMarkerTapped?.Invoke(parts[1]);
        }

        void PushState(string json) => _bridge.EvaluateJS("window.mapBridge && window.mapBridge.update(" + JsStringLiteral(json) + ");");

        static string BuildStateJson(GeoPoint player, IReadOnlyList<WebViewMapMarker> markers)
        {
            var sb = new StringBuilder();
            sb.Append("{\"player\":{\"lat\":").Append(player.lat.ToString("F6", CultureInfo.InvariantCulture))
              .Append(",\"lon\":").Append(player.lon.ToString("F6", CultureInfo.InvariantCulture)).Append("},\"markers\":[");
            for (int i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":\"").Append(Escape(marker.id)).Append("\",\"label\":\"").Append(Escape(marker.label))
                  .Append("\",\"lat\":").Append(marker.location.lat.ToString("F6", CultureInfo.InvariantCulture))
                  .Append(",\"lon\":").Append(marker.location.lon.ToString("F6", CultureInfo.InvariantCulture)).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static string Escape(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        static string JsStringLiteral(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
