using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>Converts the docked top/bottom bars' resolved screen-space edges into the raw-pixel margins
    /// WebViewObject.SetMargins needs to shrink the native WebView to exactly the rectangle left between them.
    /// Deliberately a pure function (no UnityEngine.Screen/VisualElement) so it's testable with plain numbers -
    /// the docked bars already live inside HomeUiController's safe-area-padded root, so their resolved edges
    /// already reflect Screen.safeArea correctly; this only does the last step.</summary>
    public static class WebViewMapMargins
    {
        public static (int left, int top, int right, int bottom) Compute(
            float topBarBottomEdgeScreenPx, float bottomBarTopEdgeScreenPx, int screenWidth, int screenHeight)
        {
            var top = Mathf.Clamp(Mathf.RoundToInt(topBarBottomEdgeScreenPx), 0, screenHeight);
            var bottom = Mathf.Clamp(Mathf.RoundToInt(screenHeight - bottomBarTopEdgeScreenPx), 0, screenHeight);
            return (0, top, 0, bottom);
        }
    }
}
