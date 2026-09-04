using UnityEngine;
using UnityEngine.UI;

namespace CorgiAR
{
    /// <summary>
    /// Drives the uGUI heads-up display: status line, Manual/Automatic toggle,
    /// pet-menu button, and the command buttons (sit/stand, come here, "hungry"
    /// demo toggle, photo). Also shows a short-lived toast on the status line.
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
        [SerializeField] private Button manualButton;
        [SerializeField] private Button autoButton;
        [SerializeField] private Button petMenuButton;
        [SerializeField] private Button sitButton;
        [SerializeField] private Button comeButton;
        [SerializeField] private Button hungryButton;
        [SerializeField] private Button photoButton;
        [SerializeField] private GameObject joystickObject;
        [SerializeField] private GameObject foodButtonObject;
        [SerializeField] private GameObject ballButtonObject;
        [SerializeField] private PetMenuPanel petPanel;

        [SerializeField] private Color activeMode = new(1f, 0.78f, 0.32f, 0.95f);
        [SerializeField] private Color idleMode = new(1f, 1f, 1f, 0.3f);

        private float toastUntil;
        private string toastText;
        private Text sitLabel;

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
            if (manualButton != null)
                manualButton.onClick.AddListener(() => placement?.SetMode(CompanionControlMode.Manual));
            if (autoButton != null)
                autoButton.onClick.AddListener(() => placement?.SetMode(CompanionControlMode.Automatic));
            if (petMenuButton != null)
                petMenuButton.onClick.AddListener(() => petPanel?.Toggle());
            if (sitButton != null)
            {
                sitButton.onClick.AddListener(() => companion?.ToggleSit());
                sitLabel = sitButton.GetComponentInChildren<Text>();
            }
            if (comeButton != null)
                comeButton.onClick.AddListener(() => companion?.ComeHere());
            if (hungryButton != null)
                hungryButton.onClick.AddListener(() => mood?.ToggleForceHungry());
            if (photoButton != null)
                photoButton.onClick.AddListener(() => photo?.Capture());
        }

        private void OnDisable()
        {
            manualButton?.onClick.RemoveAllListeners();
            autoButton?.onClick.RemoveAllListeners();
            petMenuButton?.onClick.RemoveAllListeners();
            sitButton?.onClick.RemoveAllListeners();
            comeButton?.onClick.RemoveAllListeners();
            hungryButton?.onClick.RemoveAllListeners();
            photoButton?.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            bool placed = placement != null && placement.IsPlaced;
            bool eating = feeding != null && feeding.IsEating;
            bool sitting = companion != null && companion.IsSitting;
            CompanionControlMode mode = companion != null ? companion.Mode
                : (placement != null ? placement.Mode : CompanionControlMode.Manual);

            bool planeDetected = placement != null && placement.HasDetectedPlane;
            if (statusLabel != null)
            {
                statusLabel.text = Time.unscaledTime < toastUntil
                    ? toastText
                    : StatusFor(placed, eating, sitting, mode, planeDetected);
            }

            if (joystickObject != null)
                joystickObject.SetActive(placed && mode == CompanionControlMode.Manual && !eating && !sitting);
            if (foodButtonObject != null)
                foodButtonObject.SetActive(placed);
            if (ballButtonObject != null)
                ballButtonObject.SetActive(placed && !eating);

            SetActive(sitButton, placed && !eating);
            SetActive(comeButton, placed && !eating && !sitting);
            if (sitLabel != null && sitButton != null && sitButton.gameObject.activeSelf)
                sitLabel.text = sitting ? "ĐỨNG DẬY" : "NGỒI";

            SetTint(manualButton, mode == CompanionControlMode.Manual);
            SetTint(autoButton, mode == CompanionControlMode.Automatic);
        }

        private string StatusFor(bool placed, bool eating, bool sitting, CompanionControlMode mode, bool planeDetected)
        {
            if (!placed)
                return planeDetected
                    ? "Đã tìm thấy mặt phẳng — đang đặt thú…"
                    : "Đưa camera quét từ từ quanh sàn/bàn để tìm mặt phẳng…";
            if (eating) return "Đang ăn… ngon quá!";
            if (sitting) return "Thú đang ngồi ngoan • chạm nút để cho đứng dậy";

            PetMood m = mood != null ? mood.Mood : PetMood.Happy;
            if (m == PetMood.Starving) return "Thú đang rất đói — cho ăn ngay đi!";
            if (m == PetMood.Hungry) return "Thú hơi đói rồi — ném cho miếng ăn nhé";

            return mode == CompanionControlMode.Manual
                ? "Kéo joystick để điều khiển • chạm thú để vuốt • chạm 2 lần để ngồi"
                : "Thú tự đi quanh bạn • ném bóng, cho ăn, gọi lại đây";
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active)
                c.gameObject.SetActive(active);
        }

        private void SetTint(Button button, bool active)
        {
            if (button != null && button.TryGetComponent(out Image image))
                image.color = active ? activeMode : idleMode;
        }
    }
}
