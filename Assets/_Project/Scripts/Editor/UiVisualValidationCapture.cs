#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ARWalking.UI;
using UnityEditor;
using UnityEngine;

namespace ARWalking.Editor
{
    public static class UiVisualValidationCapture
    {
        static int _framesRemaining;
        static string _capturePath;

        [MenuItem("Tools/AR Walking/Visual Checks/Capture 720x1600")]
        static void CaptureSmall() => BeginCapture(720, 1600);

        [MenuItem("Tools/AR Walking/Visual Checks/Capture 1080x2400")]
        static void CaptureReference() => BeginCapture(1080, 2400);

        [MenuItem("Tools/AR Walking/Visual Checks/Capture 1440x3200")]
        static void CaptureLarge() => BeginCapture(1440, 3200);

        static void BeginCapture(int width, int height)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("Enter Play Mode before capturing UI visual checks.");
                return;
            }

            SelectFixedGameViewSize(width, height);
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var directory = Path.Combine(root, "TestArtifacts", "UI");
            Directory.CreateDirectory(directory);
            _capturePath = Path.Combine(directory, $"Home_{width}x{height}_SafeArea.png");
            _framesRemaining = 16;
            EditorApplication.update -= CaptureAfterLayout;
            EditorApplication.update += CaptureAfterLayout;
        }

        static void CaptureAfterLayout()
        {
            _framesRemaining--;
            if (_framesRemaining == 8)
            {
                var simulatedInset = Mathf.Round(Screen.height * 0.04f);
                UiSafeAreaSimulation.Enabled = true;
                UiSafeAreaSimulation.SimulatedArea = new Rect(0, simulatedInset, Screen.width, Screen.height - simulatedInset * 2f);
                return;
            }
            if (_framesRemaining > 0) return;
            EditorApplication.update -= CaptureAfterLayout;
            var inset = Mathf.Round(Screen.height * 0.04f);
            ScreenCapture.CaptureScreenshot(_capturePath);
            EditorApplication.QueuePlayerLoopUpdate();
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
            Debug.Log($"UI_VISUAL_CAPTURE {_capturePath} actual={Screen.width}x{Screen.height} safeInset={inset}");
        }

        [MenuItem("Tools/AR Walking/Visual Checks/Clear Safe Area Simulation")]
        static void ClearSimulation()
        {
            UiSafeAreaSimulation.Enabled = false;
            Debug.Log("UI safe-area simulation cleared.");
        }

        static void SelectFixedGameViewSize(int width, int height)
        {
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            var groupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroup");
            var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            var viewType = editorAssembly.GetType("UnityEditor.GameView");
            if (sizesType == null || groupType == null || sizeType == null || viewType == null)
                throw new InvalidOperationException("Unity Game View reflection API is unavailable.");

            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singletonType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var group = getGroup?.Invoke(instance, new object[] { (int)GameViewSizeGroupType.Android });
            if (group == null) throw new InvalidOperationException("Android Game View size group is unavailable.");

            var label = $"ARW {width}x{height}";
            var displayTexts = (string[])groupType.GetMethod("GetDisplayTexts")?.Invoke(group, null);
            var index = displayTexts == null ? -1 : Array.FindIndex(displayTexts, text => text.Contains(label));
            if (index < 0)
            {
                var constructor = sizeType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .First(info => info.GetParameters().Length == 4);
                var enumValue = Enum.ToObject(constructor.GetParameters()[0].ParameterType, 1);
                var size = constructor.Invoke(new object[] { enumValue, width, height, label });
                groupType.GetMethod("AddCustomSize")?.Invoke(group, new[] { size });
                index = (int)groupType.GetMethod("GetTotalCount")?.Invoke(group, null) - 1;
            }

            var view = EditorWindow.GetWindow(viewType);
            var selectedSize = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            selectedSize?.SetValue(view, index);
            view.Repaint();
        }
    }
}
#endif
