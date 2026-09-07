using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CorgiAR.EditorTools
{
    /// <summary>
    /// Single idempotent entry point for the CorgiAR app: builds the smooth
    /// per-pet animation controllers, pet thumbnails, the companion prefab
    /// (wrapper + locomotion + interaction + feeding + pet swapping), the uGUI
    /// HUD, and wires it all into SampleScene's reused AR bootstrap.
    /// Menu: <c>Tools/Corgi/Configure AR Companion</c>.
    /// </summary>
    public static partial class DogARSetupGenerator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CompanionPrefabPath = "Assets/CorgiAR/Prefabs/CorgiARCompanion.prefab";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string PikminName = "Pik0100_00_Lod0";
        private const string BootstrapName = "Pikmin Mobile AR";
        private const string PreviewGroundName = "Pikmin Preview Ground";
        private const string MeadowMaterialPath = "Assets/ShibaFeeding/Generated/Playground.mat";
        private const string CompanionName = "Corgi Companion";
        private const string VisualChildName = "Pet Visual";
        private const float VisualScale = 1.5f;

        [MenuItem("Tools/Corgi/Configure AR Companion")]
        public static void ConfigureMenu()
        {
            try
            {
                ConfigureMobileAR(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Corgi AR Companion", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/Corgi/Build Android AR APK")]
        public static void BuildMenu()
        {
            BuildAndroidARBatch();
            EditorUtility.RevealInFinder(Path.GetFullPath("Builds/CorgiAR.apk"));
        }

        public static void ConfigureMobileAR(bool showDialog)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            BuildAnimationAssets();
            BuildPetThumbnails();
            AssetDatabase.Refresh();

            GameObject prefab = CreateCompanionPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            PopulatePetBindingsOnPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompanionPrefabPath);

            GameObject pikmin = Find(scene, PikminName);
            if (pikmin != null && pikmin.activeSelf)
                pikmin.SetActive(false);

            GameObject desktopCameraGo = Find(scene, "Main Camera");
            GameObject previewGround = Find(scene, PreviewGroundName);
            StylePreviewMeadow(scene, previewGround);
            GameObject bootstrap = Find(scene, BootstrapName)
                ?? throw new InvalidOperationException(BootstrapName + " bootstrap not found. Run Pikmin AR setup first.");

            Transform arSession = bootstrap.transform.Find("AR Session");
            Transform xrOrigin = bootstrap.transform.Find("XR Origin");
            if (arSession == null || xrOrigin == null)
                throw new InvalidOperationException("Bootstrap is missing AR Session / XR Origin.");

            var raycastManager = xrOrigin.GetComponent<ARRaycastManager>();
            var planeManager = xrOrigin.GetComponent<ARPlaneManager>();
            Camera arCamera = xrOrigin.GetComponentInChildren<Camera>(true);
            Camera desktopCamera = desktopCameraGo != null ? desktopCameraGo.GetComponent<Camera>() : null;

            // Rebuild the companion instance from scratch so removed components
            // never linger as "missing script" entries.
            GameObject stale = Find(scene, CompanionName);
            if (stale != null)
                UnityEngine.Object.DestroyImmediate(stale);
            var companionGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            companionGo.name = CompanionName;

            Vector3 previewPos = Vector3.zero;
            var previewRenderer = previewGround != null ? previewGround.GetComponent<Renderer>() : null;
            if (previewRenderer != null)
            {
                Bounds gb = previewRenderer.bounds;
                previewPos = new Vector3(gb.center.x, gb.max.y, gb.center.z);
            }
            companionGo.transform.SetPositionAndRotation(previewPos, Quaternion.identity);

            if (desktopCamera != null)
            {
                Vector3 camPos = previewPos + new Vector3(0f, 2.45f, 3.45f);
                desktopCamera.transform.SetPositionAndRotation(camPos,
                    Quaternion.LookRotation((previewPos + Vector3.up * 0.42f) - camPos, Vector3.up));
                desktopCamera.nearClipPlane = 0.03f;
                desktopCamera.fieldOfView = 50f;
                if (desktopCameraGo.TryGetComponent(out UniversalAdditionalCameraData urp))
                    urp.renderPostProcessing = false;
            }

            var companion = companionGo.GetComponent<DogCompanionController>();
            var interaction = companionGo.GetComponent<DogInteractionController>();
            var feeding = companionGo.GetComponent<DogFeedingController>();
            var mood = companionGo.GetComponent<PetMoodController>();
            var headLook = companionGo.GetComponent<PetHeadLook>();
            var toyFetch = companionGo.GetComponent<ToyFetchController>();
            var binder = companionGo.GetComponent<PetBinder>();
            var aligner = companionGo.GetComponent<DogGroundAligner>();

            if (desktopCamera != null)
            {
                BoundedMeadowCamera meadowCamera = GetOrAdd<BoundedMeadowCamera>(desktopCamera.gameObject);
                Set(meadowCamera,
                    ("target", companionGo.transform),
                    ("yaw", 180f), ("pitch", 36f), ("distance", 4.1f),
                    ("targetHeight", 0.42f), ("fieldOfView", 50f),
                    ("zoomLimits", new Vector2(1f, 5.2f)),
                    ("wheelZoomSensitivity", 0.34f),
                    ("pinchZoomSensitivity", 0.006f), ("zoomSharpness", 15f),
                    ("worldHalfExtents", new Vector2(4.4f, 3.6f)),
                    ("maxPanFromPet", 1.65f));
            }

            RemoveComponentByName(bootstrap, "PikminARPlacementController");
            RemoveComponentByName(bootstrap, "PikminARModeController");

            var placement = GetOrAdd<DogARPlacementController>(bootstrap);
            var modeController = GetOrAdd<DogARModeController>(bootstrap);

            Set(placement,
                ("raycastManager", raycastManager), ("planeManager", planeManager), ("arCamera", arCamera),
                ("dogRoot", companionGo), ("companion", companion), ("interaction", interaction),
                ("headLook", headLook), ("toyFetch", toyFetch));

            Set(modeController,
                ("arSessionObject", arSession.gameObject), ("xrOriginObject", xrOrigin.gameObject),
                ("arCamera", arCamera), ("placementController", placement),
                ("desktopCamera", desktopCamera), ("previewGround", previewGround),
                ("dogRoot", companionGo), ("companion", companion), ("interaction", interaction),
                ("groundAligner", aligner), ("forceARInEditor", false));

            arSession.gameObject.SetActive(false);
            xrOrigin.gameObject.SetActive(false);

            BuildHud(scene, placement, companion, interaction, feeding, mood, toyFetch, binder,
                desktopCamera != null ? desktopCamera : arCamera);

            int companions = scene.GetRootGameObjects()
                .Sum(r => r.GetComponentsInChildren<DogCompanionController>(true).Length);
            int canvases = scene.GetRootGameObjects()
                .Sum(r => r.GetComponentsInChildren<CorgiArHud>(true).Length);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save SampleScene.");
            AssetDatabase.SaveAssets();

            Debug.Log($"CORGI AR CONFIGURED: {companions} companion, {canvases} HUD, " +
                      $"{PetCatalog.Entries.Length} pets, feeding + random roaming.");

            if (showDialog)
                EditorUtility.DisplayDialog("Corgi AR Companion",
                    "Configured. Play SampleScene or build to Android.", "OK");
        }

        private static void StylePreviewMeadow(Scene scene, GameObject previewGround)
        {
            if (previewGround == null)
                return;

            Material meadow = AssetDatabase.LoadAssetAtPath<Material>(MeadowMaterialPath);
            Renderer renderer = previewGround.GetComponent<Renderer>();
            if (meadow != null && renderer != null)
                renderer.sharedMaterial = meadow;

            Vector3 scale = previewGround.transform.localScale;
            previewGround.transform.localScale = new Vector3(24f, scale.y, 24f);

            // The Shiba reference scene has two decorative green spheres. They
            // are intentionally omitted from SampleScene's cleaner meadow.
            GameObject leftBush = Find(scene, "Soft Bush Left");
            GameObject rightBush = Find(scene, "Soft Bush Right");
            if (leftBush != null) UnityEngine.Object.DestroyImmediate(leftBush);
            if (rightBush != null) UnityEngine.Object.DestroyImmediate(rightBush);
        }

        public static GameObject CreateCompanionPrefab()
        {
            PetEntry corgi = PetCatalog.Resolve("corgi");
            var corgiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(corgi.SourcePrefabPath);
            if (corgiPrefab == null)
                throw new InvalidOperationException("corgi.prefab not found at " + corgi.SourcePrefabPath);

            EnsureFolder("Assets/CorgiAR/Prefabs");

            var root = new GameObject(CompanionName);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(corgiPrefab);
                visual.name = VisualChildName;
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one;

                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

                var capsule = root.AddComponent<CapsuleCollider>();
                var aligner = root.AddComponent<DogGroundAligner>();
                var animatorAdapter = root.AddComponent<DogAnimatorAdapter>();
                var controller = root.AddComponent<DogCompanionController>();
                var interaction = root.AddComponent<DogInteractionController>();
                var feeding = root.AddComponent<DogFeedingController>();
                var mood = root.AddComponent<PetMoodController>();
                var headLook = root.AddComponent<PetHeadLook>();
                var toyFetch = root.AddComponent<ToyFetchController>();
                var binder = root.AddComponent<PetBinder>();
                var growth = root.AddComponent<PetGrowthController>();
                root.AddComponent<PetGrowthVfx>();

                Animator visualAnimator = visual.GetComponentInChildren<Animator>(true);
                var corgiOverride = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(corgi.OverrideControllerPath);
                if (visualAnimator != null && corgiOverride != null)
                {
                    visualAnimator.runtimeAnimatorController = corgiOverride;
                    visualAnimator.applyRootMotion = false;
                }

                InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                Transform mouth = FindDeep(visual.transform, "DEF-jaw_master")
                                  ?? FindDeep(visual.transform, "DEF-spine.006");

                Set(aligner, ("visual", visual.transform), ("groundClearance", 0.004f));
                Set(animatorAdapter, ("animator", visualAnimator));
                Set(controller,
                    ("inputActions", actions), ("animatorAdapter", animatorAdapter),
                    ("movementHalfExtents", new Vector2(4.4f, 3.6f)));
                Set(interaction, ("companion", controller), ("dogRoot", root.transform));
                Set(feeding, ("companion", controller), ("animatorAdapter", animatorAdapter));
                if (mouth != null)
                    Set(feeding, ("mouthBone", mouth));
                Set(mood, ("companion", controller), ("feeding", feeding));
                Set(headLook, ("companion", controller));
                Set(toyFetch, ("companion", controller), ("feeding", feeding));
                if (mouth != null)
                    Set(toyFetch, ("carryBone", mouth));
                Material petMat = AssignUrpMaterial(visual);
                Set(binder, ("animatorAdapter", animatorAdapter), ("groundAligner", aligner),
                    ("feeding", feeding), ("dogKitMaterial", petMat),
                    ("headLook", headLook), ("toyFetch", toyFetch));
                Set(growth, ("feeding", feeding), ("binder", binder),
                    ("groundAligner", aligner), ("chickensForYoung", 5),
                    ("additionalChickensForAdult", 10), ("babyScale", 0.8f),
                    ("youngScale", 1f), ("adultScale", 1.22f),
                    ("consumedChickenCount", 0));
                // Pet bindings (esp. the just-built override controllers) are set
                // in a second pass after the prefab is saved + reimported, so the
                // AnimatorOverrideController references resolve to persistent GUIDs.

                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                DogGroundAligner.Align(root.transform, visual.transform, capsule, 0.004f);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CompanionPrefabPath);
                Debug.Log("COMPANION PREFAB SAVED: " + CompanionPrefabPath, saved);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void PopulatePetBindingsOnPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CompanionPrefabPath);
            try
            {
                SetPetBindings(root.GetComponent<PetBinder>());
                PrefabUtility.SaveAsPrefabAsset(root, CompanionPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static void SetPetBindings(PetBinder binder)
        {
            var list = new PetBinder.Binding[PetCatalog.Entries.Length];
            for (int i = 0; i < PetCatalog.Entries.Length; i++)
            {
                PetEntry pet = PetCatalog.Entries[i];
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(pet.OverrideControllerPath);
                if (ctrl == null)
                    Debug.LogWarning("SetPetBindings: could not load override controller for " + pet.Id);
                list[i] = new PetBinder.Binding
                {
                    Id = pet.Id,
                    DisplayName = pet.DisplayName,
                    Family = pet.Family,
                    Scale = pet.Scale > 0f ? pet.Scale : 1f,
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pet.SourcePrefabPath),
                    Controller = ctrl,
                    Thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(pet.ThumbnailPath),
                };
            }
            binder.EditorSetBindings(list);
            EditorUtility.SetDirty(binder);
        }

        public static void BuildAndroidARBatch()
        {
            ConfigureMobileAR(false);
            ConfigureAndroidPlayerSettings();

            string dir = Path.GetFullPath("Builds");
            Directory.CreateDirectory(dir);
            string outputPath = Path.Combine(dir, "CorgiAR.apk");
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                scenes = new[] { ScenePath };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"CorgiAR Android build failed: {report.summary.result} ({report.summary.totalErrors} errors).");

            long size = new FileInfo(outputPath).Length;
            Debug.Log($"CORGI AR ANDROID BUILD SUCCEEDED: {outputPath} ({size / (1024f * 1024f):F1} MB)");
        }

        private static void ConfigureAndroidPlayerSettings()
        {
            PlayerSettings.productName = "Corgi AR Companion";
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Android, "com.tungd.corgiarcompanion");
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            if ((int)PlayerSettings.Android.minSdkVersion < 25)
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.iOS.cameraUsageDescription =
                "Camera access is used to place and interact with the pet in AR.";
        }

        private const string UrpMaterialPath = "Assets/CorgiAR/Materials/Corgi_URP.mat";
        private const string DogKitTextures = "Assets/Bublisher/3D Stylized Animated Dogs Kit/Textures/";

        /// <summary>
        /// The Dog Kit ships a built-in "Standard" material that renders magenta
        /// under URP. Build a URP/Simple Lit copy (originals untouched) and assign
        /// it to every renderer on the visual — the full URP/Lit PBR path produces
        /// flashing pixels on the target Intel graphics device.
        /// </summary>
        private static Material AssignUrpMaterial(GameObject visual)
        {
            Shader simpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (simpleLit == null)
                throw new InvalidOperationException("URP/Simple Lit shader not found.");

            EnsureFolder("Assets/CorgiAR/Materials");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(UrpMaterialPath);
            if (mat == null)
            {
                mat = new Material(simpleLit);
                AssetDatabase.CreateAsset(mat, UrpMaterialPath);
            }
            mat.shader = simpleLit;

            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(
                DogKitTextures + "3D Stylized Animated Dogs Kit - BaseColor.png");
            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseMap);
            }
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
            mat.DisableKeyword("_NORMALMAP");
            mat.DisableKeyword("_SPECGLOSSMAP");
            if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
            EditorUtility.SetDirty(mat);

            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                r.sharedMaterials = mats;
            }
            return mat;
        }

        // ---- helpers ----

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void RemoveComponentByName(GameObject go, string typeName)
        {
            foreach (Component c in go.GetComponents<Component>())
                if (c != null && c.GetType().Name == typeName)
                    UnityEngine.Object.DestroyImmediate(c, true);
        }

        private static void Set(Component target, params (string name, object value)[] fields)
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

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t.gameObject;
            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private static void EnsureFolder(string folder)
        {
            string current = "Assets";
            foreach (string part in folder.Split('/').Skip(1))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
