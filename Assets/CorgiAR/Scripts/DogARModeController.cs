using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Chooses AR vs desktop-preview at startup. On a handheld device it enables
    /// the AR Session / XR Origin and hides the Corgi until placement; in the
    /// Editor it keeps the desktop camera + preview ground so the mouse can drive
    /// the touch interaction path.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class DogARModeController : MonoBehaviour
    {
        [Header("AR objects")]
        [SerializeField] private GameObject arSessionObject;
        [SerializeField] private GameObject xrOriginObject;
        [SerializeField] private Camera arCamera;
        [SerializeField] private DogARPlacementController placementController;

        [Header("Desktop preview")]
        [SerializeField] private Camera desktopCamera;
        [SerializeField] private GameObject previewGround;

        [Header("Dog")]
        [SerializeField] private GameObject dogRoot;
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private DogInteractionController interaction;
        [SerializeField] private DogGroundAligner groundAligner;

        [Header("Testing")]
        [Tooltip("Run AR mode in the Editor with XR Simulation. Leave off for desktop WASD testing.")]
        [SerializeField] private bool forceARInEditor;

        public bool IsARMode { get; private set; }

        private void Awake()
        {
            IsARMode = forceARInEditor ||
                       (!Application.isEditor &&
                        (Application.platform == RuntimePlatform.Android ||
                         Application.platform == RuntimePlatform.IPhonePlayer));

            if (groundAligner != null)
                groundAligner.Align();

            if (!IsARMode)
            {
                SetActive(arSessionObject, false);
                SetActive(xrOriginObject, false);
                return;
            }

            DisableDesktopCamera();
            SetActive(previewGround, false);
            SetActive(arSessionObject, true);
            SetActive(xrOriginObject, true);

            if (dogRoot != null)
                dogRoot.SetActive(false);

            if (placementController != null)
            {
                placementController.enabled = true;
                placementController.BeginPlacement();
            }
        }

        private void Start()
        {
            // Desktop preview: "place" the dog where it sits so WASD + mouse
            // petting work without an AR plane. Runs in Start so the companion's
            // own Awake (Rigidbody setup) has already executed.
            if (IsARMode || companion == null || dogRoot == null)
                return;

            Camera cam = desktopCamera != null ? desktopCamera : Camera.main;
            companion.ConfigureAR(cam, null);
            companion.SetPlacement(new Pose(dogRoot.transform.position, dogRoot.transform.rotation));
            interaction?.ConfigureAR(cam);
            placementController?.ConfigureForPreview(cam);
        }

        private void DisableDesktopCamera()
        {
            if (desktopCamera == null)
                return;
            AudioListener listener = desktopCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = false;
            desktopCamera.gameObject.SetActive(false);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
