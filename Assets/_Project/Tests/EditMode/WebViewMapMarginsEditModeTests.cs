using ARWalking.UI;
using NUnit.Framework;

namespace ARWalking.Tests.EditMode
{
    public sealed class WebViewMapMarginsEditModeTests
    {
        [Test]
        public void Compute_TypicalBars_ReturnsTopAndBottomMarginsOnly()
        {
            var (left, top, right, bottom) = WebViewMapMargins.Compute(
                topBarBottomEdgeScreenPx: 220f, bottomBarTopEdgeScreenPx: 1800f,
                screenWidth: 1080, screenHeight: 2200);

            Assert.That(left, Is.EqualTo(0));
            Assert.That(right, Is.EqualTo(0));
            Assert.That(top, Is.EqualTo(220));
            Assert.That(bottom, Is.EqualTo(400)); // 2200 - 1800
        }

        [Test]
        public void Compute_ClampsToScreenBounds_NeverNegativeOrOverflowing()
        {
            var (_, top, _, bottom) = WebViewMapMargins.Compute(
                topBarBottomEdgeScreenPx: -50f, bottomBarTopEdgeScreenPx: 5000f,
                screenWidth: 1080, screenHeight: 2200);

            Assert.That(top, Is.EqualTo(0));
            Assert.That(bottom, Is.EqualTo(0));
        }
    }
}
