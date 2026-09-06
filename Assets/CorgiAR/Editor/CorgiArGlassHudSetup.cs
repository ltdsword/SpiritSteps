using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using CorgiAR.UI;

namespace CorgiAR.EditorTools
{
    /// <summary>
    /// Ensures the UI Toolkit "Corgi AR HUD" GameObject (<see cref="CorgiArGlassHud"/>) exists in
    /// PetAr.unity and has its handful of non-Resources-loadable references baked in (the actual
    /// visuals are built at runtime by <see cref="CorgiArGlassHud"/> itself, mirroring
    /// <c>WalkUiController</c> - this script only wires scene references, unlike the old
    /// <c>PetArHudGenerator</c> which hand-built an entire uGUI hierarchy). Menu:
    /// <c>Tools/Corgi/Configure PetAr HUD</c>. Idempotent.
    /// </summary>
    public static class CorgiArGlassHudSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/PetAr.unity";
        private const string HudName = "Corgi AR HUD";
        private const string FoodPrefabPath = "Assets/ShibaFeeding/Generated/ChickenLegFood.prefab";
        private const string FoodIconPath = "Assets/CorgiAR/ExternalAssets/chicken-drumstick.png";
        private const string BallPrefabPath = "Assets/CorgiAR/Generated/PlayBall.prefab";
        private const string BallModelPath = "Assets/CorgiAR/Models/Ball/Ball.fbx";
        private const string BallIconPath = "Assets/CorgiAR/Resources/UI/Icons/ball.png";
        private const float BallDiameter = 0.12f;
        private const string WhistleModelPath = "Assets/CorgiAR/ExternalAssets/Whistle00.fbx";
        private const string WhistleIconPath = "Assets/CorgiAR/Resources/UI/Icons/whistle3d.png";

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
            GameObject companionGo = Find(scene, "Corgi Companion")
                ?? throw new InvalidOperationException("Corgi Companion not found in PetAr.unity.");

            Camera arCamera = xrOrigin.GetComponentInChildren<Camera>(true);
            Camera desktopCamera = desktopCameraGo != null ? desktopCameraGo.GetComponent<Camera>() : null;
            Camera hudCamera = desktopCamera != null ? desktopCamera : arCamera;

            var placement = xrOrigin.GetComponent<DogARPlacementController>()
                ?? throw new InvalidOperationException("DogARPlacementController not found on XR Origin.");

            var companion = companionGo.GetComponent<DogCompanionController>();
            var feeding = companionGo.GetComponent<DogFeedingController>();
            var mood = companionGo.GetComponent<PetMoodController>();
            var toyFetch = companionGo.GetComponent<ToyFetchController>();
            var binder = companionGo.GetComponent<PetBinder>();

            EnsureFolder("Assets/CorgiAR/Generated");
            EnsureFolder("Assets/CorgiAR/Materials");
            EnsureFolder("Assets/CorgiAR/Resources");
            EnsureFolder("Assets/CorgiAR/Resources/UI");
            EnsureFolder("Assets/CorgiAR/Resources/UI/Icons");

            GameObject ballPrefab = EnsurePlayBallPrefab();
            RenderModelIcon(ballPrefab, BallIconPath);

            GameObject whistleModel = AssetDatabase.LoadAssetAtPath<GameObject>(WhistleModelPath);
            if (whistleModel != null)
                RenderModelIcon(whistleModel, WhistleIconPath, OverrideWhistleMaterial);

            GameObject old = Find(scene, HudName);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            var hudGo = new GameObject(HudName, typeof(UIDocument), typeof(ArPhotoCapture), typeof(CorgiArGlassHud));
            var hud = hudGo.GetComponent<CorgiArGlassHud>();
            var photo = hudGo.GetComponent<ArPhotoCapture>();

