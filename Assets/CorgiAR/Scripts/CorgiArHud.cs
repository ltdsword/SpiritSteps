using UnityEngine;
using UnityEngine.UI;

namespace CorgiAR
{
    /// <summary>
    /// Drives the uGUI heads-up display: status line and the command row (come
    /// here, feed, throw ball, photo). Also shows a short-lived toast on the
    /// status line. Manual/Automatic mode, Sit, "hungry" demo toggle, and the
    /// full-screen Pet Menu sheet were dropped in favor of automatic-only roaming
    /// and the single-tap "Đổi thú" card (<see cref="PetCycleCard"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CorgiArHud : MonoBehaviour
    {
        [SerializeField] private DogARPlacementController placement;
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private DogFeedingController feeding;
        [SerializeField] private PetMoodController mood;
        [SerializeField] private ArPhotoCapture photo;

        [Header("Widgets")]
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button comeButton;
        [SerializeField] private Button photoButton;
        [SerializeField] private GameObject foodButtonObject;
        [SerializeField] private GameObject ballButtonObject;

        private float toastUntil;
        private string toastText;

        /// <summary>Flash a message on the status line for a few seconds.</summary>
        public void ShowToast(string message, float seconds)
        {
            toastText = message;
            toastUntil = Time.unscaledTime + Mathf.Max(0.5f, seconds);
        }

        /// <summary>Shows/hides the Capture button - used by PetArContextBinder to gate photo
        /// mode from the ARWalking-side entry point context. Photo mode is the only HUD element
        /// not otherwise driven by Update(), so this stays sticky once set.</summary>
        public void SetPhotoModeEnabled(bool enabled)
        {
            if (photoButton != null) photoButton.gameObject.SetActive(enabled);
        }

        private void OnEnable()
        {
            if (comeButton != null)
                comeButton.onClick.AddListener(() => companion?.ComeHere());
            if (photoButton != null)
                photoButton.onClick.AddListener(() => photo?.Capture());
        }

        private void OnDisable()
        {
            comeButton?.onClick.RemoveAllListeners();
            photoButton?.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            bool placed = placement != null && placement.IsPlaced;
            bool eating = feeding != null && feeding.IsEating;
            bool sitting = companion != null && companion.IsSitting;

            bool planeDetected = placement != null && placement.HasDetectedPlane;
            if (statusLabel != null)
            {
                statusLabel.text = Time.unscaledTime < toastUntil
                    ? toastText
                    : StatusFor(placed, eating, sitting, planeDetected);
            }

            if (foodButtonObject != null)
                foodButtonObject.SetActive(placed);
            if (ballButtonObject != null)
                ballButtonObject.SetActive(placed && !eating);

            SetActive(comeButton, placed && !eating && !sitting);
        }

        private string StatusFor(bool placed, bool eating, bool sitting, bool planeDetected)
        {
            if (!placed)
                return planeDetected
                    ? "Đã tìm thấy mặt phẳng — đang đặt thú…"
                    : "Đưa camera quét từ từ quanh sàn/bàn để tìm mặt phẳng…";
            if (eating) return "Đang ăn… ngon quá!";
            if (sitting) return "Thú đang ngồi ngoan";

            PetMood m = mood != null ? mood.Mood : PetMood.Happy;
            if (m == PetMood.Starving) return "Thú đang rất đói — cho ăn ngay đi!";
            if (m == PetMood.Hungry) return "Thú hơi đói rồi — ném cho miếng ăn nhé";

            return "Thú tự đi quanh bạn • ném bóng, cho ăn, gọi lại đây";
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active)
                c.gameObject.SetActive(active);
        }
    }
}
