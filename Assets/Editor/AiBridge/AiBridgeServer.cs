// AI 验证桥：Editor 内嵌本地 HTTP 服务，供 AI 通过 CLI 驱动 Unity。
// 端口 127.0.0.1:17900（避开其他工具常用端口，如 8090/18091）。仅 Editor 编译。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.SceneManagement;
using CompilerMessageType = UnityEditor.Compilation.CompilerMessageType;

namespace Ciga.AiBridge
{
    [InitializeOnLoad]
    public static class AiBridgeServer
    {
        public const int Port = 17900;
        private const string PrefAutoStart = "Ciga.AiBridge.AutoStart";

        private static HttpListener _listener;
        private static Thread _thread;
        private static volatile bool _running;

        // 主线程任务队列：HTTP 在后台线程，Unity API 必须回主线程执行
        private static readonly Queue<Action> _mainQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();

        // 控制台日志环形缓冲
        private struct LogItem { public string type; public string message; public string stack; public string time; }
        private static readonly List<LogItem> _logs = new List<LogItem>();
        private static readonly object _logLock = new object();
        private const int MaxLogs = 2000;

        private static string LibDir => Path.Combine(Directory.GetCurrentDirectory(), "Library", "AiBridge");
        private static string CompileResultPath => Path.Combine(LibDir, "compile.json");
        private static string TestResultPath => Path.Combine(LibDir, "tests.json");

        static AiBridgeServer()
        {
            Directory.CreateDirectory(LibDir);
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update += PumpMainThread;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            if (EditorPrefs.GetBool(PrefAutoStart, true))
                Start();
        }

        // ---------- 启停 ----------
        public static bool IsRunning => _running;

        public static void Start()
        {
            if (_running) return;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                _listener.Start();
                _running = true;
                _thread = new Thread(Loop) { IsBackground = true };
                _thread.Start();
                Debug.Log($"[AiBridge] 已启动 http://127.0.0.1:{Port}/");
            }
            catch (Exception e)
            {
                _running = false;
                Debug.LogWarning($"[AiBridge] 启动失败（端口被占用？）：{e.Message}");
            }
        }

        public static void Stop()
        {
            _running = false;
            try { _listener?.Stop(); _listener?.Close(); } catch { }
            _listener = null;
            Debug.Log("[AiBridge] 已停止");
        }

