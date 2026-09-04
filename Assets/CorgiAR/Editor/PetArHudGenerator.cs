using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using ShibaFeeding;

namespace CorgiAR.EditorTools
{
    /// <summary>
    /// Ports the real CorgiAR uGUI HUD (as authored by first3Dproject's
    /// <c>DogARSetupGenerator.Hud.cs</c>) into SpiritSteps' shared "PetAr" scene, targeting
    /// the AR Foundation bootstrap (XR Origin / Desktop Preview Camera / Preview Ground) and
    /// the "Corgi Companion" instance already present there, instead of first3Dproject's
    /// "Pikmin Mobile AR" bootstrap. Menu: <c>Tools/Corgi/Configure PetAr HUD</c>.
    /// Idempotent - destroys and rebuilds "Corgi AR HUD" each run.
    /// </summary>
    public static class PetArHudGenerator
    {
        private const string ScenePath = "Assets/_Project/Scenes/PetAr.unity";
        private const string HudName = "Corgi AR HUD";
        private const string FoodPrefabPath = "Assets/ShibaFeeding/Generated/ChickenLegFood.prefab";
        private const string FoodIconPath = "Assets/CorgiAR/ExternalAssets/chicken-drumstick.png";
        private const string BallPrefabPath = "Assets/CorgiAR/Generated/PlayBall.prefab";
        private const string BallModelPath = "Assets/CorgiAR/Models/Ball/Ball.fbx";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const float BallDiameter = 0.12f;

        private static readonly Color Panel = new(0.05f, 0.09f, 0.12f, 0.55f);
        private static readonly Color Accent = new(1f, 0.78f, 0.32f, 0.95f);
        private static readonly Color Dim = new(1f, 1f, 1f, 0.3f);

        private static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static Sprite RoundSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        private static Sprite KnobSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        [MenuItem("Tools/Corgi/Configure PetAr HUD")]
        public static void Configure()
        {
            try
            {
                ConfigurePetArHud();
                EditorUtility.DisplayDialog("PetAr HUD", "Configured. Open PetAr.unity to inspect.", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("PetAr HUD", exception.Message, "OK");
            }
        }

        public static void ConfigurePetArHud()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject xrOrigin = Find(scene, "XR Origin")
                ?? throw new InvalidOperationException("XR Origin not found - run the AR bootstrap setup first.");
            GameObject desktopCameraGo = Find(scene, "Desktop Preview Camera");
            GameObject previewGround = Find(scene, "Preview Ground");
            GameObject companionGo = Find(scene, "Corgi Companion")
                ?? throw new InvalidOperationException("Corgi Companion not found in PetAr.unity.");

            var raycastManager = xrOrigin.GetComponent<ARRaycastManager>();
            var planeManager = xrOrigin.GetComponent<ARPlaneManager>();
            Camera arCamera = xrOrigin.GetComponentInChildren<Camera>(true);
            Camera desktopCamera = desktopCameraGo != null ? desktopCameraGo.GetComponent<Camera>() : null;
            Camera hudCamera = desktopCamera != null ? desktopCamera : arCamera;

            var placement = xrOrigin.GetComponent<DogARPlacementController>()
                ?? throw new InvalidOperationException("DogARPlacementController not found on XR Origin.");
            var modeController = xrOrigin.GetComponent<DogARModeController>()
                ?? throw new InvalidOperationException("DogARModeController not found on XR Origin.");

            var companion = companionGo.GetComponent<DogCompanionController>();
            var interaction = companionGo.GetComponent<DogInteractionController>();
            var feeding = companionGo.GetComponent<DogFeedingController>();
            var mood = companionGo.GetComponent<PetMoodController>();
            var toyFetch = companionGo.GetComponent<ToyFetchController>();
            var binder = companionGo.GetComponent<PetBinder>();

            // Manual/keyboard fallback input (the on-screen joystick bypasses this via
            // VirtualJoystick.SetManualInput directly). SpiritSteps' own default
            // InputSystem_Actions.inputactions has an identical Player/Move action.
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputActionsPath);
            if (actions != null)
                Set(companion, ("inputActions", actions));

