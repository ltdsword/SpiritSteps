using ShibaFeeding;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CorgiAR.EditorTools
{
    /// <summary>Builds the uGUI heads-up display (status, mode toggle, joystick, food button, pet menu).</summary>
    public static partial class DogARSetupGenerator
    {
        private const string HudName = "Corgi AR HUD";
        private const string FoodPrefabPath = "Assets/ShibaFeeding/Generated/ChickenLegFood.prefab";
        private const string FoodIconPath = "Assets/CorgiAR/ExternalAssets/chicken-drumstick.png";
        private const string BallPrefabPath = "Assets/CorgiAR/Generated/PlayBall.prefab";
        private const string BallModelPath = "Assets/CorgiAR/Models/Ball/Ball.fbx";
        private const float BallDiameter = 0.12f;

        private static readonly Color Panel = new(0.05f, 0.09f, 0.12f, 0.55f);
        private static readonly Color Accent = new(1f, 0.78f, 0.32f, 0.95f);
        private static readonly Color Dim = new(1f, 1f, 1f, 0.3f);

        private static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static Sprite RoundSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        private static Sprite KnobSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        private static void EnsureEventSystem(UnityEngine.SceneManagement.Scene scene)
        {
            if (Find(scene, "EventSystem") != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static GameObject BuildHud(UnityEngine.SceneManagement.Scene scene,
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
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // --- status pill (top) ---
            GameObject status = Pill(canvasGo.transform, "Status", new Vector2(0.5f, 1f),
                new Vector2(900f, 90f), new Vector2(0f, -70f));
            Text statusLabel = Label(status.transform, "Label", "Chạm màn hình để đặt thú cưng", 30);

            // --- mode toggle (top, under status) ---
            GameObject toggle = Row(canvasGo.transform, "Mode Toggle", new Vector2(0.5f, 1f),
                new Vector2(560f, 78f), new Vector2(0f, -180f));
            Button manualBtn = PillButton(toggle.transform, "Manual", "ĐIỀU KHIỂN");
            Button autoBtn = PillButton(toggle.transform, "Auto", "TỰ ĐỘNG");

            // --- command column (top-left) ---
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

            // --- joystick (bottom-left) ---
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

            // --- food button (bottom-center) ---
            GameObject foodGo = new("Food Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            foodGo.transform.SetParent(canvasGo.transform, false);
            var foodRect = (RectTransform)foodGo.transform;
            foodRect.anchorMin = foodRect.anchorMax = new Vector2(0.5f, 0f);
            foodRect.pivot = new Vector2(0.5f, 0.5f);
            // Share the joystick's bottom baseline so the two controls read as
            // one balanced mobile HUD row.
            foodRect.anchoredPosition = new Vector2(0f, 210f);
            foodRect.sizeDelta = new Vector2(200f, 200f);
            var foodImg = foodGo.GetComponent<Image>();
            foodImg.sprite = KnobSprite;
            foodImg.color = new Color(0.05f, 0.09f, 0.12f, 0.38f);
            Sprite foodIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FoodIconPath);
            GameObject drum = new("Food Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
            ConfigureFoodSelector(foodGo, foodDrag, foodPrefab);
            foodRect.anchoredPosition = new Vector2(0f, 200f);

            // --- ball button (bottom-right) ---
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

            // Placement re-points these to the live AR/preview camera once it's
            // known (the hudCamera above is only a baked-in desktop fallback,
            // wrong for real handheld AR — see DogARPlacementController.FanCamera).
            Set(placement, ("foodDrag", foodDrag), ("ballDrag", ballDrag));

            // --- pet menu: full-screen modal with a bottom card + 3-column scrolling grid ---
            GameObject panelGo = new("Pet Panel", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(CanvasGroup));
            panelGo.transform.SetParent(canvasGo.transform, false);
            StretchFull((RectTransform)panelGo.transform);
            var scrim = panelGo.GetComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.62f);      // blocks the HUD behind it
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
            // GameObject stays active; the PetMenuPanel's CanvasGroup starts hidden.
            panelGo.GetComponent<CanvasGroup>().alpha = 0f;
            panelGo.GetComponent<CanvasGroup>().blocksRaycasts = false;
            panelGo.GetComponent<CanvasGroup>().interactable = false;

            // --- full-screen white flash for the photo capture (last sibling) ---
            GameObject flashGo = new("Photo Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            flashGo.transform.SetParent(canvasGo.transform, false);
            StretchFull((RectTransform)flashGo.transform);
            var flashImg = flashGo.GetComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;

            var photo = canvasGo.AddComponent<ArPhotoCapture>();

            // --- HUD controller ---
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

            Debug.Log("CORGI AR HUD built.");
            return canvasGo;
        }

        // ---- small uGUI builders ----

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

        /// <summary>
        /// Builds a URP-safe material, carrying over the source material's base color.
        /// Persisted as a real .mat asset at a stable path — a transient in-memory
        /// Material referenced by a saved prefab goes null after the next domain
        /// reload (any script recompile), which is what made the ball render as the
        /// pink/magenta "missing material" placeholder.
        /// </summary>
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
    }
}
