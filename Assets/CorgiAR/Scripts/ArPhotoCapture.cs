using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ARWalking.UI;

namespace CorgiAR
{
    /// <summary>
    /// Takes a photo of the AR view: hides the HUD for one frame, grabs the
    /// framebuffer, and hands the PNG bytes to <see cref="UiPrototypeRuntime.SaveArPhoto"/>,
    /// which writes the file, records it in the local save, and links it to a Journey entry
    /// (the current Landmark's entry, or a new/updated per-pet-per-day entry - see
    /// docs/AR-3D-INTEGRATION-CONTRACT.md). Then plays a white flash and toasts the result.
    /// (Saving straight to the device photo gallery would need a native plugin - out of scope.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArPhotoCapture : MonoBehaviour
    {
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private Graphic flash;
        [SerializeField] private CorgiArHud hud;
        [SerializeField, Min(0.05f)] private float flashSeconds = 0.25f;

        private bool capturing;

        public void Capture()
        {
            if (!capturing)
                StartCoroutine(CaptureRoutine());
        }

        private IEnumerator CaptureRoutine()
        {
            capturing = true;
            bool hudWasOn = hudCanvas != null && hudCanvas.enabled;
            if (hudCanvas != null) hudCanvas.enabled = false;

            yield return new WaitForEndOfFrame();

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            string message;
            try
            {
                UiPrototypeRuntime.Instance.SaveArPhoto(shot.EncodeToPNG());
                message = "Đã lưu ảnh vào Journey";
            }
            catch (Exception e)
            {
                message = "Lưu ảnh lỗi: " + e.Message;
            }
            Destroy(shot);

            if (hudCanvas != null) hudCanvas.enabled = hudWasOn;
            if (hud != null) hud.ShowToast(message, 3.5f);

            if (flash != null)
            {
                float t = 0f;
                Color c = flash.color;
                while (t < flashSeconds)
                {
                    t += Time.deltaTime;
                    c.a = Mathf.Lerp(1f, 0f, t / flashSeconds);
                    flash.color = c;
                    yield return null;
                }
                c.a = 0f;
                flash.color = c;
            }

            capturing = false;
        }
    }
}