            EnsureFolder("Assets/CorgiAR/Generated");
            EnsureFolder("Assets/CorgiAR/Materials");

            GameObject hud = BuildHud(scene, placement, companion, interaction, feeding, mood, toyFetch, binder, hudCamera);

            Set(placement, ("foodDrag", hud.transform.Find("Food Button").GetComponent<FoodDragThrowUI>()),
                ("ballDrag", hud.transform.Find("Ball Button").GetComponent<ToyDragThrowUI>()));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save PetAr.unity.");
            AssetDatabase.SaveAssets();

            Debug.Log("PETAR HUD CONFIGURED.");
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (Find(scene, "EventSystem") != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static GameObject BuildHud(Scene scene,
            DogARPlacementController placement, DogCompanionController companion,
            DogInteractionController interaction, DogFeedingController feeding,
            PetMoodController mood, ToyFetchController toyFetch,
            PetBinder binder, Camera hudCamera)
        {
            EnsureEventSystem(scene);

            GameObject old = Find(scene, HudName);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            var canvasGo = new GameObject(HudName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject status = Pill(canvasGo.transform, "Status", new Vector2(0.5f, 1f),
                new Vector2(900f, 90f), new Vector2(0f, -70f));
            Text statusLabel = Label(status.transform, "Label", "Chạm màn hình để đặt thú cưng", 30);

            GameObject toggle = Row(canvasGo.transform, "Mode Toggle", new Vector2(0.5f, 1f),
                new Vector2(560f, 78f), new Vector2(0f, -180f));
            Button manualBtn = PillButton(toggle.transform, "Manual", "ĐIỀU KHIỂN");
            Button autoBtn = PillButton(toggle.transform, "Auto", "TỰ ĐỘNG");

            GameObject column = new("Command Column", typeof(RectTransform));
            column.transform.SetParent(canvasGo.transform, false);
            var colRect = (RectTransform)column.transform;
            colRect.anchorMin = colRect.anchorMax = new Vector2(0f, 1f);
            colRect.pivot = new Vector2(0f, 1f);
            colRect.anchoredPosition = new Vector2(24f, -260f);
            colRect.sizeDelta = new Vector2(250f, 430f);
            var vlg = column.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.childAlignment = TextAnchor.UpperLeft;

            Button petMenuBtn = ColumnButton(column.transform, "Pet Menu Button", "ĐỔI THÚ", Accent);
            Button sitBtn = ColumnButton(column.transform, "Sit Button", "NGỒI", Dim);
            Button comeBtn = ColumnButton(column.transform, "Come Button", "LẠI ĐÂY", Dim);
            Button hungryBtn = ColumnButton(column.transform, "Hungry Button", "ĐÓI", Dim);
            Button photoBtn = ColumnButton(column.transform, "Photo Button", "CHỤP ẢNH", Dim);

            GameObject joyGo = new("Joystick", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            joyGo.transform.SetParent(canvasGo.transform, false);
            var joyRect = (RectTransform)joyGo.transform;
            joyRect.anchorMin = joyRect.anchorMax = new Vector2(0f, 0f);
            joyRect.pivot = new Vector2(0.5f, 0.5f);
            joyRect.anchoredPosition = new Vector2(190f, 210f);
            joyRect.sizeDelta = new Vector2(260f, 260f);
            var joyImg = joyGo.GetComponent<Image>();
            joyImg.sprite = KnobSprite;
            joyImg.color = Panel;
            GameObject knob = new("Knob", typeof(RectTransform), typeof(Image));
            knob.transform.SetParent(joyGo.transform, false);
            var knobRect = (RectTransform)knob.transform;
            knobRect.sizeDelta = new Vector2(120f, 120f);
            knob.GetComponent<Image>().sprite = KnobSprite;
            knob.GetComponent<Image>().color = Accent;
            var joystick = joyGo.GetComponent<VirtualJoystick>();
            Set(joystick, ("baseRect", joyRect), ("knob", knobRect), ("companion", companion), ("radius", 110f));

            GameObject foodGo = new("Food Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            foodGo.transform.SetParent(canvasGo.transform, false);
            var foodRect = (RectTransform)foodGo.transform;
            foodRect.anchorMin = foodRect.anchorMax = new Vector2(0.5f, 0f);
            foodRect.pivot = new Vector2(0.5f, 0.5f);
            foodRect.sizeDelta = new Vector2(200f, 200f);
            var foodImg = foodGo.GetComponent<Image>();
            foodImg.sprite = KnobSprite;
            foodImg.color = new Color(0.05f, 0.09f, 0.12f, 0.38f);
            Sprite foodIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FoodIconPath);
            GameObject drum = new("Drumstick Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            drum.transform.SetParent(foodGo.transform, false);
            RectTransform drumRect = (RectTransform)drum.transform;
            drumRect.anchorMin = drumRect.anchorMax = new Vector2(0.5f, 0.5f);
            drumRect.sizeDelta = new Vector2(285f, 285f);
            Image drumImage = drum.GetComponent<Image>();
            drumImage.sprite = foodIcon;
            drumImage.preserveAspect = true;
            drumImage.raycastTarget = false;
            Shadow drumShadow = drum.AddComponent<Shadow>();
            drumShadow.effectColor = new Color(0.08f, 0.05f, 0.02f, 0.25f);
            drumShadow.effectDistance = new Vector2(3f, -5f);
            var foodDrag = foodGo.AddComponent<FoodDragThrowUI>();
            GameObject foodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FoodPrefabPath);
            foodDrag.Configure(hudCamera, feeding, foodPrefab, foodImg);
            foodRect.anchoredPosition = new Vector2(0f, 200f);

            GameObject ballGo = new("Ball Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ballGo.transform.SetParent(canvasGo.transform, false);
            var ballRect = (RectTransform)ballGo.transform;
            ballRect.anchorMin = ballRect.anchorMax = new Vector2(0.5f, 0f);
            ballRect.pivot = new Vector2(0.5f, 0.5f);
            ballRect.anchoredPosition = new Vector2(320f, 200f);
            ballRect.sizeDelta = new Vector2(200f, 200f);
            var ballImg = ballGo.GetComponent<Image>();
            ballImg.sprite = KnobSprite;
            ballImg.color = new Color(0.10f, 0.28f, 0.52f, 0.9f);
            Label(ballGo.transform, "Label", "NÉM\nBÓNG", 24);
            var ballDrag = ballGo.AddComponent<ToyDragThrowUI>();
            GameObject ballPrefab = EnsurePlayBallPrefab();
            ballDrag.Configure(hudCamera, toyFetch, ballPrefab, ballImg);

            GameObject panelGo = new("Pet Panel", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(CanvasGroup));
            panelGo.transform.SetParent(canvasGo.transform, false);
            StretchFull((RectTransform)panelGo.transform);
            var scrim = panelGo.GetComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.62f);
            var group = panelGo.GetComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;

            GameObject sheet = new("Sheet", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            sheet.transform.SetParent(panelGo.transform, false);
            var sheetRect = (RectTransform)sheet.transform;
            sheetRect.anchorMin = new Vector2(0.02f, 0.06f);
            sheetRect.anchorMax = new Vector2(0.98f, 0.82f);
            sheetRect.offsetMin = sheetRect.offsetMax = Vector2.zero;
            var sheetImg = sheet.GetComponent<Image>();
            sheetImg.sprite = RoundSprite;
            sheetImg.type = Image.Type.Sliced;
            sheetImg.color = new Color(0.06f, 0.09f, 0.12f, 0.98f);

            Label(sheet.transform, "Title", "CHỌN THÚ CƯNG", 34).rectTransform
                .SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 18f, 66f);

            GameObject closeGo = new("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(sheet.transform, false);
            var closeRect = (RectTransform)closeGo.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(84f, 84f);
            closeRect.anchoredPosition = new Vector2(-12f, -12f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.sprite = RoundSprite;
            closeImg.type = Image.Type.Sliced;
            closeImg.color = new Color(0.8f, 0.3f, 0.3f, 0.95f);
            closeGo.GetComponent<Button>().targetGraphic = closeImg;
            Label(closeGo.transform, "X", "✕", 36);

            GameObject viewport = new("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(sheet.transform, false);
            var vpRect = (RectTransform)viewport.transform;
            vpRect.anchorMin = new Vector2(0f, 0f);
            vpRect.anchorMax = new Vector2(1f, 1f);
            vpRect.offsetMin = new Vector2(24f, 24f);
            vpRect.offsetMax = new Vector2(-24f, -96f);

            GameObject content = new("Content", typeof(RectTransform), typeof(GridLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300f, 320f);
            grid.spacing = new Vector2(18f, 18f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = sheet.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = vpRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            GameObject entryTemplate = new("Entry Template",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            entryTemplate.transform.SetParent(content.transform, false);
            entryTemplate.GetComponent<Image>().sprite = RoundSprite;
            entryTemplate.GetComponent<Image>().type = Image.Type.Sliced;
            entryTemplate.GetComponent<Image>().color = Dim;
            GameObject thumb = new("Thumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            thumb.transform.SetParent(entryTemplate.transform, false);
            var thumbRect = (RectTransform)thumb.transform;
            thumbRect.anchorMin = new Vector2(0.08f, 0.26f);
            thumbRect.anchorMax = new Vector2(0.92f, 0.96f);
            thumbRect.offsetMin = thumbRect.offsetMax = Vector2.zero;
            thumb.GetComponent<Image>().raycastTarget = false;
            thumb.GetComponent<Image>().preserveAspect = true;
            Text nameText = Label(entryTemplate.transform, "Name", "Pet", 26);
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0.02f, 0.02f);
            nameRect.anchorMax = new Vector2(0.98f, 0.22f);
            nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
            entryTemplate.SetActive(false);

            var petPanel = panelGo.AddComponent<PetMenuPanel>();
            Set(petPanel, ("binder", binder), ("row", (RectTransform)content.transform),
                ("entryTemplate", entryTemplate), ("closeButton", closeGo.GetComponent<Button>()));
            panelGo.GetComponent<CanvasGroup>().alpha = 0f;
            panelGo.GetComponent<CanvasGroup>().blocksRaycasts = false;
            panelGo.GetComponent<CanvasGroup>().interactable = false;

            GameObject flashGo = new("Photo Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            flashGo.transform.SetParent(canvasGo.transform, false);
            StretchFull((RectTransform)flashGo.transform);
            var flashImg = flashGo.GetComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;

            var photo = canvasGo.AddComponent<ArPhotoCapture>();

            var hud = canvasGo.AddComponent<CorgiArHud>();
            Set(photo, ("hudCanvas", canvas), ("flash", flashImg), ("hud", hud));
            Set(hud,
                ("placement", placement), ("companion", companion), ("feeding", feeding),
                ("mood", mood), ("photo", photo),
                ("statusLabel", statusLabel),
                ("manualButton", manualBtn), ("autoButton", autoBtn), ("petMenuButton", petMenuBtn),
                ("sitButton", sitBtn), ("comeButton", comeBtn), ("hungryButton", hungryBtn),
                ("photoButton", photoBtn),
                ("joystickObject", joyGo), ("foodButtonObject", foodGo), ("ballButtonObject", ballGo),
                ("petPanel", petPanel));

            Debug.Log("CORGI AR HUD built (PetAr).");
            return canvasGo;
        }

        // ---- small uGUI builders (mirrors first3Dproject's DogARSetupGenerator.Hud.cs) ----

        private static GameObject Pill(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y > 0.9f ? 1f : anchor.y < 0.1f ? 0f : 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = RoundSprite;
            img.type = Image.Type.Sliced;
            img.color = Panel;
            return go;
        }

        private static GameObject Row(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            GameObject go = Pill(parent, name, anchor, size, pos);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            return go;
        }

        private static Button ColumnButton(Transform parent, string name, string text, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = RoundSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            go.GetComponent<Button>().targetGraphic = img;
            go.GetComponent<LayoutElement>().minHeight = 72f;
            Label(go.transform, "Label", text, 26);
            return go.GetComponent<Button>();
        }

        private static GameObject EnsurePlayBallPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
            if (existing != null)
                return existing;

            EnsureFolder("Assets/CorgiAR/Generated");
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                ball.name = "PlayBall";

                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                GameObject ballModel = AssetDatabase.LoadAssetAtPath<GameObject>(BallModelPath);
                MeshFilter modelMeshFilter = ballModel != null ? ballModel.GetComponentInChildren<MeshFilter>() : null;
                MeshRenderer modelMeshRenderer = ballModel != null ? ballModel.GetComponentInChildren<MeshRenderer>() : null;

                Material mat;
                if (modelMeshFilter != null && modelMeshFilter.sharedMesh != null && modelMeshRenderer != null)
                {
                    Mesh mesh = modelMeshFilter.sharedMesh;
                    ball.GetComponent<MeshFilter>().sharedMesh = mesh;

                    Bounds bounds = mesh.bounds;
                    float largestAxis = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    float scale = largestAxis > 0f ? BallDiameter / largestAxis : BallDiameter;
                    ball.transform.localScale = Vector3.one * scale;

                    Material[] sourceMats = modelMeshRenderer.sharedMaterials;
                    var mats = new Material[sourceMats.Length];
                    for (int i = 0; i < sourceMats.Length; i++)
                        mats[i] = BuildBallMaterial(sourceMats[i], lit, i);
                    ball.GetComponent<Renderer>().sharedMaterials = mats;
                    mat = mats.Length > 0 ? mats[0] : BuildBallMaterial(null, lit, 0);

                    var sc = ball.GetComponent<SphereCollider>();
                    sc.isTrigger = true;
                    sc.center = bounds.center;
                    sc.radius = largestAxis * 0.5f;
                }
                else
                {
                    ball.transform.localScale = Vector3.one * BallDiameter;
                    mat = BuildBallMaterial(null, lit, 0);
                    ball.GetComponent<Renderer>().sharedMaterial = mat;

                    var sc = ball.GetComponent<SphereCollider>();
                    sc.isTrigger = true;
                }

                var trail = ball.AddComponent<TrailRenderer>();
                trail.time = 0.2f;
                trail.startWidth = 0.06f;
                trail.endWidth = 0f;
                trail.sharedMaterial = mat;

                ball.AddComponent<ThrownToy>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(ball, BallPrefabPath);
                Debug.Log("PLAY BALL PREFAB SAVED: " + BallPrefabPath, saved);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ball);
            }
        }

        private static Material BuildBallMaterial(Material source, Shader lit, int index)
        {
            EnsureFolder("Assets/CorgiAR/Materials");
            string path = $"Assets/CorgiAR/Materials/Ball_{index}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = lit;
            }

            Color color = new Color(0.92f, 0.26f, 0.2f);
            if (source != null)
            {
                if (source.HasProperty("_BaseColor")) color = source.GetColor("_BaseColor");
                else if (source.HasProperty("_Color")) color = source.color;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Button PillButton(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = RoundSprite;
            img.type = Image.Type.Sliced;
            img.color = Dim;
            go.GetComponent<Button>().targetGraphic = img;
            Label(go.transform, "Label", text, 26);
            return go.GetComponent<Button>();
        }

        private static Text Label(Transform parent, string name, string text, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            StretchFull((RectTransform)go.transform);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = UiFont;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, size / 2);
            label.resizeTextMaxSize = size;
            label.raycastTarget = false;
            return label;
        }

        private static void StretchFull(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static void EnsureFolder(string folder)
        {
            string current = "Assets";
            foreach (string part in folder.Split('/'))
            {
                if (part == "Assets") continue;
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t.gameObject;
            return null;
        }

        private static void Set(UnityEngine.Object target, params (string name, object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach ((string name, object value) in fields)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p == null)
                    throw new MissingFieldException(target.GetType().Name, name);
                switch (value)
                {
                    case null: p.objectReferenceValue = null; break;
                    case bool b: p.boolValue = b; break;
                    case float f: p.floatValue = f; break;
                    case int i: p.intValue = i; break;
                    case string s: p.stringValue = s; break;
                    case UnityEngine.Object o: p.objectReferenceValue = o; break;
                    case Vector2 v2: p.vector2Value = v2; break;
                    case Vector3 v3: p.vector3Value = v3; break;
                    default: throw new InvalidOperationException("Unsupported field type for " + name);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
