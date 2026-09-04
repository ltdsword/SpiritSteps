using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CorgiAR.Tests
{
    public sealed class PetMenuPresentationTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [Test]
        public void OpeningMenu_RaisesFullScreenModalAboveHud()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene reopened = default;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                reopened = scene;
                GameObject hud = FindInScene(scene, "Corgi AR HUD");
                Assert.IsNotNull(hud, "generated HUD is missing");

                Transform panel = hud.transform.Find("Pet Panel");
                Assert.IsNotNull(panel, "pet menu panel is missing");

                Component menu = panel.GetComponents<Component>()
                    .First(c => c != null && c.GetType().Name == "PetMenuPanel");
                menu.GetType().GetMethod("Open").Invoke(menu, null);

                Assert.IsTrue(panel.gameObject.activeSelf, "opening the menu must make it visible");
                Assert.AreEqual(hud.transform.childCount - 1, panel.GetSiblingIndex(),
                    "the modal must render above every HUD control");

                var rect = (RectTransform)panel;
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero),
                    "the modal backdrop must cover the full screen");
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one),
                    "the modal backdrop must cover the full screen");

                CanvasGroup group = panel.GetComponent<CanvasGroup>();
                Assert.IsNotNull(group, "the modal must block input from controls behind it");
                Assert.IsTrue(group.interactable);
                Assert.IsTrue(group.blocksRaycasts);
                Assert.IsNotNull(panel.Find("Sheet/Close"), "the modal needs an obvious close button");
            }
            finally
            {
                if (previousSetup != null && previousSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                else if (reopened.IsValid())
                    EditorSceneManager.CloseScene(reopened, true);
            }
        }

        [Test]
        public void BindingPet_PreservesSourcePrefabRootScale()
        {
            Type binderType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("CorgiAR.PetBinder"))
                .FirstOrDefault(type => type != null);
            Assert.IsNotNull(binderType, "PetBinder runtime type is missing");

            var wrapper = new GameObject("wrapper");
            var source = new GameObject("native-scale pet");
            source.transform.localScale = new Vector3(2f, 3f, 4f);
            try
            {
                Component binder = wrapper.AddComponent(binderType);
                var serialized = new SerializedObject(binder);
                SerializedProperty bindings = serialized.FindProperty("bindings");
                bindings.arraySize = 1;
                SerializedProperty binding = bindings.GetArrayElementAtIndex(0);
                binding.FindPropertyRelative("Id").stringValue = "native";
                binding.FindPropertyRelative("DisplayName").stringValue = "Native";
                binding.FindPropertyRelative("Family").enumValueIndex = 1;
                binding.FindPropertyRelative("Prefab").objectReferenceValue = source;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                binderType.GetMethod("Bind")?.Invoke(binder, new object[] { "native" });

                Transform visual = wrapper.transform.Find("Pet Visual");
                Assert.IsNotNull(visual, "binding should create the visual child");
                Assert.That(visual.localScale, Is.EqualTo(source.transform.localScale),
                    "pet selection must retain the model author's native root scale");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wrapper);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == objectName)
                        return child.gameObject;
            }
            return null;
        }
    }
}
