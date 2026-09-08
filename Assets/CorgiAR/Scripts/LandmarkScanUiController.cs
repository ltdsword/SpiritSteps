using ARWalking.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CorgiAR
{
    /// <summary>
    /// UI and reward flow for the standalone Landmark scanner. This intentionally lives in the
    /// LandmarkScan scene and never enables or depends on the companion AR controllers.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class LandmarkScanUiController : MonoBehaviour
    {
        private const string LandmarkId = PrototypeIds.NotreDameBasilica;
        private const string RewardPetId = PrototypeIds.Bull;

        private UIDocument document;
        private LandmarkImageTrackingController tracker;
        private UiPrototypeRuntime runtime;
        private VisualElement appRoot;
        private bool stampCollected;
        private bool showInfoCard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= BootstrapInLandmarkScene;
            SceneManager.sceneLoaded += BootstrapInLandmarkScene;
        }

        private static void BootstrapInLandmarkScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "LandmarkScan" ||
                FindFirstObjectByType<LandmarkScanUiController>() != null)
                return;

            var uiObject = new GameObject("Landmark Scan UI");
            var uiDocument = uiObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = Resources.Load<PanelSettings>("UI/ARWalkingArPanelSettings");
            uiObject.AddComponent<LandmarkScanUiController>();
        }

        private void Start()
        {
            runtime = UiPrototypeRuntime.EnsureExists();
            document = GetComponent<UIDocument>();
            if (document.panelSettings == null)
                document.panelSettings = Resources.Load<PanelSettings>("UI/ARWalkingArPanelSettings");

            tracker = FindFirstObjectByType<LandmarkImageTrackingController>();
            if (tracker != null)
            {
                tracker.TargetRecognized += OnTargetRecognized;
                tracker.ContentTapped += OnContentTapped;
            }

            stampCollected = runtime.SaveData != null &&
                             runtime.SaveData.completedLandmarkIds.Contains(LandmarkId);
            BuildRoot();
            Render();
        }

        private void OnDestroy()
        {
            if (tracker != null)
            {
                tracker.TargetRecognized -= OnTargetRecognized;
                tracker.ContentTapped -= OnContentTapped;
            }
        }

        private void BuildRoot()
        {
            var root = document.rootVisualElement;
            root.Clear();
            root.style.backgroundColor = new StyleColor(Color.clear);
            var styleSheet = Resources.Load<StyleSheet>("UI/ARWalking");
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            appRoot = new VisualElement { name = "landmark-scan-root" };
            appRoot.AddToClassList("app-root");
            appRoot.AddToClassList("ar-panel-root");
            appRoot.style.flexGrow = 1f;
            appRoot.style.backgroundColor = new StyleColor(Color.clear);
            root.Add(appRoot);
        }

        private void OnTargetRecognized() => Render();
        private void OnContentTapped()
        {
            if (!tracker.Recognized || showInfoCard)
                return;
            showInfoCard = true;
            Render();
        }

        private void Render()
        {
            if (appRoot == null)
                return;

            appRoot.Clear();
            var page = Element("landmark-scan-page", "landmark-scan-page");
            appRoot.Add(page);

            var header = Element("landmark-scan-header", "landmark-scan-header");
            var back = new Button(ReturnToJourney) { text = "‹", name = "landmark-scan-back" };
            back.AddToClassList("icon-button");
            back.AddToClassList("dark-round-control");
            header.Add(back);
            var titlePill = Element("landmark-scan-title-pill", "landmark-scan-title-pill");
            titlePill.Add(Text("Scan Notre-Dame Basilica", "subtitle"));
            header.Add(titlePill);
            page.Add(header);

            if (tracker == null)
            {
                var controls = Element("ar-scan-controls", "ar-scan-controls");
                controls.Add(Instruction("Image recognition tracker not found."));
                page.Add(controls);
                return;
            }

            if (!tracker.Recognized)
            {
                page.Add(Element("ar-scanning-frame", "ar-scanning-frame"));
                var controls = Element("ar-scan-controls", "ar-scan-controls");
                controls.Add(Instruction("Point your camera at the Notre-Dame Basilica image"));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                controls.Add(ActionButton("Simulate recognition", tracker.SimulateRecognitionForEditor, "secondary-action"));
#endif
                page.Add(controls);
                return;
            }

            if (!showInfoCard)
            {
                var controls = Element("ar-scan-controls", "ar-scan-controls");
                controls.Add(Instruction("Notre-Dame Basilica recognized! Tap the model to learn its story."));
                page.Add(controls);
                return;
            }

            BuildRecognitionResult(page);
        }

        private void BuildRecognitionResult(VisualElement page)
        {
            var sheet = Element("landmark-scan-result-sheet", "landmark-scan-result-sheet");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "landmark-scan-result-scroll" };
            scroll.AddToClassList("landmark-scan-result-scroll");
            scroll.Add(Text("Recognized!", "title"));
            scroll.Add(Text("NOTRE-DAME BASILICA OF SAIGON", "eyebrow"));

            scroll.Add(Text("HISTORY", "landmark-scan-section-title"));
            scroll.Add(Text(
                "Construction began on 7 October 1877 and the basilica was completed in 1880, designed by French architect Jules Bourard.",
                "landmark-scan-result-body"));

            scroll.Add(Text("CULTURAL & SOCIAL SIGNIFICANCE", "landmark-scan-section-title"));
            scroll.Add(Text(
                "Through many historical changes, Notre-Dame Cathedral Basilica of Saigon has grown beyond a religious building to become the soul and an inseparable cultural heritage of the southern city.",
                "landmark-scan-result-body"));

            var reward = Element("landmark-reward-card", "landmark-reward-card");
            var rewardImage = new Image
            {
                name = "landmark-reward-image",
                image = RewardPetTexture(),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            rewardImage.AddToClassList("landmark-reward-image");
            reward.Add(rewardImage);
            var rewardCopy = Element("landmark-reward-copy", "landmark-reward-copy");
            rewardCopy.Add(Text("Bull", "subtitle"));
            rewardCopy.Add(Text(stampCollected ? "Already in your collection" : "Discovery reward", "body"));
            reward.Add(rewardCopy);
            scroll.Add(reward);

            scroll.Add(stampCollected
                ? ActionButton("Back to Journey", ReturnToJourney, "primary-action")
                : ActionButton("Claim Bull & Stamp", CollectReward, "primary-action"));
            sheet.Add(scroll);
            page.Add(sheet);
        }

        private Texture2D RewardPetTexture()
        {
            if (runtime?.Assets == null || runtime.Data == null)
                return null;
            for (var i = 0; i < runtime.Data.Companions.Count; i++)
                if (runtime.Data.Companions[i].id == RewardPetId)
                    return runtime.Assets.Companion(i);
            return null;
        }

        private void CollectReward()
        {
            runtime.CompleteLandmarkMemory(LandmarkId);
            stampCollected = true;
            Render();
        }

        private void ReturnToJourney() => runtime.ReturnFromLandmarkScan();

        private static VisualElement Instruction(string value)
        {
            var pill = Element(null, "ar-instruction-pill");
            pill.Add(Text(value, "body"));
            return pill;
        }

        private static Button ActionButton(string label, System.Action action, params string[] classes)
        {
            var button = new Button(action) { text = label };
            button.AddToClassList("action-button");
            foreach (var className in classes)
                button.AddToClassList(className);
            return button;
        }

        private static Label Text(string value, string className)
        {
            var label = new Label(value);
            label.AddToClassList(className);
            return label;
        }

        private static VisualElement Element(string name, params string[] classes)
        {
            var element = new VisualElement { name = name };
            foreach (var className in classes)
                element.AddToClassList(className);
            return element;
        }
    }
}
