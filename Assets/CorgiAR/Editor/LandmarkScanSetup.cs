using System;
using System.IO;
using System.Linq;
using CorgiAR;
using Unity.XR.CoreUtils;
using UnityEditor;
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
    /// <summary>Builds the isolated AR Foundation image-tracking proof of concept.</summary>
    public static class LandmarkScanSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/LandmarkScan.unity";
        private const string SourceImagePath = "Assets/_Project/Art/UI/ReferenceTemp/Landmarks/notre-dame.png";
        private const string TargetFolder = "Assets/_Project/Art/AR/Targets";
        private const string TargetImagePath = TargetFolder + "/notre-dame-basilica.png";
        private const string LibraryFolder = "Assets/_Project/Resources/AR";
        private const string LibraryPath = LibraryFolder + "/LandmarkReferenceImageLibrary.asset";
        private const string TargetName = "notre-dame-basilica";
        private const float PrintedWidthMetres = 0.20f;

        private const string NotreDameModelFolder = "Assets/_Project/Resources/AR/notre-dame-cathedral-basilica-of-saigon";
        private const string NotreDameModelPath = NotreDameModelFolder + "/source/Notre_Dame.fbx";
        private const string NotreDameGeneratedFolder = NotreDameModelFolder + "/Generated";
        private const string NotreDamePrefabPath = NotreDameGeneratedFolder + "/NotreDameLandmark.prefab";
        /// <summary>Display height in metres - taller than the 0.20m printed marker so the model
        /// reads as a landmark standing on the card rather than a flat decal.</summary>
        private const float NotreDameDisplayHeight = 0.32f;

        [MenuItem("Tools/AR Walking/Landmark Scan/Build Notre-Dame POC")]
        public static void BuildMenu()
        {
            Build();
            EditorUtility.DisplayDialog("Landmark Scan",
                "Created LandmarkScan.unity and a 20 cm Notre-Dame reference image.\n\n" +
                "Open the scene for Editor UI simulation, or use the Android POC build menu for a real scan.", "OK");
        }

        public static void Build()
        {
            EnsureFolder(TargetFolder);
            EnsureFolder(LibraryFolder);

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TargetImagePath) == null)
            {
                if (!AssetDatabase.CopyAsset(SourceImagePath, TargetImagePath))
                    throw new InvalidOperationException("Could not copy landmark target from " + SourceImagePath);
                AssetDatabase.ImportAsset(TargetImagePath, ImportAssetOptions.ForceSynchronousImport);
            }

            Texture2D targetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TargetImagePath)
                                      ?? throw new InvalidOperationException("Target image is missing: " + TargetImagePath);
            XRReferenceImageLibrary library = BuildReferenceLibrary(targetTexture);

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
            imageManager.requestedMaxNumberOfMovingImages = 1;

            GameObject modelPrefab = EnsureNotreDameModelPrefab();
            GameObject marker = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            marker.name = "Notre-Dame Landmark Model";
            marker.SetActive(false);

            var controller = originObject.AddComponent<LandmarkImageTrackingController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("imageManager").objectReferenceValue = imageManager;
            serializedController.FindProperty("trackedContent").objectReferenceValue = marker;
            serializedController.FindProperty("expectedTargetName").stringValue = TargetName;
            serializedController.FindProperty("showTrackedContent").boolValue = true;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save " + ScenePath);

            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("LANDMARK_SCAN_POC_BUILT target=" + TargetName + " width=0.20m scene=" + ScenePath);
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

        private static XRReferenceImageLibrary BuildReferenceLibrary(Texture2D texture)
        {
            XRReferenceImageLibrary library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            while (library.count > 0)
                library.RemoveAt(library.count - 1);

            float height = PrintedWidthMetres * texture.height / texture.width;
            library.Add();
            library.SetName(0, TargetName);
            library.SetTexture(0, texture, false);
            library.SetSpecifySize(0, true);
            library.SetSize(0, new Vector2(PrintedWidthMetres, height));
            EditorUtility.SetDirty(library);
            return library;
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
}
