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
        private const string DefaultLandmarkId = PrototypeIds.NotreDameBasilica;

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

            RefreshStampState();
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

        private void OnTargetRecognized()
        {
            showInfoCard = false;
            RefreshStampState();
            Render();
        }
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
            titlePill.Add(Text(tracker != null && tracker.Recognized
                ? "Scan " + CurrentLandmark().name
                : "Scan a Landmark", "subtitle"));
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
                controls.Add(Instruction("Point your camera at a supported Landmark image"));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                controls.Add(ActionButton("Simulate recognition", tracker.SimulateRecognitionForEditor, "secondary-action"));
#endif
                page.Add(controls);
                return;
            }

            if (!showInfoCard)
            {
                var controls = Element("ar-scan-controls", "ar-scan-controls");
                controls.Add(Instruction(CurrentLandmark().name + " recognized! Tap the model to learn its story."));
                page.Add(controls);
                return;
            }

            BuildRecognitionResult(page);
        }

        private void BuildRecognitionResult(VisualElement page)
        {
            LandmarkUiData landmark = CurrentLandmark();
            string rewardPetId = landmark.companionRewardId;
            string rewardPetName = CompanionName(rewardPetId);
            var sheet = Element("landmark-scan-result-sheet", "landmark-scan-result-sheet");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "landmark-scan-result-scroll" };
            scroll.AddToClassList("landmark-scan-result-scroll");
            scroll.Add(Text("Recognized!", "title"));
            scroll.Add(Text(landmark.name.ToUpperInvariant(), "eyebrow"));

            scroll.Add(Text(landmark.id == PrototypeIds.Landmark81
                ? "HISTORY AND DEVELOPMENT"
                : "HISTORY", "landmark-scan-section-title"));
            var history = Text(landmark.history, "landmark-scan-result-body");
            history.name = "landmark-history";
            scroll.Add(history);

            scroll.Add(Text(landmark.id == PrototypeIds.Landmark81
                ? "CULTURAL & SYMBOLIC SIGNIFICANCE"
                : "CULTURAL & HISTORICAL SIGNIFICANCE", "landmark-scan-section-title"));
            scroll.Add(Text(landmark.architecture, "landmark-scan-result-body"));

            var reward = Element("landmark-reward-card", "landmark-reward-card");
            var rewardImage = new Image
            {
                name = "landmark-reward-image",
                image = RewardPetTexture(rewardPetId),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            rewardImage.AddToClassList("landmark-reward-image");
            reward.Add(rewardImage);
            var rewardCopy = Element("landmark-reward-copy", "landmark-reward-copy");
            var rewardName = Text(rewardPetName, "subtitle");
            rewardName.name = "landmark-reward-name";
            rewardCopy.Add(rewardName);
            rewardCopy.Add(Text(stampCollected ? "Already in your collection" : "Discovery reward", "body"));
            reward.Add(rewardCopy);
            scroll.Add(reward);

            scroll.Add(stampCollected
                ? ActionButton("Back to Journey", ReturnToJourney, "primary-action")
                : ActionButton("Claim " + rewardPetName + " & Stamp", CollectReward, "primary-action"));
            sheet.Add(scroll);
            page.Add(sheet);
        }

        private Texture2D RewardPetTexture(string rewardPetId)
        {
            if (runtime?.Assets == null || runtime.Data == null)
                return null;
            for (var i = 0; i < runtime.Data.Companions.Count; i++)
                if (runtime.Data.Companions[i].id == rewardPetId)
                    return runtime.Assets.Companion(i);
            return null;
        }

        private void CollectReward()
        {
            runtime.CompleteLandmarkMemory(CurrentLandmarkId());
            stampCollected = true;
            Render();
        }

        private string CurrentLandmarkId()
        {
            string targetName = tracker != null ? tracker.RecognizedTargetName : null;
            if (!string.IsNullOrEmpty(targetName))
                foreach (LandmarkUiData landmark in runtime.Data.Landmarks)
                    if (landmark.id == targetName)
                        return landmark.id;
            return DefaultLandmarkId;
        }

        private LandmarkUiData CurrentLandmark()
        {
            string id = CurrentLandmarkId();
            foreach (LandmarkUiData landmark in runtime.Data.Landmarks)
                if (landmark.id == id)
                    return landmark;
            return runtime.Data.Landmarks[0];
        }

        private string CompanionName(string id)
        {
            foreach (CompanionUiData companion in runtime.Data.Companions)
                if (companion.id == id)
                    return companion.name;
            return id;
        }

        private void RefreshStampState()
        {
            stampCollected = runtime.SaveData != null &&
                             runtime.SaveData.completedLandmarkIds.Contains(CurrentLandmarkId());
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
