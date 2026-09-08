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
        private const string MaterialPath = LibraryFolder + "/LandmarkScanMarker.mat";
        private const string TargetName = "notre-dame-basilica";
        private const float PrintedWidthMetres = 0.20f;

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

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Tracked Target Test Cube";
            marker.transform.localScale = new Vector3(0.12f, 0.04f, 0.12f);
            marker.GetComponent<Renderer>().sharedMaterial = BuildMarkerMaterial();
            marker.SetActive(false);

            var controller = originObject.AddComponent<LandmarkImageTrackingController>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("imageManager").objectReferenceValue = imageManager;
            serializedController.FindProperty("trackedContent").objectReferenceValue = marker;
            serializedController.FindProperty("expectedTargetName").stringValue = TargetName;
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

        private static Material BuildMarkerMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                                ?? throw new InvalidOperationException("URP Unlit shader is unavailable.");
                material = new Material(shader) { name = "Landmark Scan Marker" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetColor("_BaseColor", new Color(0.22f, 0.9f, 0.38f, 1f));
            EditorUtility.SetDirty(material);
            return material;
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
