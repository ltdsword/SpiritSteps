using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CorgiAR.Tests
{
    /// <summary>
    /// The Corgi is touch-only: the companion prefab must carry no hand-tracking
    /// or MediaPipe component and no missing scripts.
    /// </summary>
    public sealed class PrefabHygieneTests
    {
        private const string PrefabPath = "Assets/CorgiAR/Prefabs/CorgiARCompanion.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        private static readonly string[] BannedNameFragments =
        {
            "MediaPipe", "Mediapipe", "HandLandmark", "HandInteraction",
            "MouseHand", "CameraUvMapper"
        };

        [Test]
        public void CompanionPrefab_HasNoHandTrackingOrMissingScripts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "CorgiARCompanion.prefab is missing at " + PrefabPath);

            AssertHierarchyIsTouchOnly(prefab, "Companion prefab");
        }

        [Test]
        public void SampleScene_HasNoHandTrackingOrMissingScripts()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForTest = !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    AssertHierarchyIsTouchOnly(root, "SampleScene");
            }
            finally
            {
                if (openedForTest)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertHierarchyIsTouchOnly(GameObject root, string context)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                Assert.IsNotNull(component,
                    context + " has a missing script component.");

                string typeName = component.GetType().Name;
                Assert.IsFalse(
                    BannedNameFragments.Any(fragment => typeName.Contains(fragment)),
                    $"{context} still carries a hand-tracking component: {typeName}");
            }
        }
    }
}
