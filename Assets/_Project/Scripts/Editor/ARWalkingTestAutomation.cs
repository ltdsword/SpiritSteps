#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ARWalking.Editor
{
    [InitializeOnLoad]
    public static class ARWalkingTestAutomation
    {
        static TestRunnerApi _api;
        static ARWalkingTestCallbacks _callbacks;

        static ARWalkingTestAutomation() => RegisterCallbacks();

        [MenuItem("Tools/AR Walking/Tests/Run Edit Mode")]
        public static void RunEditMode() => Run(TestMode.EditMode, "ARWalking.EditModeTests");

        [MenuItem("Tools/AR Walking/Tests/Run Play Mode")]
        public static void RunPlayMode() => Run(TestMode.PlayMode, "ARWalking.PlayModeTests");

        static void RegisterCallbacks()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = ScriptableObject.CreateInstance<ARWalkingTestCallbacks>();
            _api.RegisterCallbacks(_callbacks);
        }

        static void Run(TestMode mode, string assemblyName)
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var existingReport = Path.Combine(root, "TestArtifacts", "Results", mode + ".txt");
            if (File.Exists(existingReport)) File.Delete(existingReport);
            var settings = new ExecutionSettings
            {
                filters = new[] { new Filter { testMode = mode, assemblyNames = new[] { assemblyName } } }
            };
            _api.Execute(settings);
            Debug.Log("ARW_TEST_STARTED " + mode + " " + assemblyName);
        }
    }

    internal sealed class ARWalkingTestCallbacks : ScriptableObject, ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            // Derived straight from the result rather than tracked via shared EditorPrefs state -
            // RunEditMode()/RunPlayMode() are often queued back-to-back (e.g. from a single
            // automation script), and a single "currently active mode" key gets overwritten by the
            // second call before the first run's async RunFinished fires, corrupting both reports.
            var mode = result.Test.TestMode;
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var directory = Path.Combine(root, "TestArtifacts", "Results");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, mode + ".txt");
            var report = new StringBuilder();
            report.AppendLine("status=" + result.TestStatus);
            report.AppendLine("passed=" + result.PassCount);
            report.AppendLine("failed=" + result.FailCount);
            report.AppendLine("skipped=" + result.SkipCount);
            report.AppendLine("inconclusive=" + result.InconclusiveCount);
            AppendFailures(result, report);
            File.WriteAllText(path, report.ToString());
            Debug.Log("ARW_TEST_RESULT " + mode + " status=" + result.TestStatus + " passed=" + result.PassCount + " failed=" + result.FailCount + " report=" + path);
        }

        static void AppendFailures(ITestResultAdaptor result, StringBuilder report)
        {
            if (result.FailCount > 0 && !result.Children.Any())
            {
                report.AppendLine("FAIL " + result.Test.FullName);
                report.AppendLine(result.Message);
                report.AppendLine(result.StackTrace);
            }
            foreach (var child in result.Children) AppendFailures(child, report);
        }
    }
}
#endif
