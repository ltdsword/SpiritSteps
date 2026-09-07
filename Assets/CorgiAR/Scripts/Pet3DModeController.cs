using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Boots the non-AR meadow playground. This deliberately lives separately
    /// from DogARModeController so the app's Pet 3D scene cannot alter PetAr.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class Pet3DModeController : MonoBehaviour
    {
        [Header("AR objects to keep disabled")]
        [SerializeField] private GameObject arSessionObject;
        [SerializeField] private GameObject xrOriginObject;
        [SerializeField] private Camera arCamera;
        [SerializeField] private DogARPlacementController placementController;

        [Header("Meadow preview")]
        [SerializeField] private Camera desktopCamera;
        [SerializeField] private GameObject previewGround;
        [SerializeField] private MeadowPlayArea meadowPlayArea;

        [Header("Dog")]
        [SerializeField] private GameObject dogRoot;
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private DogInteractionController interaction;
        [SerializeField] private DogGroundAligner groundAligner;

        public bool IsARMode => false;

        private void Awake()
        {
            SetActive(arSessionObject, false);
            SetActive(xrOriginObject, false);
            SetActive(previewGround, true);
            SetActive(dogRoot, true);
            if (arCamera != null)
                arCamera.gameObject.SetActive(false);
            if (desktopCamera != null)
                desktopCamera.gameObject.SetActive(true);
            if (placementController != null)
                placementController.enabled = false;
            groundAligner?.Align();
        }

        private void Start()
        {
            if (companion == null || dogRoot == null)
                return;

            Camera camera = desktopCamera != null ? desktopCamera : Camera.main;
            EnsureMeadowPlayArea();
            if (meadowPlayArea != null)
            {
                meadowPlayArea.Configure(dogRoot.transform.position, dogRoot.transform.position.y, true);
                companion.ConfigurePlayArea(meadowPlayArea);
            }

            companion.ConfigureAR(camera, null);
            companion.SetMovementBoundsEnabled(true);
            companion.SetPlacement(new Pose(dogRoot.transform.position, dogRoot.transform.rotation));
            if (camera != null)
                camera.GetComponent<BoundedMeadowCamera>()?.Configure(dogRoot.transform, meadowPlayArea);
            interaction?.ConfigureAR(camera);
            placementController?.ConfigureForPreview(camera);
        }

        private void EnsureMeadowPlayArea()
        {
            if (meadowPlayArea != null)
                return;
            GameObject host = previewGround != null ? previewGround : gameObject;
            meadowPlayArea = host.GetComponent<MeadowPlayArea>();
            if (meadowPlayArea == null)
                meadowPlayArea = host.AddComponent<MeadowPlayArea>();
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