        // ---------- HTTP 主循环（后台线程）----------
        private static void Loop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try { ctx = _listener.GetContext(); }
                catch { break; }
                try { Handle(ctx); }
                catch (Exception e)
                {
                    TryWrite(ctx, 500, "{\"error\":\"" + Escape(e.Message) + "\"}");
                }
            }
        }

        private static void Handle(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
            var q = ctx.Request.QueryString;

            switch (path)
            {
                case "/health":
                    RunOnMain(() => Json(ctx, "{" +
                        $"\"ok\":true,\"port\":{Port}," +
                        $"\"unity\":\"{Application.unityVersion}\"," +
                        $"\"isCompiling\":{Lower(EditorApplication.isCompiling)}," +
                        $"\"isPlaying\":{Lower(EditorApplication.isPlaying)}" + "}"));
                    break;

                case "/console":
                    HandleConsole(ctx, q);
                    break;

                case "/compile":
                    RunOnMain(() =>
                    {
                        AssetDatabase.Refresh();
                        CompilationPipeline.RequestScriptCompilation();
                        Json(ctx, "{\"requested\":true,\"isCompiling\":" + Lower(EditorApplication.isCompiling) + "}");
                    });
                    break;

                case "/status":
                    RunOnMain(() => Json(ctx, "{" +
                        $"\"isCompiling\":{Lower(EditorApplication.isCompiling)}," +
                        $"\"isPlaying\":{Lower(EditorApplication.isPlaying)}," +
                        "\"compile\":" + ReadFileOr(CompileResultPath, "null") + "}"));
                    break;

                case "/play":
                    RunOnMain(() => { EditorApplication.isPlaying = true; Json(ctx, "{\"playing\":true}"); });
                    break;

                case "/stop":
                    RunOnMain(() => { EditorApplication.isPlaying = false; Json(ctx, "{\"playing\":false}"); });
                    break;

                case "/screenshot":
                    HandleScreenshot(ctx, q);
                    break;

                case "/hierarchy":
                    RunOnMain(() => Json(ctx, DumpHierarchy()));
                    break;

                case "/tests":
                    RunOnMain(() => AiBridgeTests.Run(q.Get("mode") ?? "edit", TestResultPath,
                        ok => Json(ctx, "{\"started\":" + Lower(ok) + "}")));
                    break;

                case "/tests/result":
                    Json(ctx, ReadFileOr(TestResultPath, "null"));
                    break;

                default:
                    TryWrite(ctx, 404, "{\"error\":\"unknown path\"}");
                    break;
            }
        }

        private static void HandleConsole(HttpListenerContext ctx, System.Collections.Specialized.NameValueCollection q)
        {
            string level = (q.Get("level") ?? "all").ToLowerInvariant();
            int limit = ParseInt(q.Get("limit"), 100);
            var sb = new StringBuilder();
            sb.Append("{\"logs\":[");
            lock (_logLock)
            {
                int start = 0;
                var filtered = new List<LogItem>();
                foreach (var l in _logs)
                {
                    bool keep = level == "all"
                        || (level == "error" && (l.type == "Error" || l.type == "Exception" || l.type == "Assert"))
                        || (level == "warn" && (l.type == "Warning" || l.type == "Error" || l.type == "Exception"));
                    if (keep) filtered.Add(l);
                }
                start = Math.Max(0, filtered.Count - limit);
                for (int i = start; i < filtered.Count; i++)
                {
                    if (i > start) sb.Append(",");
                    var l = filtered[i];
                    sb.Append("{\"type\":\"").Append(Escape(l.type)).Append("\",")
                      .Append("\"message\":\"").Append(Escape(l.message)).Append("\",")
                      .Append("\"time\":\"").Append(Escape(l.time)).Append("\"}");
                }
            }
            sb.Append("]}");
            Json(ctx, sb.ToString());
        }

        private static void HandleScreenshot(HttpListenerContext ctx, System.Collections.Specialized.NameValueCollection q)
        {
            string path = q.Get("path");
            if (string.IsNullOrEmpty(path))
                path = Path.Combine(LibDir, "shot_" + DateTime.Now.ToString("HHmmss") + ".png");
            RunOnMain(() =>
            {
                try
                {
                    ScreenCapture.CaptureScreenshot(path);
                    Json(ctx, "{\"path\":\"" + Escape(path) + "\",\"note\":\"PlayMode 下截 GameView，文件下一帧写盘\"}");
                }
                catch (Exception e) { Json(ctx, "{\"error\":\"" + Escape(e.Message) + "\"}"); }
            });
        }

        private static string DumpHierarchy()
        {
            var sb = new StringBuilder();
            Scene scene = SceneManager.GetActiveScene();
            sb.Append("{\"scene\":\"").Append(Escape(scene.name)).Append("\",\"roots\":[");
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (i > 0) sb.Append(",");
                AppendNode(sb, roots[i].transform, 0);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendNode(StringBuilder sb, Transform t, int depth)
        {
            sb.Append("{\"name\":\"").Append(Escape(t.name)).Append("\",")
              .Append("\"active\":").Append(Lower(t.gameObject.activeSelf)).Append(",")
              .Append("\"children\":[");
            if (depth < 6)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    if (i > 0) sb.Append(",");
                    AppendNode(sb, t.GetChild(i), depth + 1);
                }
            }
            sb.Append("]}");
        }

        // ---------- 日志捕获 ----------
        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            lock (_logLock)
            {
                _logs.Add(new LogItem
                {
                    type = type.ToString(),
                    message = condition,
                    stack = stackTrace,
                    time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                });
                if (_logs.Count > MaxLogs) _logs.RemoveRange(0, _logs.Count - MaxLogs);
            }
        }

        // ---------- 编译结果持久化（跨域重载用文件）----------
        private static readonly List<string> _compileErrors = new List<string>();
        private static readonly List<string> _compileWarnings = new List<string>();

        private static void OnCompilationStarted(object _)
        {
            _compileErrors.Clear();
            _compileWarnings.Clear();
        }

        private static void OnAssemblyFinished(string assemblyPath, CompilerMessage[] messages)
        {
            foreach (var m in messages)
            {
                string line = $"{m.file}({m.line},{m.column}): {m.message}";
                if (m.type == CompilerMessageType.Error) _compileErrors.Add(line);
                else if (m.type == CompilerMessageType.Warning) _compileWarnings.Add(line);
            }
        }

        private static void OnCompilationFinished(object _)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\"time\":\"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("\",")
                  .Append("\"errorCount\":").Append(_compileErrors.Count).Append(",")
                  .Append("\"warningCount\":").Append(_compileWarnings.Count).Append(",")
                  .Append("\"errors\":[").Append(JoinJson(_compileErrors)).Append("],")
                  .Append("\"warnings\":[").Append(JoinJson(_compileWarnings)).Append("]}");
                Directory.CreateDirectory(LibDir);
                File.WriteAllText(CompileResultPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        // ---------- 主线程调度 ----------
        private static void RunOnMain(Action work)
        {
            var done = new ManualResetEventSlim(false);
            lock (_queueLock) _mainQueue.Enqueue(() => { try { work(); } finally { done.Set(); } });
            // 等主线程执行完（带超时，避免编译/重载卡死请求）
            done.Wait(15000);
        }

        private static void PumpMainThread()
        {
            for (int i = 0; i < 16; i++)
            {
                Action a = null;
                lock (_queueLock) { if (_mainQueue.Count > 0) a = _mainQueue.Dequeue(); }
                if (a == null) break;
                try { a(); } catch (Exception e) { Debug.LogWarning("[AiBridge] " + e.Message); }
            }
        }

        // ---------- 工具 ----------
        private static void Json(HttpListenerContext ctx, string body) => TryWrite(ctx, 200, body);

        private static void TryWrite(HttpListenerContext ctx, int code, string body)
        {
            try
            {
                byte[] buf = Encoding.UTF8.GetBytes(body);
                ctx.Response.StatusCode = code;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.ContentLength64 = buf.Length;
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                ctx.Response.OutputStream.Close();
            }
            catch { }
        }

        private static string ReadFileOr(string path, string fallback)
        {
            try { return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : fallback; }
            catch { return fallback; }
        }

        private static string JoinJson(List<string> items)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("\"").Append(Escape(items[i])).Append("\"");
            }
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string Lower(bool b) => b ? "true" : "false";

        private static int ParseInt(string s, int def)
        {
            return int.TryParse(s, out int v) ? v : def;
        }
    }
}
