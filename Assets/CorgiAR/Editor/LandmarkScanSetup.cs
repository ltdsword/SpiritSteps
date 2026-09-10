using System;
using System.IO;
using System.Linq;
using CorgiAR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CorgiAR.EditorTools
{
    /// <summary>Builds the isolated AR Foundation Landmark image scanner.</summary>
    public static class LandmarkScanSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/LandmarkScan.unity";
        private const string SourceImagePath = "Assets/_Project/Art/UI/ReferenceTemp/Landmarks/notre-dame.png";
        private const string TargetFolder = "Assets/_Project/Art/AR/Targets";
        private const string TargetImagePath = TargetFolder + "/notre-dame-basilica.png";
        private const string Landmark81TargetImagePath = TargetFolder + "/landmark_81.jpg";
        private const string LibraryFolder = "Assets/_Project/Resources/AR";
        private const string LibraryPath = LibraryFolder + "/LandmarkReferenceImageLibrary.asset";
        private const string NotreDameTargetName = "notre-dame-basilica";
        private const string Landmark81TargetName = "landmark-81";
        private const float PrintedWidthMetres = 0.20f;

        private const string NotreDameModelFolder = "Assets/_Project/Resources/AR/notre-dame-cathedral-basilica-of-saigon";
        private const string NotreDameModelPath = NotreDameModelFolder + "/source/Notre_Dame.fbx";
        private const string NotreDameGeneratedFolder = NotreDameModelFolder + "/Generated";
        private const string NotreDamePrefabPath = NotreDameGeneratedFolder + "/NotreDameLandmark.prefab";
        /// <summary>Display height in metres - taller than the 0.20m printed marker so the model
        /// reads as a landmark standing on the card rather than a flat decal.</summary>
        private const float NotreDameDisplayHeight = 0.32f;

        private const string Landmark81ModelPath = "Assets/_Project/Resources/AR/landmark-81/source/Landmark 81.glb";
        private const string Landmark81GeneratedFolder = "Assets/_Project/Resources/AR/landmark-81/Generated";
        private const string Landmark81PrefabPath = Landmark81GeneratedFolder + "/Landmark81Landmark.prefab";
        private const float Landmark81DisplayHeight = 0.32f;

        [MenuItem("Tools/AR Walking/Landmark Scan/Build Landmark Scanner")]
        public static void BuildMenu()
        {
            Build();
            EditorUtility.DisplayDialog("Landmark Scan",
                "Created LandmarkScan.unity with the Notre-Dame Basilica and Landmark 81 targets.\n\n" +
                "Open the scene for Editor UI simulation, or use the Android POC build menu for a real scan.", "OK");
        }

        public static void Build()
        {
            PrepareAssetsForPlayerBuild();

            XRReferenceImageLibrary library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath)
                                              ?? throw new InvalidOperationException("Reference image library is missing: " + LibraryPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LandmarkScan";

            var sessionObject = new GameObject("AR Session");
            sessionObject.AddComponent<ARSession>();
            sessionObject.AddComponent<ARInputManager>();

            var originObject = new GameObject("XR Origin");
            var origin = originObject.AddComponent<XROrigin>();
            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObject.transform, false);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<TrackedPoseDriver>();
            cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;

            origin.Camera = camera;
            origin.CameraFloorOffsetObject = cameraOffset;

            var imageManager = originObject.AddComponent<ARTrackedImageManager>();
            imageManager.referenceLibrary = library;
            imageManager.requestedMaxNumberOfMovingImages = 2;

            GameObject notreDameModel = (GameObject)PrefabUtility.InstantiatePrefab(EnsureNotreDameModelPrefab());
            notreDameModel.name = "Notre-Dame Landmark Model";
            notreDameModel.SetActive(false);
            GameObject landmark81Model = (GameObject)PrefabUtility.InstantiatePrefab(EnsureLandmark81ModelPrefab());
            landmark81Model.name = "Landmark 81 Model";
            landmark81Model.SetActive(false);

            var controller = originObject.AddComponent<LandmarkImageTrackingController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("imageManager").objectReferenceValue = imageManager;
            serializedController.FindProperty("trackedContent").objectReferenceValue = notreDameModel;
            serializedController.FindProperty("expectedTargetName").stringValue = NotreDameTargetName;
            SerializedProperty bindings = serializedController.FindProperty("trackedContents");
            bindings.arraySize = 2;
            bindings.GetArrayElementAtIndex(0).FindPropertyRelative("targetName").stringValue = NotreDameTargetName;
            bindings.GetArrayElementAtIndex(0).FindPropertyRelative("content").objectReferenceValue = notreDameModel;
            bindings.GetArrayElementAtIndex(1).FindPropertyRelative("targetName").stringValue = Landmark81TargetName;
            bindings.GetArrayElementAtIndex(1).FindPropertyRelative("content").objectReferenceValue = landmark81Model;
            serializedController.FindProperty("showTrackedContent").boolValue = true;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save " + ScenePath);

            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("LANDMARK_SCAN_POC_BUILT targets=" + NotreDameTargetName + "," +
                      Landmark81TargetName + " width=0.20m scene=" + ScenePath);
        }

        internal static void PrepareAssetsForPlayerBuild()
        {
            EnsureFolder(TargetFolder);
            EnsureFolder(LibraryFolder);

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TargetImagePath) == null)
            {
                if (!AssetDatabase.CopyAsset(SourceImagePath, TargetImagePath))
                    throw new InvalidOperationException("Could not copy landmark target from " + SourceImagePath);
                AssetDatabase.ImportAsset(TargetImagePath, ImportAssetOptions.ForceSynchronousImport);
            }

            EnsureImageTargetImportSettings(Landmark81TargetImagePath);

            Texture2D targetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetImagePath)
                                      ?? throw new InvalidOperationException("Target image is missing: " + TargetImagePath);
            Texture2D landmark81Target = AssetDatabase.LoadAssetAtPath<Texture2D>(Landmark81TargetImagePath)
                                         ?? throw new InvalidOperationException("Target image is missing: " + Landmark81TargetImagePath);
            BuildReferenceLibrary(targetTexture, landmark81Target);
            EnsureNotreDameModelPrefab();
            EnsureLandmark81ModelPrefab();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/AR Walking/Landmark Scan/Build Android POC APK")]
        public static void BuildAndroidPoc()
        {
            BuildAndroidPoc(false);
        }

        [MenuItem("Tools/AR Walking/Landmark Scan/Build And Run Android POC")]
        public static void BuildAndRunAndroidPoc()
        {
            BuildAndroidPoc(true);
        }

        private static void BuildAndroidPoc(bool runAfterBuild)
        {
            Build();
            string outputDirectory = Path.GetFullPath("Builds");
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, "LandmarkScanPOC.apk");
            var androidTarget = UnityEditor.Build.NamedBuildTarget.Android;
            string originalIdentifier = PlayerSettings.GetApplicationIdentifier(androidTarget);
            string originalProductName = PlayerSettings.productName;
            try
            {
                PlayerSettings.SetApplicationIdentifier(androidTarget, "com.team06.arwalking.landmarkscan");
                PlayerSettings.productName = "Landmark Scan POC";
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.Development |
                              (runAfterBuild ? BuildOptions.AutoRunPlayer : BuildOptions.None)
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Landmark scan APK build failed: " + report.summary.result);
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(androidTarget, originalIdentifier);
                PlayerSettings.productName = originalProductName;
                AssetDatabase.SaveAssets();
            }

            Debug.Log((runAfterBuild
                ? "LANDMARK_SCAN_ANDROID_BUILD_AND_RUN_SUCCEEDED "
                : "LANDMARK_SCAN_ANDROID_BUILD_SUCCEEDED ") + outputPath);

            if (!runAfterBuild)
                EditorUtility.RevealInFinder(outputPath);
        }

        private static XRReferenceImageLibrary BuildReferenceLibrary(Texture2D notreDameTexture, Texture2D landmark81Texture)
        {
            XRReferenceImageLibrary library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            while (library.count > 0)
                library.RemoveAt(library.count - 1);

            AddReferenceImage(library, NotreDameTargetName, notreDameTexture);
            AddReferenceImage(library, Landmark81TargetName, landmark81Texture);
            EditorUtility.SetDirty(library);
            return library;
        }

        private static void AddReferenceImage(XRReferenceImageLibrary library, string targetName, Texture2D texture)
        {
            int index = library.count;
            float height = PrintedWidthMetres * texture.height / texture.width;
            library.Add();
            library.SetName(index, targetName);
            library.SetTexture(index, texture, false);
            library.SetSpecifySize(index, true);
            library.SetSize(index, new Vector2(PrintedWidthMetres, height));
        }

        private static void EnsureImageTargetImportSettings(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer ||
                importer.npotScale == TextureImporterNPOTScale.None)
                return;

            // Reference-image aspect ratio must stay exact. Unity's default NPOT conversion would
            // resize this 452x678 portrait image and can make the tracked pose/size inaccurate.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Builds (or reuses) a prefab wrapping the Notre-Dame FBX, normalized to
        /// <see cref="NotreDameDisplayHeight"/> metres tall with its base sitting at local Y=0 (so
        /// it stands on the tracked image plane) and centred on X/Z. A BoxCollider sized to the
        /// model's bounds lets <see cref="LandmarkImageTrackingController"/> raycast-detect taps
        /// on it. The FBX's own materials already reference the Universal Render Pipeline/Lit
        /// shader (auto-generated by the importer from the accompanying textures), so no material
        /// rebuilding is needed here, unlike the onigiri food model.
        /// </summary>
        private static GameObject EnsureNotreDameModelPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(NotreDamePrefabPath);
            if (existing != null)
                return existing;

            EnsureFolder(NotreDameGeneratedFolder);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(NotreDameModelPath)
                ?? throw new InvalidOperationException("Notre-Dame FBX not found at " + NotreDameModelPath);

            var root = new GameObject("NotreDameLandmark");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "Notre-Dame Visual";
                visual.transform.SetParent(root.transform, false);

                float scale = NotreDameDisplayHeight / MeasureBounds(visual).size.y;
                visual.transform.localScale = Vector3.one * scale;
                Physics.SyncTransforms();

                Bounds scaledBounds = MeasureBounds(visual);
                visual.transform.localPosition = new Vector3(-scaledBounds.center.x, -scaledBounds.min.y, -scaledBounds.center.z);
                Physics.SyncTransforms();

                Bounds finalBounds = MeasureBounds(visual);
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = finalBounds.center;
                collider.size = finalBounds.size;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, NotreDamePrefabPath);
                if (saved == null)
                    throw new InvalidOperationException("Could not save " + NotreDamePrefabPath);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsureLandmark81ModelPrefab()
        {
            EnsureFolder(Landmark81GeneratedFolder);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Landmark81ModelPath)
                ?? throw new InvalidOperationException("Landmark 81 model is not imported at " + Landmark81ModelPath);

            var root = new GameObject("Landmark81Landmark");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "Landmark 81 Visual";
                visual.transform.SetParent(root.transform, false);

                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Temporary Landmark model has no Renderer.");

                Bounds sourceBounds = MeasureBounds(visual);
                float scale = Landmark81DisplayHeight / sourceBounds.size.y;
                visual.transform.localScale = Vector3.one * scale;
                Physics.SyncTransforms();

                Bounds scaledBounds = MeasureBounds(visual);
                visual.transform.localPosition = new Vector3(-scaledBounds.center.x, -scaledBounds.min.y, -scaledBounds.center.z);
                Physics.SyncTransforms();

                Bounds finalBounds = MeasureBounds(visual);
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = finalBounds.center;
                collider.size = finalBounds.size;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, Landmark81PrefabPath);
                if (saved == null)
                    throw new InvalidOperationException("Could not save " + Landmark81PrefabPath);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Bounds MeasureBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(item => item.path != ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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

    internal sealed class LandmarkScanBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android)
                LandmarkScanSetup.PrepareAssetsForPlayerBuild();
        }
    }
}
