using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CorgiAR
{
    /// <summary>
    /// Handheld-AR world layer: the placement reticle, tap-to-place / reposition,
    /// and forwarding non-UI touches (and the mouse in the editor) to
    /// <see cref="DogInteractionController"/>. All on-screen controls now live on
    /// the uGUI canvas (<see cref="CorgiArHud"/>); this component draws no GUI.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class DogARPlacementController : MonoBehaviour
    {
        private const int ReticleSegments = 48;
        private static readonly List<ARRaycastHit> Hits = new();

        [Header("AR")]
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private Camera arCamera;

        [Header("Pet")]
        [SerializeField] private GameObject dogRoot;
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private DogInteractionController interaction;
        [SerializeField] private PetHeadLook headLook;
        [SerializeField] private ToyFetchController toyFetch;
        [SerializeField] private ShibaFeeding.FoodDragThrowUI foodDrag;
        [SerializeField] private ToyDragThrowUI ballDrag;

        [Header("Reticle")]
        [SerializeField, Min(0.01f)] private float reticleRadius = 0.075f;
        [SerializeField, Min(0.001f)] private float reticleWidth = 0.006f;
        [SerializeField] private Color reticleColor = new(1f, 0.78f, 0.35f, 0.95f);

        [Header("Modes")]
        [SerializeField] private CompanionControlMode startingMode = CompanionControlMode.Automatic;

        private GameObject reticle;
        private Material reticleMaterial;
        private bool hasReticleHit;
        private Pose reticlePose;
        private bool isPlaced;
        private CompanionControlMode controlMode;

        public bool IsPlaced => isPlaced;
        public CompanionControlMode Mode => controlMode;
        /// <summary>The camera currently driving AR interactions - the real AR camera in AR
        /// mode, or the desktop-preview camera once <see cref="ConfigureForPreview"/> runs.
        /// Single source of truth for anything (e.g. <see cref="CorgiAR.UI.CorgiArGlassHud"/>'s
        /// drag-throw controllers) that needs "whichever camera is live right now".</summary>
        public Camera ArCamera => arCamera;
        /// <summary>True while the screen-centre raycast is currently hitting a detected plane
        /// (the orange ring reticle is visible) - i.e. it's safe to tap to place the pet.</summary>
        public bool HasDetectedPlane => hasReticleHit;

        /// <summary>Raised when the pet is first placed (argument is always true).</summary>
        public event Action<bool> PlacedChanged;

        private void Awake() => controlMode = startingMode;

        public void BeginPlacement()
        {
            if (planeManager != null)
                planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            CreateReticle();
            if (companion != null)
            {
                companion.ConfigureAR(arCamera, raycastManager);
                companion.SetMode(controlMode);
            }
            interaction?.ConfigureAR(arCamera);
            FanCamera(arCamera);
        }

        private void FanCamera(Camera cam)
        {
            if (headLook != null) headLook.SetCamera(cam);
            if (toyFetch != null) toyFetch.SetCamera(cam);
            if (foodDrag != null) foodDrag.SetCamera(cam);
            if (ballDrag != null) ballDrag.SetCamera(cam);
        }

        public void SetMode(CompanionControlMode mode)
        {
            controlMode = mode;
            companion?.SetMode(mode);
        }

        /// <summary>
        /// Editor / desktop preview: no AR planes, the pet is already standing on
        /// the preview ground. Uses the desktop camera for pet raycasts so mouse
        /// petting works, and reports "placed" so the HUD shows its controls.
        /// </summary>
        public void ConfigureForPreview(Camera previewCamera)
        {
            arCamera = previewCamera;
            raycastManager = null;
            planeManager = null;
            if (reticle != null)
                reticle.SetActive(false);
            bool wasPlaced = isPlaced;
            isPlaced = true;
            interaction?.ConfigureAR(previewCamera);
            FanCamera(previewCamera);
            if (!wasPlaced)
                PlacedChanged?.Invoke(true);
        }

        private void OnEnable()
        {
            if (raycastManager != null && planeManager != null)
                BeginPlacement();
        }

        private void Update()
        {
            if (arCamera == null)
                return;

            if (raycastManager != null)
                UpdateReticle();
            HandleTouches();
        }

        private void UpdateReticle()
        {
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            hasReticleHit = TryGetPlanePose(screenCenter, out reticlePose);
            if (reticle == null)
                CreateReticle();
            if (reticle == null)
                return;

            reticle.SetActive(hasReticleHit && !isPlaced);
            if (hasReticleHit)
            {
                Vector3 normal = reticlePose.rotation * Vector3.up;
                reticle.transform.SetPositionAndRotation(
                    reticlePose.position + normal * 0.003f, reticlePose.rotation);

                // No tap required: the moment a plane is found under the screen centre, place
                // the pet there automatically.
                if (!isPlaced)
                    PlaceDog(reticlePose);
            }
        }

        private static bool IsOverUi(int pointerId) =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);

        private void HandleTouches()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                HandleMouseFallback();
                return;
            }

            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed && !touch.press.wasReleasedThisFrame)
                    continue;

                int id = touch.touchId.ReadValue();
                if (IsOverUi(id))
                    continue;

                Vector2 pos = touch.position.ReadValue();
                bool pressed = touch.press.wasPressedThisFrame;
                bool released = touch.press.wasReleasedThisFrame;

                if (isPlaced && interaction != null)
                {
                    if (RaycastDog(pos) || (!pressed && !released))
                    {
                        interaction.ProcessTouch(pos, pressed, released, Time.unscaledTimeAsDouble);
                        if (RaycastDog(pos))
                            continue;
                    }
                }

                if (!isPlaced && released && TryGetPlanePose(pos, out Pose pose) && !RaycastDog(pos))
                    PlaceDog(pose);
            }
        }

        private void HandleMouseFallback()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || IsOverUi(-1))
                return;

            Vector2 pos = mouse.position.ReadValue();
            bool pressed = mouse.leftButton.wasPressedThisFrame;
            bool released = mouse.leftButton.wasReleasedThisFrame;
            bool held = mouse.leftButton.isPressed;

            if (isPlaced && interaction != null && (pressed || released || held))
                interaction.ProcessTouch(pos, pressed, released, Time.unscaledTimeAsDouble);

            if (!isPlaced && released && TryGetPlanePose(pos, out Pose pose) && !RaycastDog(pos))
                PlaceDog(pose);
        }

        private void PlaceDog(Pose pose)
        {
            if (dogRoot == null || companion == null)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            dogRoot.SetActive(true);
            dogRoot.transform.SetPositionAndRotation(pose.position,
                Quaternion.LookRotation(forward.normalized, Vector3.up));
            companion.ConfigureAR(arCamera, raycastManager);
            companion.SetPlacement(pose);
            interaction?.ConfigureAR(arCamera);

            bool wasPlaced = isPlaced;
            isPlaced = true;
            if (reticle != null)
                reticle.SetActive(false);
            if (!wasPlaced)
                PlacedChanged?.Invoke(true);
        }

        private bool RaycastDog(Vector2 screenPosition)
        {
            if (arCamera == null || dogRoot == null)
                return false;
            Ray ray = arCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, arCamera.farClipPlane,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                return false;
            Transform t = hit.collider.transform;
            return t == dogRoot.transform || t.IsChildOf(dogRoot.transform);
        }

        private bool TryGetPlanePose(Vector2 screenPosition, out Pose pose)
        {
            pose = default;
            if (raycastManager == null)
                return false;
            Hits.Clear();
            if (raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon) &&
                Hits.Count > 0)
            {
                pose = Hits[0].pose;
                return true;
            }
            pose = default;
            return false;
        }

        private void CreateReticle()
        {
            if (reticle != null)
                return;
            reticle = new GameObject("Pet Placement Reticle");
            reticle.transform.SetParent(transform, false);

            var line = reticle.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = ReticleSegments;
            line.startWidth = reticleWidth;
            line.endWidth = reticleWidth;
            line.startColor = reticleColor;
            line.endColor = reticleColor;
            line.numCornerVertices = 2;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                reticleMaterial = new Material(shader) { color = reticleColor };
                line.material = reticleMaterial;
            }

            for (int i = 0; i < ReticleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ReticleSegments;
                line.SetPosition(i,
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reticleRadius);
            }
            reticle.SetActive(false);
        }

        private void OnDestroy()
        {
            if (reticleMaterial != null)
                Destroy(reticleMaterial);
        }
    }
}
