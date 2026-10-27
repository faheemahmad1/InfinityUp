using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Poi.Tests
{
    [InitializeOnLoad]
    public static class PoiTestDriver
    {
        const string FlagPath = "Docs/run-tests.flag";
        const string SummaryPath = "Docs/editmode-summary.txt";
        static TestRunnerApi api;
        static bool started;

        static PoiTestDriver()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (started || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!File.Exists(FlagPath))
            {
                return;
            }

            string filter = File.ReadAllText(FlagPath).Trim();
            File.Delete(FlagPath);
            started = true;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.hideFlags = HideFlags.HideAndDontSave;
            api.RegisterCallbacks(new Sink());
            var testFilter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "Poi.Tests" }
            };
            if (!string.IsNullOrEmpty(filter) && filter != "start")
            {
                testFilter.testNames = new[] { filter };
            }

            api.Execute(new ExecutionSettings(testFilter));
            Debug.Log("POI edit-mode tests started");
        }

        sealed class Sink : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var builder = new StringBuilder();
                builder.Append("passed=").Append(result.PassCount);
                builder.Append(" failed=").Append(result.FailCount);
                builder.Append(" skipped=").Append(result.SkipCount);
                builder.Append(" inconclusive=").Append(result.InconclusiveCount);
                builder.Append('\n');
                WriteFailures(builder, result);
                Directory.CreateDirectory("Docs");
                File.WriteAllText(SummaryPath, builder.ToString());
                Debug.Log("POI edit-mode tests finished " + builder.ToString().Split('\n')[0]);
                started = false;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            static void WriteFailures(StringBuilder builder, ITestResultAdaptor result)
            {
                if (result.HasChildren)
                {
                    foreach (ITestResultAdaptor child in result.Children)
                    {
                        WriteFailures(builder, child);
                    }

                    return;
                }

                if (result.TestStatus == TestStatus.Passed || result.TestStatus == TestStatus.Skipped)
                {
                    return;
                }

                builder.Append(result.Test.FullName).Append(" [").Append(result.TestStatus).Append("]\n");
                if (!string.IsNullOrEmpty(result.Message))
                {
                    builder.Append(result.Message).Append('\n');
                }

                if (!string.IsNullOrEmpty(result.StackTrace))
                {
                    builder.Append(result.StackTrace).Append('\n');
                }
            }
        }
    }
}
