using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CorgiAR
{
    /// <summary>
    /// Small, reusable bridge for the Landmark image-tracking spike. It deliberately owns no
    /// reward/save logic: a production integration can forward the first recognition to the
    /// ARWalking landmark flow after device tracking has been validated.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARTrackedImageManager))]
    public sealed class LandmarkImageTrackingController : MonoBehaviour
    {
        [Serializable]
        private sealed class TrackedContentBinding
        {
            public string targetName;
            public GameObject content;
        }

        [SerializeField] private ARTrackedImageManager imageManager;
        // Kept for backwards compatibility with the original Notre-Dame-only scene.
        [SerializeField] private GameObject trackedContent;
        [SerializeField] private string expectedTargetName = "notre-dame-basilica";
        [SerializeField] private TrackedContentBinding[] trackedContents = Array.Empty<TrackedContentBinding>();
        [SerializeField] private bool showTrackedContent;
        [SerializeField] private bool showLegacyDebugGui;

        private bool recognized;
        private bool currentlyTracking;
        private string recognizedTargetName;
        private GameObject activeTrackedContent;
        private Light landmark81Light;
        private string status = "Point the camera at a supported Landmark target.";

        public bool Recognized => recognized;
        public string ExpectedTargetName => expectedTargetName;
        public string RecognizedTargetName => recognizedTargetName;
        public event Action TargetRecognized;
        /// <summary>Raised when the player taps the 3D landmark model once it is showing on the
        /// tracked image - the UI uses this to reveal the history/info card.</summary>
        public event Action ContentTapped;

        private void Awake()
        {
            if (imageManager == null)
                imageManager = GetComponent<ARTrackedImageManager>();
            HideAllTrackedContent();
        }

        private void OnEnable()
        {
            if (imageManager != null)
                imageManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        private void Update()
        {
            if (activeTrackedContent == null || !activeTrackedContent.activeInHierarchy)
                return;
            Vector2? tapPosition = TapScreenPosition();
            if (tapPosition == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Ray ray = cam.ScreenPointToRay(new Vector3(tapPosition.Value.x, tapPosition.Value.y, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.transform.IsChildOf(activeTrackedContent.transform))
                ContentTapped?.Invoke();
        }

        private static Vector2? TapScreenPosition()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return Mouse.current.position.ReadValue();
            return null;
        }

        private void OnDisable()
        {
            if (imageManager != null)
                imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
        {
            foreach (ARTrackedImage image in changes.added)
                ApplyTrackingState(image);
            foreach (ARTrackedImage image in changes.updated)
                ApplyTrackingState(image);
            foreach (var removed in changes.removed)
            {
                ARTrackedImage image = removed.Value;
                if (image != null && IsExpected(image))
                    SetTrackingLost(image.referenceImage.name);
            }
        }

        private void ApplyTrackingState(ARTrackedImage image)
        {
            if (image == null || !IsExpected(image))
                return;

            if (image.trackingState != TrackingState.Tracking)
            {
                SetTrackingLost(image.referenceImage.name);
                return;
            }

            string targetName = image.referenceImage.name;
            if (recognized && !string.Equals(recognizedTargetName, targetName, StringComparison.Ordinal))
                return;

            currentlyTracking = true;
            status = recognized
                ? targetName + " is being tracked."
                : targetName + " recognized!";

            activeTrackedContent = ContentFor(targetName);
            if (activeTrackedContent != null && showTrackedContent)
            {
                HideAllTrackedContent(activeTrackedContent);
                activeTrackedContent.transform.SetParent(image.transform, false);
                activeTrackedContent.transform.localPosition = new Vector3(0f, 0.025f, 0f);
                ApplyContentPresentation(targetName);
                activeTrackedContent.SetActive(true);
            }

            if (recognized)
                return;

            recognized = true;
            recognizedTargetName = targetName;
            Debug.Log("LANDMARK_IMAGE_RECOGNIZED " + recognizedTargetName, this);
            TargetRecognized?.Invoke();
        }

        private bool IsExpected(ARTrackedImage image) => ContentFor(image.referenceImage.name) != null;

        private GameObject ContentFor(string targetName)
        {
            if (trackedContents != null)
                foreach (TrackedContentBinding binding in trackedContents)
                    if (binding != null && binding.content != null &&
                        string.Equals(binding.targetName, targetName, StringComparison.Ordinal))
                        return binding.content;

            return string.Equals(targetName, expectedTargetName, StringComparison.Ordinal)
                ? trackedContent
                : null;
        }

        private void HideAllTrackedContent(GameObject except = null)
        {
            if (trackedContent != null && trackedContent != except)
                trackedContent.SetActive(false);
            if (trackedContents == null)
                return;
            foreach (TrackedContentBinding binding in trackedContents)
                if (binding != null && binding.content != null && binding.content != except)
                    binding.content.SetActive(false);
        }

        private void ApplyContentPresentation(string targetName)
        {
            bool isLandmark81 = string.Equals(targetName, "landmark-81", StringComparison.Ordinal);
            activeTrackedContent.transform.localRotation = isLandmark81
                ? Quaternion.Euler(0f, -15f, 0f)
                : Quaternion.identity;

            if (landmark81Light == null)
            {
                var lightObject = new GameObject("Landmark 81 Model Light");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.localRotation = Quaternion.Euler(48f, -32f, 0f);
                landmark81Light = lightObject.AddComponent<Light>();
                landmark81Light.type = LightType.Directional;
                landmark81Light.color = Color.white;
                landmark81Light.intensity = 1f;
                landmark81Light.shadows = LightShadows.None;
            }
            landmark81Light.enabled = isLandmark81;
        }

        private void SetTrackingLost(string targetName)
        {
            if (recognized && !string.Equals(recognizedTargetName, targetName, StringComparison.Ordinal))
                return;
            currentlyTracking = false;
            status = recognized
                ? "Target recognized; point back at it to restore tracking."
                : "Searching for a supported Landmark target...";
            if (activeTrackedContent != null)
                activeTrackedContent.SetActive(false);
            if (landmark81Light != null)
                landmark81Light.enabled = false;
        }

        /// <summary>Validates the post-recognition presentation in Editor; it does not test image recognition.</summary>
        public void SimulateRecognitionForEditor()
        {
            SimulateRecognitionForEditor(expectedTargetName);
        }

        public void SimulateLandmark81RecognitionForEditor()
        {
            SimulateRecognitionForEditor("landmark-81");
        }

        private void SimulateRecognitionForEditor(string targetName)
        {
            recognized = true;
            currentlyTracking = true;
            recognizedTargetName = targetName;
            activeTrackedContent = ContentFor(targetName);
            status = "SIMULATED: recognition UI works. Test the real scan on Android.";
            if (activeTrackedContent != null && showTrackedContent)
            {
                HideAllTrackedContent(activeTrackedContent);
                activeTrackedContent.transform.SetParent(transform, false);
                activeTrackedContent.transform.localPosition = new Vector3(0f, 0f, 0.6f);
                ApplyContentPresentation(targetName);
                activeTrackedContent.SetActive(true);
            }
            Debug.Log("LANDMARK_IMAGE_RECOGNIZED_SIMULATED " + recognizedTargetName, this);
            TargetRecognized?.Invoke();
        }

        /// <summary>Validates the post-tap presentation in Editor/tests, where simulating a real
        /// screen tap on the tracked content isn't practical.</summary>
        public void SimulateContentTapForEditor() => ContentTapped?.Invoke();

        private void OnGUI()
        {
            if (!showLegacyDebugGui)
                return;
            float scale = Mathf.Max(1f, Screen.width / 720f);
            float margin = 24f * scale;
            float panelHeight = 190f * scale;
            Rect panel = new Rect(margin, margin, Screen.width - margin * 2f, panelHeight);

            Color oldColor = GUI.color;
            GUI.color = new Color(0.08f, 0.12f, 0.09f, 0.88f);
            GUI.Box(panel, GUIContent.none);
            GUI.color = oldColor;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(25f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = currentlyTracking ? new Color(0.45f, 1f, 0.56f) : Color.white }
            };
            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18f * scale),
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            GUI.Label(new Rect(panel.x + margin, panel.y + 18f * scale, panel.width - margin * 2f, 40f * scale),
                recognized ? "LANDMARK FOUND" : "SCAN LANDMARK", titleStyle);
            GUI.Label(new Rect(panel.x + margin, panel.y + 62f * scale, panel.width - margin * 2f, 68f * scale),
                status, bodyStyle);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!recognized && GUI.Button(
                    new Rect(panel.x + margin, panel.y + 132f * scale, 250f * scale, 42f * scale),
                    "Simulate UI only"))
                SimulateRecognitionForEditor();
#endif
        }
    }
}
