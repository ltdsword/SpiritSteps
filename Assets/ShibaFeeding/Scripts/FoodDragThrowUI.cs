using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShibaFeeding
{
    /// <summary>Drag the food chip out of the HUD and release to throw it to the dog.</summary>
    public sealed class FoodDragThrowUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("A component implementing IFeedableDog (ShibaFeedingController or CorgiAR DogFeedingController).")]
        [SerializeField] private MonoBehaviour feedTarget;
        [SerializeField] private GameObject foodPrefab;
        [SerializeField] private Graphic buttonGraphic;
        [SerializeField] private FoodSourceVisualFeedback visualFeedback;

        [Header("World food model")]
        [Tooltip("Largest world-space dimension used to normalize imported food models.")]
        [SerializeField, Min(0.05f)] private float worldFoodSize = 0.28f;

        private IFeedableDog shiba;

        [Header("Interaction feel")]
        [SerializeField, Min(0.5f)] private float heldDepth = 2.4f;
        [SerializeField] private Color normalColor = new Color(0.05f, 0.09f, 0.12f, 0.38f);
        [SerializeField] private Color pressedColor = new Color(0.08f, 0.16f, 0.15f, 0.52f);

        private ThrownFood heldFood;
        private bool dragging;

        // Real, unassisted throw velocity: measured from the actual drag motion
        // (world position delta / time), nothing added. Holding still and
        // releasing yields ~zero velocity, so the treat just drops under gravity.
        private Vector3 lastHeldPosition;
        private float lastHeldTime;
        private Vector3 releaseVelocity;

        private void Update()
        {
            // Direct Input System fallback keeps the interaction working even when a
            // project's EventSystem actions were replaced or are temporarily disabled.
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                Vector2 position = touch.position.ReadValue();
                if (touch.press.wasPressedThisFrame && IsInsideButton(position))
                    BeginHold(position);
                else if (dragging && touch.press.isPressed)
                    MoveHeldFood(position);
                else if (dragging && touch.press.wasReleasedThisFrame)
                    ReleaseFood(position);
                return;
            }

            if (Mouse.current == null)
                return;

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame && IsInsideButton(mousePosition))
                BeginHold(mousePosition);
            else if (dragging && Mouse.current.leftButton.isPressed)
                MoveHeldFood(mousePosition);
            else if (dragging && Mouse.current.leftButton.wasReleasedThisFrame)
                ReleaseFood(mousePosition);
        }

        private void Awake()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (shiba == null && feedTarget != null)
                shiba = feedTarget as IFeedableDog;
            if (shiba == null)
            {
                // Legacy scenes serialized a concrete reference that this field
                // rename dropped; fall back to the only feedable dog in the scene.
                shiba = FindFirstObjectByType<ShibaFeedingController>();
                feedTarget = shiba as MonoBehaviour;
            }
            if (buttonGraphic == null)
                buttonGraphic = GetComponent<Graphic>();
            if (visualFeedback == null)
                visualFeedback = GetComponent<FoodSourceVisualFeedback>();
            normalColor = new Color(0.05f, 0.09f, 0.12f, 0.38f);
            pressedColor = new Color(0.08f, 0.16f, 0.15f, 0.52f);
            BuildRuntimeButtonVisual();
            SetButtonColor(normalColor);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            BeginHold(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MoveHeldFood(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ReleaseFood(eventData.position);
        }

        public void Configure(Camera camera, IFeedableDog receiver, GameObject prefab, Graphic graphic)
        {
            worldCamera = camera;
            shiba = receiver;
            feedTarget = receiver as MonoBehaviour;
            foodPrefab = prefab;
            buttonGraphic = graphic;
        }

        /// <summary>Re-point at the live AR/preview camera (the one baked in at HUD
        /// generation time gets disabled when the app switches into real AR).</summary>
        public void SetCamera(Camera camera) => worldCamera = camera;

        private void BeginHold(Vector2 screenPosition)
        {
            if (heldFood != null || shiba == null || shiba.IsEating)
                return;

            dragging = true;
            Vector3 spawnPosition = ScreenToHeldWorld(screenPosition);
            GameObject foodObject = foodPrefab != null
                ? Instantiate(foodPrefab, spawnPosition, Quaternion.identity)
                : CreateRuntimeFood(spawnPosition);
            foodObject.name = "Low Poly Treat (Held)";
            heldFood = PrepareWorldFood(foodObject);
            if (heldFood == null)
            {
                Destroy(foodObject);
                dragging = false;
                if (visualFeedback != null)
                    visualFeedback.SetHeld(false);
                return;
            }
            heldFood.SetHeld(true);
            shiba.BeginFollowingHeldFood(heldFood.transform);
            if (visualFeedback != null)
                visualFeedback.SetHeld(true);
            SetButtonColor(pressedColor);

            lastHeldPosition = heldFood.transform.position;
            lastHeldTime = Time.time;
            releaseVelocity = Vector3.zero;
        }

        private ThrownFood PrepareWorldFood(GameObject foodObject)
        {
            // Imported FBX assets are deliberately kept free of gameplay components.
            // Add the same lightweight wrapper used by the generated food at runtime,
            // so swapping the visual does not alter throw/eat behaviour or physics.
            Camera[] importedCameras = foodObject.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < importedCameras.Length; i++)
                importedCameras[i].enabled = false;
            Light[] importedLights = foodObject.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < importedLights.Length; i++)
                importedLights[i].enabled = false;

            Bounds renderBounds;
            if (TryGetRenderBounds(foodObject, out renderBounds))
            {
                float largestDimension = Mathf.Max(renderBounds.size.x, renderBounds.size.y, renderBounds.size.z);
                if (largestDimension > 0.0001f)
                {
                    float scaleMultiplier = worldFoodSize / largestDimension;
                    foodObject.transform.localScale *= scaleMultiplier;
                    Physics.SyncTransforms();
                    TryGetRenderBounds(foodObject, out renderBounds);
                }

                if (foodObject.GetComponentInChildren<Collider>() == null)
                {
                    SphereCollider sphere = foodObject.AddComponent<SphereCollider>();
                    sphere.center = foodObject.transform.InverseTransformPoint(renderBounds.center);
                    Vector3 lossy = foodObject.transform.lossyScale;
                    float maxScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z), 0.0001f);
                    sphere.radius = Mathf.Max(renderBounds.extents.x, renderBounds.extents.y, renderBounds.extents.z) / maxScale;
                }
            }

            ThrownFood thrownFood = foodObject.GetComponent<ThrownFood>();
            if (thrownFood == null)
                thrownFood = foodObject.AddComponent<ThrownFood>();

            if (foodObject.GetComponent<TrailRenderer>() == null)
            {
                Renderer modelRenderer = null;
                Renderer[] modelRenderers = foodObject.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < modelRenderers.Length; i++)
                {
                    if (!(modelRenderers[i] is TrailRenderer) && modelRenderers[i].sharedMaterial != null)
                    {
                        modelRenderer = modelRenderers[i];
                        break;
                    }
                }
                TrailRenderer trail = foodObject.AddComponent<TrailRenderer>();
                trail.time = 0.18f;
                trail.startWidth = 0.045f;
                trail.endWidth = 0f;
                trail.startColor = new Color(1f, 0.82f, 0.28f, 0.55f);
                trail.endColor = new Color(1f, 0.35f, 0.05f, 0f);
                if (modelRenderer != null)
                    trail.sharedMaterial = modelRenderer.sharedMaterial;
            }

            return thrownFood;
        }

        private static bool TryGetRenderBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            bounds = new Bounds(root.transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is TrailRenderer || !renderers[i].enabled ||
                    !renderers[i].gameObject.activeInHierarchy)
                    continue;
                if (!found)
                {
                    bounds = renderers[i].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
            return found;
        }

        private void MoveHeldFood(Vector2 screenPosition)
        {
            if (!dragging || heldFood == null)
                return;
            Vector3 newPosition = ScreenToHeldWorld(screenPosition);
            float dt = Mathf.Max(Time.time - lastHeldTime, 0.0001f);
            releaseVelocity = (newPosition - lastHeldPosition) / dt;
            heldFood.transform.position = newPosition;
            lastHeldPosition = newPosition;
            lastHeldTime = Time.time;
        }

        private void ReleaseFood(Vector2 screenPosition)
        {
            if (!dragging)
                return;

            dragging = false;
            if (visualFeedback != null)
                visualFeedback.SetHeld(false);
            SetButtonColor(normalColor);
            if (heldFood == null)
                return;

            ThrownFood releasedFood = heldFood;
            heldFood = null;
            shiba.EndFollowingHeldFood();
            releasedFood.SetHeld(false);

            float groundY = shiba.GetFoodLandingPoint().y;
            releasedFood.Launch(releaseVelocity, shiba, groundY);
            if (visualFeedback != null)
                visualFeedback.MarkInteractionUsed();
        }

        private bool IsInsideButton(Vector2 screenPosition)
        {
            RectTransform rect = transform as RectTransform;
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null);
        }

        private static GameObject CreateRuntimeFood(Vector3 position)
        {
            GameObject food = new GameObject("Low Poly Treat");
            food.transform.position = position;
            food.transform.localScale = Vector3.one * 0.85f;

            Mesh mesh = BuildRoundedFoodMesh();

            MeshFilter filter = food.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = food.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = new Color(1f, 0.34f, 0.06f) };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(1f, 0.34f, 0.06f));
            renderer.sharedMaterial = material;

            Material boneMaterial = new Material(shader) { color = new Color(1f, 0.88f, 0.62f) };
            if (boneMaterial.HasProperty("_BaseColor"))
                boneMaterial.SetColor("_BaseColor", new Color(1f, 0.88f, 0.62f));
            CreateBonePart(food.transform, "Bone Shaft", PrimitiveType.Cylinder,
                new Vector3(0.18f, 0f, 0f), new Vector3(0.035f, 0.13f, 0.035f), Quaternion.Euler(0f, 0f, 90f), boneMaterial);
            CreateBonePart(food.transform, "Bone Knob Top", PrimitiveType.Sphere,
                new Vector3(0.31f, 0.045f, 0f), Vector3.one * 0.075f, Quaternion.identity, boneMaterial);
            CreateBonePart(food.transform, "Bone Knob Bottom", PrimitiveType.Sphere,
                new Vector3(0.31f, -0.045f, 0f), Vector3.one * 0.075f, Quaternion.identity, boneMaterial);

            SphereCollider collider = food.AddComponent<SphereCollider>();
            collider.radius = 0.16f;
            food.AddComponent<ThrownFood>();
            TrailRenderer trail = food.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.075f;
            trail.endWidth = 0f;
            trail.sharedMaterial = material;
            trail.startColor = new Color(1f, 0.82f, 0.28f, 0.7f);
            trail.endColor = new Color(1f, 0.35f, 0.05f, 0f);
            return food;
        }

        private static Mesh BuildRoundedFoodMesh()
        {
            const int segments = 10;
            const int rings = 6;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();

            vertices.Add(new Vector3(-0.045f, 0.14f, 0f));
            for (int ring = 1; ring < rings; ring++)
            {
                float latitude = Mathf.PI * ring / rings;
                float y = Mathf.Cos(latitude) * 0.14f;
                float radius = Mathf.Sin(latitude);
                for (int segment = 0; segment < segments; segment++)
                {
                    float longitude = Mathf.PI * 2f * segment / segments;
                    vertices.Add(new Vector3(
                        -0.045f + Mathf.Cos(longitude) * radius * 0.19f,
                        y,
                        Mathf.Sin(longitude) * radius * 0.145f));
                }
            }
            int bottom = vertices.Count;
            vertices.Add(new Vector3(-0.045f, -0.14f, 0f));

            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles.Add(0); triangles.Add(1 + next); triangles.Add(1 + segment);
            }
            for (int ring = 0; ring < rings - 2; ring++)
            {
                int current = 1 + ring * segments;
                int nextRing = current + segments;
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    triangles.Add(current + segment); triangles.Add(nextRing + next); triangles.Add(nextRing + segment);
                    triangles.Add(current + segment); triangles.Add(current + next); triangles.Add(nextRing + next);
                }
            }
            int lastRing = 1 + (rings - 2) * segments;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles.Add(lastRing + segment); triangles.Add(lastRing + next); triangles.Add(bottom);
            }

            Mesh mesh = new Mesh { name = "Rounded Low Poly Drumstick" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateBonePart(Transform parent, string name, PrimitiveType type,
            Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation;
            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null) Destroy(partCollider);
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private Vector3 ScreenToHeldWorld(Vector2 screenPosition)
        {
            if (worldCamera == null)
                return Vector3.zero;
            return worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, heldDepth));
        }

        private void SetButtonColor(Color color)
        {
            if (buttonGraphic != null)
                buttonGraphic.color = color;
        }

        private void BuildRuntimeButtonVisual()
        {
            RectTransform buttonRect = transform as RectTransform;
            if (buttonRect == null || transform.Find("Runtime Drumstick Icon") != null)
                return;

            // Respect the layout authored by each scene. SampleScene aligns this
            // source with its joystick, while the feeding demo uses a higher
            // mobile-safe anchor of its own.
            Transform oldIcon = transform.Find("Food Icon");
            if (oldIcon != null) oldIcon.gameObject.SetActive(false);

            Transform hintTransform = transform.parent != null ? transform.parent.Find("Hint") : null;
            if (hintTransform != null)
            {
                hintTransform.gameObject.SetActive(false);
                RectTransform hintRect = hintTransform as RectTransform;
                hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, Screen.width > Screen.height ? 0.42f : 0.3f);
                hintRect.pivot = new Vector2(0.5f, 0.5f);
                hintRect.anchoredPosition = Vector2.zero;
                hintRect.sizeDelta = new Vector2(900f, 52f);
                Text hint = hintTransform.GetComponent<Text>();
                if (hint != null)
                {
                    hint.text = "GIỮ THỨC ĂN  •  KÉO LÊN  •  THẢ ĐỂ NÉM";
                    hint.fontSize = 24;
                }
            }

            Text label = transform.Find("Label") != null ? transform.Find("Label").GetComponent<Text>() : null;
            if (label != null)
            {
                label.gameObject.SetActive(false);
                label.text = "KÉO ĐỂ NÉM";
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0.02f);
                labelRect.anchorMax = new Vector2(1f, 0.24f);
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            }

            // The generated frameless hierarchy already owns its icon, quantity and tutorial.
            // Keeping the hit target separate prevents a duplicate runtime icon from appearing.
            if (transform.Find("Food Item Visual") != null)
                return;

            Transform generatedIcon = transform.Find("Drumstick Icon");
            if (generatedIcon != null)
            {
                Graphic generatedGraphic = generatedIcon.GetComponent<Graphic>();
                if (generatedGraphic != null)
                    generatedGraphic.raycastTarget = false;
                // Preserve the authored size of sprite-based icons. Only the
                // legacy procedural graphic needs to stretch across the source.
                if (generatedIcon.GetComponent<DrumstickIconGraphic>() != null)
                {
                    RectTransform generatedRect = generatedIcon as RectTransform;
                    generatedRect.anchorMin = Vector2.zero;
                    generatedRect.anchorMax = Vector2.one;
                    generatedRect.offsetMin = generatedRect.offsetMax = Vector2.zero;
                }
                return;
            }

            GameObject iconRoot = new GameObject("Runtime Drumstick Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(DrumstickIconGraphic));
            iconRoot.transform.SetParent(transform, false);
            RectTransform rootRect = iconRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            rootRect.SetAsFirstSibling();
            DrumstickIconGraphic icon = iconRoot.GetComponent<DrumstickIconGraphic>();
            icon.raycastTarget = false;
        }
    }
}
