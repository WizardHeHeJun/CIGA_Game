// ------------------------------------------------------------
// StartupSetup.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using Ciga.Startup;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ciga.Startup.EditorTools
{
    /// <summary>
    /// 一键生成启动流装配：3 个 SO + Login/GameMain 场景（已接线）+ 加入 Build Settings。
    /// 幂等：SO 已存在则复用；场景覆盖重建（退役旧 GameMain 组件）。
    /// 菜单：Ciga/Startup/生成启动流装配。
    /// </summary>
    public static class StartupSetup
    {
        private const string SoDir = "Assets/Res/Config/Startup";
        private const string LoginScenePath = "Assets/Res/Scene/Login.unity";
        private const string GameMainScenePath = "Assets/Res/Scene/GameMain.unity";
        private const string BootstrapScenePath = "Assets/Res/AnchorHorror/Bootstrap.unity";
        private const string HorrorScenePath = "Assets/Res/AnchorHorror/HorrorLevel.unity";

        [MenuItem("Ciga/Startup/生成启动流装配")]
        public static void BuildAll()
        {
            EnsureFolder(SoDir);

            // 1. CreateOrLoad 三个 SO
            var loginCfg = CreateOrLoad<LoginPanelConfig>(SoDir + "/LoginPanelConfig.asset");
            var mainCfg = CreateOrLoad<MainMenuConfig>(SoDir + "/MainMenuConfig.asset");
            var loadingCfg = CreateOrLoad<LoadingConfig>(SoDir + "/LoadingConfig.asset");

            // 2. 确认 TMP Essential Resources 已导入（CJK fallback 已由 AnchorHorrorSetup 配好，不重复配）
            EnsureTmpEssentials();

            // 3. 当前活动场景须有路径，防止 Single 模式弹保存框
            var active = SceneManager.GetActiveScene();
            string tempActive = null;
            if (string.IsNullOrEmpty(active.path))
            {
                tempActive = SoDir + "/__temp_startup.unity";
                EditorSceneManager.SaveScene(active, tempActive);
            }

            // 4. 构建 Login.unity
            BuildScene(LoginScenePath, () => PopulateLoginScene(loginCfg, loadingCfg));

            // 5. 构建 GameMain.unity（覆盖旧版，退役旧 GameMain 组件）
            BuildScene(GameMainScenePath, () => PopulateGameMainScene(mainCfg, loadingCfg));

            // 6. 更新 Build Settings（Login=0, GameMain=1, Bootstrap=2, HorrorLevel=3）
            AddScenesToBuildSettings();

            if (tempActive != null)
            {
                AssetDatabase.DeleteAsset(tempActive);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[StartupSetup] 启动流装配已生成。Login.unity 已打开，菜单栏 Play 即可联调。");
        }

        // Login 场景：Camera + Canvas + EventSystem(StandaloneInputModule) + LoginPanel + SceneLoader 挂点
        private static void PopulateLoginScene(LoginPanelConfig loginCfg, LoadingConfig loadingCfg)
        {
            // 相机
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.tag = "MainCamera";

            // Canvas
            var canvasGo = BuildCanvas("UICanvas");

            // EventSystem（StandaloneInputModule，纯旧 Input Manager）
            BuildEventSystem();

            // SceneLoader 挂点（DontDestroyOnLoad，常驻）
            var loaderGo = new GameObject("SceneLoader");
            var loader = loaderGo.AddComponent<SceneLoader>();
            WireObj(loader, "_loadingConfig", loadingCfg);

            // LoginPanel 根节点
            var panelRoot = NewUiNode(canvasGo.transform, "LoginPanelRoot");
            StretchFull(panelRoot);
            var panelBg = panelRoot.gameObject.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            panelBg.raycastTarget = false;

            // 背景图层
            var bgNode = NewUiNode(panelRoot, "Background");
            StretchFull(bgNode);
            var bgImg = bgNode.gameObject.AddComponent<Image>();
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            // Logo 图（居中偏上）
            var logoNode = NewUiNode(panelRoot, "Logo");
            logoNode.anchorMin = new Vector2(0.5f, 0.6f);
            logoNode.anchorMax = new Vector2(0.5f, 0.6f);
            logoNode.pivot = new Vector2(0.5f, 0.5f);
            logoNode.sizeDelta = new Vector2(400f, 160f);
            logoNode.anchoredPosition = Vector2.zero;
            var logoImg = logoNode.gameObject.AddComponent<Image>();
            logoImg.color = new Color(1f, 1f, 1f, 0f); // 默认透明，ApplyConfig 有图时显示
            logoImg.raycastTarget = false;

            // 进入按钮（居中偏下）
            var btnNode = NewUiNode(panelRoot, "EnterButton");
            btnNode.anchorMin = new Vector2(0.5f, 0.35f);
            btnNode.anchorMax = new Vector2(0.5f, 0.35f);
            btnNode.pivot = new Vector2(0.5f, 0.5f);
            btnNode.sizeDelta = new Vector2(240f, 64f);
            btnNode.anchoredPosition = Vector2.zero;
            var btnBg = btnNode.gameObject.AddComponent<Image>();
            btnBg.color = new Color(0.25f, 0.45f, 0.85f, 1f);
            var enterBtn = btnNode.gameObject.AddComponent<Button>();

            // 按钮文字
            var btnLabelNode = NewUiNode(btnNode, "Label");
            StretchFull(btnLabelNode);
            var btnLabel = btnLabelNode.gameObject.AddComponent<TextMeshProUGUI>();
            btnLabel.text = "进入游戏";
            btnLabel.fontSize = 28f;
            btnLabel.alignment = TextAlignmentOptions.Center;
            btnLabel.color = Color.white;
            btnLabel.raycastTarget = false;

            // 副标题（按钮下方，默认隐藏）
            var subtitleNode = NewUiNode(panelRoot, "Subtitle");
            subtitleNode.anchorMin = new Vector2(0.5f, 0.28f);
            subtitleNode.anchorMax = new Vector2(0.5f, 0.28f);
            subtitleNode.pivot = new Vector2(0.5f, 0.5f);
            subtitleNode.sizeDelta = new Vector2(600f, 36f);
            subtitleNode.anchoredPosition = Vector2.zero;
            var subtitleLabel = subtitleNode.gameObject.AddComponent<TextMeshProUGUI>();
            subtitleLabel.text = string.Empty;
            subtitleLabel.fontSize = 22f;
            subtitleLabel.alignment = TextAlignmentOptions.Center;
            subtitleLabel.color = new Color(1f, 1f, 1f, 0.6f);
            subtitleLabel.raycastTarget = false;
            subtitleNode.gameObject.SetActive(false);

            // 挂 LoginPanel 并接线
            var loginPanel = panelRoot.gameObject.AddComponent<LoginPanel>();
            WireObj(loginPanel, "_root", panelRoot.gameObject);
            WireObj(loginPanel, "_config", loginCfg);
            WireObj(loginPanel, "_backgroundImage", bgImg);
            WireObj(loginPanel, "_logoImage", logoImg);
            WireObj(loginPanel, "_enterButton", enterBtn);
            WireObj(loginPanel, "_enterButtonLabel", btnLabel);
            WireObj(loginPanel, "_subtitleLabel", subtitleLabel);
        }

        // GameMain 场景：Canvas + EventSystem + MainMenuPanel + SceneLoader 挂点（旧 GameMain.cs 已退役，不再添加）
        private static void PopulateGameMainScene(MainMenuConfig mainCfg, LoadingConfig loadingCfg)
        {
            // 相机
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.tag = "MainCamera";

            // Canvas
            var canvasGo = BuildCanvas("UICanvas");

            // EventSystem（StandaloneInputModule）
            BuildEventSystem();

            // SceneLoader 挂点
            var loaderGo = new GameObject("SceneLoader");
            var loader = loaderGo.AddComponent<SceneLoader>();
            WireObj(loader, "_loadingConfig", loadingCfg);

            // MainMenuPanel 根节点（全屏）
            var panelRoot = NewUiNode(canvasGo.transform, "MainMenuPanelRoot");
            StretchFull(panelRoot);
            var panelBg = panelRoot.gameObject.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.06f, 0.10f, 1f);
            panelBg.raycastTarget = false;

            // 背景图层
            var bgNode = NewUiNode(panelRoot, "Background");
            StretchFull(bgNode);
            var bgImg = bgNode.gameObject.AddComponent<Image>();
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;

            // Logo 图（顶部居中）
            var logoNode = NewUiNode(panelRoot, "Logo");
            logoNode.anchorMin = new Vector2(0.5f, 0.7f);
            logoNode.anchorMax = new Vector2(0.5f, 0.7f);
            logoNode.pivot = new Vector2(0.5f, 0.5f);
            logoNode.sizeDelta = new Vector2(400f, 120f);
            logoNode.anchoredPosition = Vector2.zero;
            var logoImg = logoNode.gameObject.AddComponent<Image>();
            logoImg.color = new Color(1f, 1f, 1f, 0f);
            logoImg.raycastTarget = false;

            // 标题文字
            var titleNode = NewUiNode(panelRoot, "Title");
            titleNode.anchorMin = new Vector2(0f, 0.55f);
            titleNode.anchorMax = new Vector2(1f, 0.55f);
            titleNode.pivot = new Vector2(0.5f, 0.5f);
            titleNode.sizeDelta = new Vector2(0f, 90f);
            titleNode.anchoredPosition = Vector2.zero;
            var titleLabel = titleNode.gameObject.AddComponent<TextMeshProUGUI>();
            titleLabel.text = "锚点解谜";
            titleLabel.fontSize = 64f;
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.alignment = TextAlignmentOptions.Center;
            titleLabel.color = Color.white;
            titleLabel.raycastTarget = false;

            // 开始按钮
            var startBtnNode = NewUiNode(panelRoot, "StartButton");
            startBtnNode.anchorMin = new Vector2(0.5f, 0.40f);
            startBtnNode.anchorMax = new Vector2(0.5f, 0.40f);
            startBtnNode.pivot = new Vector2(0.5f, 0.5f);
            startBtnNode.sizeDelta = new Vector2(240f, 64f);
            startBtnNode.anchoredPosition = Vector2.zero;
            var startBtnBg = startBtnNode.gameObject.AddComponent<Image>();
            startBtnBg.color = new Color(0.25f, 0.55f, 0.35f, 1f);
            var startBtn = startBtnNode.gameObject.AddComponent<Button>();
            var startLabelNode = NewUiNode(startBtnNode, "Label");
            StretchFull(startLabelNode);
            var startLabel = startLabelNode.gameObject.AddComponent<TextMeshProUGUI>();
            startLabel.text = "开始游戏";
            startLabel.fontSize = 28f;
            startLabel.alignment = TextAlignmentOptions.Center;
            startLabel.color = Color.white;
            startLabel.raycastTarget = false;

            // 退出按钮
            var quitBtnNode = NewUiNode(panelRoot, "QuitButton");
            quitBtnNode.anchorMin = new Vector2(0.5f, 0.30f);
            quitBtnNode.anchorMax = new Vector2(0.5f, 0.30f);
            quitBtnNode.pivot = new Vector2(0.5f, 0.5f);
            quitBtnNode.sizeDelta = new Vector2(240f, 64f);
            quitBtnNode.anchoredPosition = Vector2.zero;
            var quitBtnBg = quitBtnNode.gameObject.AddComponent<Image>();
            quitBtnBg.color = new Color(0.55f, 0.25f, 0.25f, 1f);
            var quitBtn = quitBtnNode.gameObject.AddComponent<Button>();
            var quitLabelNode = NewUiNode(quitBtnNode, "Label");
            StretchFull(quitLabelNode);
            var quitLabel = quitLabelNode.gameObject.AddComponent<TextMeshProUGUI>();
            quitLabel.text = "退出";
            quitLabel.fontSize = 28f;
            quitLabel.alignment = TextAlignmentOptions.Center;
            quitLabel.color = Color.white;
            quitLabel.raycastTarget = false;

            // 挂 MainMenuPanel 并接线
            var mainPanel = panelRoot.gameObject.AddComponent<MainMenuPanel>();
            WireObj(mainPanel, "_root", panelRoot.gameObject);
            WireObj(mainPanel, "_config", mainCfg);
            WireObj(mainPanel, "_backgroundImage", bgImg);
            WireObj(mainPanel, "_logoImage", logoImg);
            WireObj(mainPanel, "_titleLabel", titleLabel);
            WireObj(mainPanel, "_startButton", startBtn);
            WireObj(mainPanel, "_startButtonLabel", startLabel);
            WireObj(mainPanel, "_quitButton", quitBtn);
            WireObj(mainPanel, "_quitButtonLabel", quitLabel);
        }

        // Build Settings：顺序 Login(0) GameMain(1) Bootstrap(2) HorrorLevel(3)；剔除失效项、去重。
        private static void AddScenesToBuildSettings()
        {
            // 其余已存在场景（剔除失效项 + 将重排的 Login/GameMain）
            var rest = EditorBuildSettings.scenes
                .Where(sc => !string.IsNullOrEmpty(sc.path) && System.IO.File.Exists(sc.path))
                .Where(sc => sc.path != LoginScenePath && sc.path != GameMainScenePath)
                .ToList();

            // 明确按顺序构造：Login(0) → GameMain(1) → 其余（避免倒序 Insert 的可读性陷阱）
            var ordered = new List<EditorBuildSettingsScene>();
            if (System.IO.File.Exists(LoginScenePath))
            {
                ordered.Add(new EditorBuildSettingsScene(LoginScenePath, true));
            }

            if (System.IO.File.Exists(GameMainScenePath))
            {
                ordered.Add(new EditorBuildSettingsScene(GameMainScenePath, true));
            }

            ordered.AddRange(rest);

            // 确保 Bootstrap / HorrorLevel 在列（AnchorHorrorSetup 可能已加，这里补保险）
            EnsureScene(ordered, BootstrapScenePath);
            EnsureScene(ordered, HorrorScenePath);

            EditorBuildSettings.scenes = ordered.ToArray();
        }

        private static void EnsureScene(List<EditorBuildSettingsScene> list, string path)
        {
            if (!System.IO.File.Exists(path))
            {
                return;
            }

            if (!list.Any(sc => sc.path == path))
            {
                list.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        // Single 模式建/覆盖场景（先清空再重建），省去手动保存弹框。
        private static void BuildScene(string path, System.Action populate)
        {
            var scene = System.IO.File.Exists(path)
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Object.DestroyImmediate(roots[i]);
            }

            populate();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static GameObject BuildCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static void BuildEventSystem()
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static RectTransform NewUiNode(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static void EnsureTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                return;
            }

            Debug.LogWarning("[StartupSetup] 找不到 'Assets/TextMesh Pro'，TMP Essential Resources 可能未导入。" +
                             "可手动执行：Window → TextMeshPro → Import TMP Essential Resources。" +
                             "（CJK 中文 fallback 已由 AnchorHorrorSetup 配好，无需重复配置。）");
        }

        private static void WireObj(Component c, string prop, Object value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p != null)
            {
                p.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[StartupSetup] 接线失败：{c.GetType().Name} 无属性 '{prop}'", c);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
