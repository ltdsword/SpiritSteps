using System;
using UnityEngine;
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
        [SerializeField] private ARTrackedImageManager imageManager;
        [SerializeField] private GameObject trackedContent;
        [SerializeField] private string expectedTargetName = "notre-dame-basilica";
        [SerializeField] private bool showTrackedContent;
        [SerializeField] private bool showLegacyDebugGui;

        private bool recognized;
        private bool currentlyTracking;
        private string status = "Point the camera at the printed Notre-Dame target.";

        public bool Recognized => recognized;
        public string ExpectedTargetName => expectedTargetName;
        public event Action TargetRecognized;

        private void Awake()
        {
            if (imageManager == null)
                imageManager = GetComponent<ARTrackedImageManager>();
            if (trackedContent != null)
                trackedContent.SetActive(false);
        }

        private void OnEnable()
        {
            if (imageManager != null)
                imageManager.trackablesChanged.AddListener(OnTrackablesChanged);
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
                    SetTrackingLost();
            }
        }

        private void ApplyTrackingState(ARTrackedImage image)
        {
            if (image == null || !IsExpected(image))
                return;

            if (image.trackingState != TrackingState.Tracking)
            {
                SetTrackingLost();
                return;
            }

            currentlyTracking = true;
            status = recognized
                ? "Notre-Dame target is being tracked."
                : "Notre-Dame target recognized!";

            if (trackedContent != null && showTrackedContent)
            {
                trackedContent.transform.SetParent(image.transform, false);
                trackedContent.transform.localPosition = new Vector3(0f, 0.025f, 0f);
                trackedContent.transform.localRotation = Quaternion.identity;
                trackedContent.SetActive(true);
            }

            if (recognized)
                return;

            recognized = true;
            Debug.Log("LANDMARK_IMAGE_RECOGNIZED " + expectedTargetName, this);
            TargetRecognized?.Invoke();
        }

        private bool IsExpected(ARTrackedImage image) =>
            string.Equals(image.referenceImage.name, expectedTargetName, StringComparison.Ordinal);

        private void SetTrackingLost()
        {
            currentlyTracking = false;
            status = recognized
                ? "Target recognized; point back at it to restore tracking."
                : "Searching for the Notre-Dame target...";
            if (trackedContent != null)
                trackedContent.SetActive(false);
        }

        /// <summary>Validates the post-recognition presentation in Editor; it does not test image recognition.</summary>
        public void SimulateRecognitionForEditor()
        {
            recognized = true;
            currentlyTracking = true;
            status = "SIMULATED: recognition UI works. Test the real scan on Android.";
            if (trackedContent != null && showTrackedContent)
            {
                trackedContent.transform.SetParent(transform, false);
                trackedContent.transform.localPosition = new Vector3(0f, 0f, 0.6f);
                trackedContent.SetActive(true);
            }
            Debug.Log("LANDMARK_IMAGE_RECOGNIZED_SIMULATED " + expectedTargetName, this);
            TargetRecognized?.Invoke();
        }

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
