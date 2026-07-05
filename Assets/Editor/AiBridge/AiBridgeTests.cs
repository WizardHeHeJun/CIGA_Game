// AiBridge 测试运行：经 TestRunnerApi 跑 EditMode/PlayMode 测试，结果写文件供 CLI 轮询。
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Ciga.AiBridge
{
    public static class AiBridgeTests
    {
        private static TestRunnerApi _api;

        public static void Run(string mode, string resultPath, Action<bool> ack)
        {
            try
            {
                TestMode tm = (mode != null && mode.ToLowerInvariant().StartsWith("play"))
                    ? TestMode.PlayMode : TestMode.EditMode;

                if (_api == null)
                    _api = ScriptableObject.CreateInstance<TestRunnerApi>();

                CleanupInitTestScenes();

                // 先写一个 running 占位
                File.WriteAllText(resultPath,
                    "{\"status\":\"running\",\"mode\":\"" + tm + "\"}", Encoding.UTF8);

                _api.RegisterCallbacks(new Callbacks(resultPath));
                _api.Execute(new ExecutionSettings(new Filter { testMode = tm }));
                ack?.Invoke(true);
            }
            catch (Exception e)
            {
                try { File.WriteAllText(resultPath, "{\"status\":\"error\",\"message\":\"" + e.Message.Replace("\"", "'") + "\"}", Encoding.UTF8); } catch { }
                ack?.Invoke(false);
            }
        }

        private static void CleanupInitTestScenes()
        {
            string[] sceneGuids = AssetDatabase.FindAssets("InitTestScene t:Scene", new[] { "Assets" });
            bool deletedAny = false;
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string fileName = Path.GetFileName(path);
                if (!fileName.StartsWith("InitTestScene", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(path))
                {
                    deletedAny = true;
                }
            }

            if (deletedAny)
            {
                AssetDatabase.Refresh();
            }
        }

        private class Callbacks : ICallbacks
        {
            private readonly string _path;
            private int _passed, _failed, _skipped;
            private readonly StringBuilder _failures = new StringBuilder();

            public Callbacks(string path) { _path = path; }

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed: _passed++; break;
                    case TestStatus.Failed:
                        _failed++;
                        if (_failures.Length > 0) _failures.Append(",");
                        _failures.Append("{\"name\":\"").Append(Esc(result.Test.FullName))
                                 .Append("\",\"message\":\"").Append(Esc(result.Message)).Append("\"}");
                        break;
                    case TestStatus.Skipped: _skipped++; break;
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.Append("{\"status\":\"done\",")
                  .Append("\"time\":\"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("\",")
                  .Append("\"passed\":").Append(_passed).Append(",")
                  .Append("\"failed\":").Append(_failed).Append(",")
                  .Append("\"skipped\":").Append(_skipped).Append(",")
                  .Append("\"failures\":[").Append(_failures).Append("]}");
                try { File.WriteAllText(_path, sb.ToString(), Encoding.UTF8); } catch { }
            }

            private static string Esc(string s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
            }
        }
    }
}
