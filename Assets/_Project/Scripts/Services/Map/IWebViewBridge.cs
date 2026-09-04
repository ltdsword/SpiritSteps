using System;

namespace ARWalking.UI
{
    /// <summary>Seam over "the thing that hosts a native WebView" so WebViewMapView's own logic (message
    /// parsing, JSON building, margin/visibility calls) is testable without a real WebViewObject - which is a
    /// genuine native plugin, not something a test double can convincingly stand in for. GreeWebViewBridge is
    /// the only production implementation.</summary>
    public interface IWebViewBridge
    {
        void Init(Action<string> onMessage, Action<string> onError, Action<string> onLoaded);
        bool IsInitialized { get; }
        void SetMargins(int left, int top, int right, int bottom);
        void SetVisibility(bool visible);
        void LoadURL(string url);
        void EvaluateJS(string js);
    }
}