            GameObject foodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FoodPrefabPath);
            Sprite foodIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FoodIconPath);
            Sprite ballIcon = AssetDatabase.LoadAssetAtPath<Sprite>(BallIconPath);
            Sprite whistleIcon = AssetDatabase.LoadAssetAtPath<Sprite>(WhistleIconPath);

            Set(hud,
                ("placement", placement), ("companion", companion), ("feeding", feeding),
                ("mood", mood), ("toyFetch", toyFetch), ("binder", binder),
                ("photo", photo), ("hudCamera", hudCamera),
                ("foodPrefab", foodPrefab), ("ballPrefab", ballPrefab),
                ("foodIconSprite", foodIcon), ("ballIconSprite", ballIcon),
                ("whistleIconSprite", whistleIcon));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save PetAr.unity.");
            AssetDatabase.SaveAssets();

            Debug.Log("CORGI AR GLASS HUD CONFIGURED.");
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

        /// <summary>The imported Whistle00.fbx renders with speckled magenta/green noise (bad
        /// normals or overlapping geometry in the source mesh, not a shader/pipeline mismatch -
        /// its material already targets URP/Lit) - swap in a plain plastic-look material instead
        /// of trying to fix the source mesh.</summary>
        /// <summary>Whistle00.fbx's mesh is skinned (SkinnedMeshRenderer, no blend shapes, no
        /// visible rig purpose) - instantiating it outside an Animator context left its bone
        /// matrices unresolved, corrupting vertex positions into the speckled noise seen in the
        /// render. Baking each SkinnedMeshRenderer into a static mesh at its current pose and
        /// swapping in a plain MeshRenderer sidesteps the skinning entirely.</summary>
        private static void BakeSkinnedMeshes(GameObject inst)
        {
            foreach (SkinnedMeshRenderer skinned in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh();
                skinned.BakeMesh(baked, true);
                GameObject go = skinned.gameObject;
                Material[] mats = skinned.sharedMaterials;
                UnityEngine.Object.DestroyImmediate(skinned);
                go.AddComponent<MeshFilter>().sharedMesh = baked;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            }
        }

        private static void OverrideWhistleMaterial(GameObject inst)
        {
            BakeSkinnedMeshes(inst);
            EnsureFolder("Assets/CorgiAR/Materials");
            const string path = "Assets/CorgiAR/Materials/Whistle.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            Color color = new Color(1f, 0.78f, 0.2f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            if (mat.HasProperty("_SpecColor")) mat.SetColor("_SpecColor", Color.black);
            if (mat.HasProperty("_SpecularHighlights"))
            {
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            EditorUtility.SetDirty(mat);

            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Renders a model (a prefab or an imported FBX's root GameObject) to a
        /// transparent PNG icon under a Resources folder, so the runtime HUD can
        /// <c>Resources.Load</c> it with zero wiring.</summary>
        private static void RenderModelIcon(GameObject model, string outputPath, Action<GameObject> beforeRender = null)
        {
            const int size = 256;
            const int iconLayer = 31;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var camGo = new GameObject("~IconCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 5f;
            cam.cullingMask = 1 << iconLayer;
            cam.targetTexture = rt;

            var lightGo = new GameObject("~IconLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.cullingMask = 1 << iconLayer;
            lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

            GameObject inst = null;
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 150f, 0f));
                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = iconLayer;
                foreach (TrailRenderer trail in inst.GetComponentsInChildren<TrailRenderer>(true))
                    trail.enabled = false;
                beforeRender?.Invoke(inst);

                Bounds b = RendererWorldBounds(inst);
                float radius = Mathf.Max(0.1f, b.extents.magnitude);
                float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.35f;
                cam.transform.position = b.center + new Vector3(0.4f, 0.35f, 1f).normalized * dist;
                cam.transform.LookAt(b.center);

                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                System.IO.File.WriteAllBytes(outputPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                if (inst != null) UnityEngine.Object.DestroyImmediate(inst);
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(lightGo);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(outputPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }

        private static Bounds RendererWorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true)
                .Where(r => r is not TrailRenderer).ToArray();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
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
                    case Color c: p.colorValue = c; break;
                    default: throw new InvalidOperationException("Unsupported field type for " + name);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
