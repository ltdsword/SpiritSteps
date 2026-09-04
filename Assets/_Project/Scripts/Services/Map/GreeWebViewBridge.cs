using System;
using Gree.UnityWebView;
using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>Thin adapter over the real net.gree.unity-webview WebViewObject. Adds itself as a component on
    /// the given host GameObject (WebViewObject is itself a MonoBehaviour the plugin expects to live on some
    /// GameObject) - callers never touch WebViewObject directly, only this interface.</summary>
    public sealed class GreeWebViewBridge : IWebViewBridge
    {
        readonly WebViewObject _webViewObject;

        public GreeWebViewBridge(GameObject host)
        {
            _webViewObject = host.AddComponent<WebViewObject>();
        }

        public bool IsInitialized => _webViewObject.IsInitialized();

        public void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded)
        {
            _webViewObject.Init(
                cb: msg => onMessage(msg),
                err: msg => onError(msg),
                httpErr: msg => onError(msg),
                ld: _ => onLoaded(null));
        }

        public void SetMargins(int left, int top, int right, int bottom) => _webViewObject.SetMargins(left, top, right, bottom);
        public void SetVisibility(bool visible) => _webViewObject.SetVisibility(visible);
        public void LoadURL(string url) => _webViewObject.LoadURL(url);
        public void EvaluateJS(string js) => _webViewObject.EvaluateJS(js);
    }
}
