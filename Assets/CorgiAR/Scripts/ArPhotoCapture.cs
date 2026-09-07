using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
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
    ///
    /// <paramref name="hudCanvas"/>/<paramref name="flash"/>/<paramref name="hud"/> are the
    /// legacy uGUI hooks (still used by the vestigial SampleScene HUD). The UI Toolkit glass HUD
    /// (<see cref="CorgiArGlassHud"/>) instead assigns <see cref="HudDocument"/> and listens to
    /// <see cref="ToastRequested"/>/<see cref="FlashRequested"/>/<see cref="PhotosChanged"/> -
    /// both hook sets are optional and independent, so this component stays shared.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArPhotoCapture : MonoBehaviour
    {
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private Graphic flash;
        [SerializeField] private CorgiArHud hud;
        [SerializeField] private UIDocument hudDocument;
        [SerializeField, Min(0.05f)] private float flashSeconds = 0.25f;
        [SerializeField, Min(1)] private int maxSessionPhotos = 20;

        private readonly List<Texture2D> sessionPhotos = new();
        private bool capturing;

        public event Action<string> ToastRequested;
        public event Action FlashRequested;
        public event Action PhotosChanged;
        public IReadOnlyList<Texture2D> SessionPhotos => sessionPhotos;

        /// <summary>Lets the UI Toolkit glass HUD register its own <see cref="UIDocument"/> to
        /// hide during capture, without needing a serialized-field wiring step.</summary>
        public void SetHudDocument(UIDocument doc) => hudDocument = doc;

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
            bool hudDocWasOn = hudDocument != null && hudDocument.rootVisualElement != null &&
                                hudDocument.rootVisualElement.style.display != DisplayStyle.None;
            if (hudDocument != null && hudDocument.rootVisualElement != null)
                hudDocument.rootVisualElement.style.display = DisplayStyle.None;

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

            if (hudCanvas != null) hudCanvas.enabled = hudWasOn;
            if (hudDocument != null && hudDocument.rootVisualElement != null && hudDocWasOn)
                hudDocument.rootVisualElement.style.display = DisplayStyle.Flex;
            hud?.ShowToast(message, 3.5f);
            ToastRequested?.Invoke(message);

            sessionPhotos.Add(shot);
            while (sessionPhotos.Count > maxSessionPhotos)
            {
                Destroy(sessionPhotos[0]);
                sessionPhotos.RemoveAt(0);
            }
            PhotosChanged?.Invoke();

            FlashRequested?.Invoke();
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

        private void OnDestroy()
        {
            foreach (Texture2D photo in sessionPhotos)
                if (photo != null) Destroy(photo);
            sessionPhotos.Clear();
        }
    }
}
