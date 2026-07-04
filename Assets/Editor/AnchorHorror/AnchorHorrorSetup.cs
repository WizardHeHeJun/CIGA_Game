// ------------------------------------------------------------
// AnchorHorrorSetup.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ciga.AnchorHorror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 一键生成锚点解谜的可运行装配：3 个 SO + Bootstrap/HorrorLevel 场景（已接线）+ 加入 Build Settings。
    /// 幂等：已存在则复用。菜单 Ciga/AnchorHorror/生成可运行装配。
    /// 界面走 uGUI Canvas（记忆面板 + 结算界面，均 TMP）；DebugHUD 仍用 IMGUI；黑屏/音频等可选引用留空（运行时判空）。
    /// 场景以"附加模式"离线构建，绝不替换/干扰用户当前打开的场景（避免自动化下弹保存框）。
    /// </summary>
    public static class AnchorHorrorSetup
    {
        private const string SoDir = "Assets/Res/AnchorHorror";
        private const string BootstrapScene = SoDir + "/Bootstrap.unity";
        private const string HorrorScene = SoDir + "/HorrorLevel.unity";
        private const string SquareSpritePath = SoDir + "/WhiteSquare.png";
        private const string CjkTtfPath = SoDir + "/AnchorCJK.ttf";
        private const string CjkFontPath = SoDir + "/AnchorCJK SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private static Sprite _squareSprite;

        [MenuItem("Ciga/AnchorHorror/生成可运行装配")]
        public static void BuildAll()
        {
            EnsureFolder(SoDir);

            var cfg = CreateOrLoad<GlobalConfig>(SoDir + "/GlobalConfig.asset");
            var db = CreateOrLoad<FeatureDatabase>(SoDir + "/FeatureDatabase.asset");
            var level = CreateOrLoad<LevelConfig>(SoDir + "/LevelConfig.asset");
            PopulateFeatureDatabase(db);               // 填中文特征名/关键词颜色（空库时）
            _squareSprite = GetOrCreateSquareSprite(); // 玩家/物品可见所需的方块 sprite
            EnsureTmpEssentials();                     // 保证 TMP 通用字体(LiberationSans)+着色器可用（浮字/面板文本）
            EnsureCjkFallback();                       // 中文字形回退（黑体动态字体加入 TMP 全局 fallback）
            BakeCjkGlyphs(db);                         // 预烤用到的中文字形进图集，避免编辑态动态生成时机导致品红闪现

            // 使当前场景"干净有路径"，后续 Single 建场景才不会弹保存框（自动化/测试环境活动场景常是未命名的）
            var active = SceneManager.GetActiveScene();
            string tempActive = null;
            if (string.IsNullOrEmpty(active.path))
            {
                tempActive = SoDir + "/__temp_active.unity";
                EditorSceneManager.SaveScene(active, tempActive);
            }

            BuildScene(HorrorScene, PopulateHorrorLevel);
            BuildScene(BootstrapScene, () => PopulateBootstrap(cfg, db, level));
            AddScenesToBuildSettings();

            if (tempActive != null)
            {
                AssetDatabase.DeleteAsset(tempActive); // 已被 Bootstrap(Single) 取代关闭，可删
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // 此时 Bootstrap 已是打开的活动场景，按 Play 即可联调
            Debug.Log("[AnchorHorror] 可运行装配已生成并打开 " + BootstrapScene + "，按 Play 即可联调。");
        }

        // Single 模式建/覆盖场景：打开已存在的或新建空场景 → 清空 → 重建 → 保存。天然避免同路径冲突。
        // 前置：当前场景须"干净有路径"（BuildAll 已用临时场景处理未命名情形），否则 Single 会弹保存框。
        private static void BuildScene(string path, Action populate)
        {
            var scene = System.IO.File.Exists(path)
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(roots[i]);
            }

            populate();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void PopulateBootstrap(GlobalConfig cfg, FeatureDatabase db, LevelConfig level)
        {
            // --- 系统根（常驻）---
            var root = new GameObject("AnchorHorror");
            var sanity = root.AddComponent<SanitySystem>();
            var interaction = root.AddComponent<InteractionSystem>();
            var gm = root.AddComponent<GameManager>();
            var feedback = root.AddComponent<SanityFeedback>();
            var shake = root.AddComponent<CameraShake2D>();
            root.AddComponent<MatchFeedback>();
            var memoryPanel = root.AddComponent<MemoryPanel>();
            var resultScreen = root.AddComponent<ResultScreen>();
            var countdown = root.AddComponent<CountdownPanel>();
            var tutorial = root.AddComponent<TutorialPanel>();
            var hud = root.AddComponent<DebugHUD>();
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var whisper = root.AddComponent<AudioSource>();
            whisper.playOnAwake = false;
            var noise = root.AddComponent<AudioSource>();
            noise.playOnAwake = false;

            // --- 玩家 ---
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            player.AddComponent<BoxCollider2D>();
            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = _squareSprite;
            sr.color = Color.cyan;
            sr.sortingOrder = 10; // 玩家压在物品之上
            var pc = player.AddComponent<PlayerController2D>();
            player.AddComponent<PlayerJitter2D>(); // 低 San 手抖（缩放微颤，_target 缺省用自身）

            // --- InitRoom 候选物品（散布，供收集）---
            SpawnItem("Item_A", new Vector2(-2, 1), FeatureColor.Red, FeatureShape.Round, FeatureMaterial.Wood, FeatureTexture.Smooth);
            SpawnItem("Item_B", new Vector2(2, 1), FeatureColor.Blue, FeatureShape.Square, FeatureMaterial.Metal, FeatureTexture.Rough);
            SpawnItem("Item_C", new Vector2(-2, -1), FeatureColor.Green, FeatureShape.Long, FeatureMaterial.Glass, FeatureTexture.Glossy);
            SpawnItem("Item_D", new Vector2(2, -1), FeatureColor.Yellow, FeatureShape.Flat, FeatureMaterial.Fabric, FeatureTexture.Matte);

            // --- 相机（挂在 CameraRig 下：Rig 跟随玩家、相机本身受 Shake 局部抖动，两者互不抢 transform）---
            var cameraRig = new GameObject("CameraRig");
            var camGo = new GameObject("Main Camera");
            camGo.transform.SetParent(cameraRig.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGo.transform.localPosition = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";

            var camFollow = cameraRig.AddComponent<CameraFollow2D>();

            // --- 全屏黑遮罩（San 压暗）；层级由 sortingOrder=1000 决定，z 偏移仅为落在相机近裁剪面内可见 ---
            var overlay = new GameObject("DarkOverlay");
            overlay.transform.SetParent(camGo.transform, false);
            overlay.transform.localPosition = new Vector3(0f, 0f, 1f);
            overlay.transform.localScale = new Vector3(40f, 30f, 1f);
            var osr = overlay.AddComponent<SpriteRenderer>();
            osr.sprite = _squareSprite;
            osr.color = new Color(0f, 0f, 0f, 0f);
            osr.sortingOrder = 1000;

            // --- 过渡黑屏遮罩（sortingOrder 2000 > San 遮罩 1000，加载关卡时盖住一切；z 偏移仅为落在相机近裁剪面内可见，不影响层级排序）---
            var trans = new GameObject("TransitionOverlay");
            trans.transform.SetParent(camGo.transform, false);
            trans.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            trans.transform.localScale = new Vector3(40f, 30f, 1f);
            var tsr = trans.AddComponent<SpriteRenderer>();
            tsr.sprite = _squareSprite;
            tsr.color = new Color(0f, 0f, 0f, 0f);
            tsr.sortingOrder = 2000;

            // --- 世界空间操作提示（TMP，借 CJK fallback 显中文，兼作可见性验证）---
            var hint = new GameObject("HintText");
            hint.transform.position = new Vector3(0f, -4.3f, 0f);
            var htmp = hint.AddComponent<TextMeshPro>();
            htmp.text = "WASD 移动    E 拾取/选择    R 检视(听声音/看信息)    Tab 记忆面板";
            htmp.fontSize = 2.2f;
            htmp.alignment = TextAlignmentOptions.Center;
            htmp.color = new Color(1f, 1f, 1f, 0.72f);
            var hmr = hint.GetComponent<MeshRenderer>();
            if (hmr != null)
            {
                hmr.sortingLayerName = "Default";
                hmr.sortingOrder = 1500;
            }

            // --- 接线（私有 [SerializeField] 用 SerializedObject）---
            WireObj(gm, "_config", cfg);
            WireObj(gm, "_database", db);
            WireObj(gm, "_levelConfig", level);
            WireObj(gm, "_sanity", sanity);
            WireObj(gm, "_interaction", interaction);
            WireObj(gm, "_player", pc);
            WireObj(gm, "_tutorial", tutorial);

            WireObj(interaction, "_player", player.transform);
            WireObj(hud, "_sanity", sanity);
            WireObj(feedback, "_player", pc);
            WireObj(feedback, "_config", cfg);
            WireObj(feedback, "_darkOverlay", osr);
            WireObj(feedback, "_heartbeat", audio);
            WireObj(feedback, "_noiseSource", noise);
            WireObj(gm, "_transitionOverlay", tsr);
            WireObj(gm, "_whisperSource", whisper);
            WireObj(shake, "_camera", camGo.transform);
            WireObj(camFollow, "_target", player.transform);
            WireObj(camFollow, "_cam", cam);
            // MatchFeedback._font 留空：FloatingText 的 TextMeshPro 会自动用 TMP 默认字体(LiberationSans)

            // --- uGUI 界面（记忆面板 + 结算界面 + 倒计时面板），挂在常驻 root 下，随 GameManager 跨场景常驻 ---
            BuildAndWireUi(root.transform, memoryPanel, resultScreen, countdown, tutorial);
        }

        // 构建共享的 Screen Space Overlay Canvas，内含记忆面板、结算界面与倒计时面板（初始均隐藏），并接线到组件。
        // 无按钮交互（Tab / R / Esc 走键盘），故不加 GraphicRaycaster / EventSystem。
        private static void BuildAndWireUi(Transform parent, MemoryPanel memory, ResultScreen result, CountdownPanel countdown, TutorialPanel tutorial)
        {
            var canvasGo = new GameObject("UICanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // --- 记忆面板：居中的半透明暗色盒，内含 richText 内容 ---
            var memRoot = NewUiNode(canvas.transform, "MemoryPanelRoot");
            memRoot.anchorMin = new Vector2(0.5f, 0.5f);
            memRoot.anchorMax = new Vector2(0.5f, 0.5f);
            memRoot.pivot = new Vector2(0.5f, 0.5f);
            memRoot.anchoredPosition = Vector2.zero;
            memRoot.sizeDelta = new Vector2(760f, 620f);
            var memBg = memRoot.gameObject.AddComponent<Image>();
            memBg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            memBg.raycastTarget = false;

            var memContent = CreateText(memRoot, "MemoryContent", 32f, TextAlignmentOptions.TopLeft);
            var memContentRt = (RectTransform)memContent.transform;
            StretchFull(memContentRt);
            memContentRt.offsetMin = new Vector2(40f, 40f);
            memContentRt.offsetMax = new Vector2(-40f, -40f);
            memContent.enableWordWrapping = true;
            memRoot.gameObject.SetActive(false);

            // --- 结算界面：全屏压暗 + 大标题 + 操作提示 ---
            var resultRoot = NewUiNode(canvas.transform, "ResultRoot");
            StretchFull(resultRoot);
            var dim = resultRoot.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = false;

            var title = CreateText(resultRoot, "ResultTitle", 96f, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.text = "已 通 关";
            var titleRt = (RectTransform)title.transform;
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(1f, 0.5f);
            titleRt.pivot = new Vector2(0.5f, 0.5f);
            titleRt.sizeDelta = new Vector2(0f, 180f);
            titleRt.anchoredPosition = new Vector2(0f, 80f);

            var hint = CreateText(resultRoot, "ResultHint", 36f, TextAlignmentOptions.Center);
            hint.text = "按 R 重新开始      按 Esc 返回主菜单";
            var hintRt = (RectTransform)hint.transform;
            hintRt.anchorMin = new Vector2(0f, 0.5f);
            hintRt.anchorMax = new Vector2(1f, 0.5f);
            hintRt.pivot = new Vector2(0.5f, 0.5f);
            hintRt.sizeDelta = new Vector2(0f, 60f);
            hintRt.anchoredPosition = new Vector2(0f, -60f);
            resultRoot.gameObject.SetActive(false);

            WireObj(memory, "_root", memRoot.gameObject);
            WireObj(memory, "_content", memContent);
            WireObj(result, "_root", resultRoot.gameObject);
            WireObj(result, "_title", title);
            WireObj(result, "_hint", hint);

            // --- 倒计时面板：右上角固定，HorrorLevel 相位由 CountdownPanel 自动显/隐 ---
            var countdownRoot = NewUiNode(canvas.transform, "CountdownRoot");
            countdownRoot.anchorMin = new Vector2(1f, 1f);
            countdownRoot.anchorMax = new Vector2(1f, 1f);
            countdownRoot.pivot = new Vector2(1f, 1f);
            countdownRoot.anchoredPosition = new Vector2(-40f, -40f);
            countdownRoot.sizeDelta = new Vector2(260f, 80f);
            var countdownBg = countdownRoot.gameObject.AddComponent<Image>();
            countdownBg.color = new Color(0f, 0f, 0f, 0.55f);
            countdownBg.raycastTarget = false;

            var countdownText = CreateText(countdownRoot, "CountdownText", 52f, TextAlignmentOptions.Center);
            var countdownTextRt = (RectTransform)countdownText.transform;
            StretchFull(countdownTextRt);
            countdownTextRt.offsetMin = new Vector2(12f, 8f);
            countdownTextRt.offsetMax = new Vector2(-12f, -8f);
            countdownText.text = "03:00";
            countdownRoot.gameObject.SetActive(false); // 初始隐藏，HorrorLevel 相位才显示

            WireObj(countdown, "_root", countdownRoot.gameObject);
            WireObj(countdown, "_timeText", countdownText);

            // --- 教程图盖屏（迭代B，占位）：全屏灰底 + 占位图框 + 提示，任意键继续 ---
            var tutorialRoot = NewUiNode(canvas.transform, "TutorialRoot");
            StretchFull(tutorialRoot);
            var tutorialDim = tutorialRoot.gameObject.AddComponent<Image>();
            tutorialDim.color = new Color(0.03f, 0.03f, 0.05f, 1f);
            tutorialDim.raycastTarget = false;

            var tutorialImageRt = NewUiNode(tutorialRoot, "TutorialImage");
            tutorialImageRt.anchorMin = new Vector2(0.5f, 0.5f);
            tutorialImageRt.anchorMax = new Vector2(0.5f, 0.5f);
            tutorialImageRt.pivot = new Vector2(0.5f, 0.5f);
            tutorialImageRt.anchoredPosition = new Vector2(0f, 60f);
            tutorialImageRt.sizeDelta = new Vector2(900f, 520f);
            var tutorialImg = tutorialImageRt.gameObject.AddComponent<Image>();
            tutorialImg.color = new Color(0.5f, 0.5f, 0.5f, 1f); // 占位灰图（缺美术资源，可替换真教程图）
            tutorialImg.raycastTarget = false;

            var tutorialLabel = CreateText(tutorialImageRt, "TutorialLabel", 44f, TextAlignmentOptions.Center);
            tutorialLabel.text = "教程（占位）";

            var tutorialPrompt = CreateText(tutorialRoot, "TutorialPrompt", 40f, TextAlignmentOptions.Center);
            tutorialPrompt.text = "按任意键继续";
            var tutorialPromptRt = (RectTransform)tutorialPrompt.transform;
            tutorialPromptRt.anchorMin = new Vector2(0f, 0f);
            tutorialPromptRt.anchorMax = new Vector2(1f, 0f);
            tutorialPromptRt.pivot = new Vector2(0.5f, 0f);
            tutorialPromptRt.sizeDelta = new Vector2(0f, 80f);
            tutorialPromptRt.anchoredPosition = new Vector2(0f, 120f);
            tutorialRoot.gameObject.SetActive(false); // Show() 时激活

            WireObj(tutorial, "_root", tutorialRoot.gameObject);
            WireObj(tutorial, "_image", tutorialImg);
            WireObj(tutorial, "_prompt", tutorialPrompt);
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

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, float fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void PopulateHorrorLevel()
        {
            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = new Vector3(0, -3, 0);

            // 恐怖关卡散布物品（含能匹配上文候选特征的物品，供联调通关）
            SpawnItem("H_Red1", new Vector2(-3, 2), FeatureColor.Red, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None);
            SpawnItem("H_Red2", new Vector2(-1, 2), FeatureColor.Red, FeatureShape.Round, FeatureMaterial.None, FeatureTexture.None);
            SpawnItem("H_Blue", new Vector2(1, 2), FeatureColor.Blue, FeatureShape.Square, FeatureMaterial.Metal, FeatureTexture.None);
            SpawnItem("H_Wood", new Vector2(3, 2), FeatureColor.Brown, FeatureShape.None, FeatureMaterial.Wood, FeatureTexture.None);
            SpawnItem("H_Glass", new Vector2(-3, 0), FeatureColor.White, FeatureShape.Long, FeatureMaterial.Glass, FeatureTexture.Glossy);
            SpawnItem("H_Fabric", new Vector2(-1, 0), FeatureColor.Yellow, FeatureShape.Flat, FeatureMaterial.Fabric, FeatureTexture.Matte);
            SpawnItem("H_Smooth", new Vector2(1, 0), FeatureColor.Green, FeatureShape.Round, FeatureMaterial.Ceramic, FeatureTexture.Smooth);
            SpawnItem("H_Rough", new Vector2(3, 0), FeatureColor.Black, FeatureShape.Irregular, FeatureMaterial.Metal, FeatureTexture.Rough);
        }

        private static void SpawnItem(string name, Vector2 pos, FeatureColor c, FeatureShape s, FeatureMaterial m, FeatureTexture t)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<BoxCollider2D>();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _squareSprite;
            sr.color = Color.white;
            var tag = go.AddComponent<FeatureTag>();
            WireEnum(tag, "_color", (int)c);
            WireEnum(tag, "_shape", (int)s);
            WireEnum(tag, "_material", (int)m);
            WireEnum(tag, "_texture", (int)t);
        }

        private static void AddScenesToBuildSettings()
        {
            // 剔除文件已不存在的失效条目（如早被删除的 SampleScene 残留引用），再补上本玩法场景。
            var list = EditorBuildSettings.scenes
                .Where(sc => !string.IsNullOrEmpty(sc.path) && System.IO.File.Exists(sc.path))
                .ToList();
            EnsureScene(list, BootstrapScene);
            EnsureScene(list, HorrorScene);
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void EnsureScene(List<EditorBuildSettingsScene> list, string path)
        {
            if (!list.Any(sc => sc.path == path))
            {
                list.Add(new EditorBuildSettingsScene(path, true));
            }
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

        // 导入 TMP Essential Resources（自带 LiberationSans 通用字体 + TMP 着色器）。
        // TMP 文本不指定字体时自动用它；MemoryPanel 的 TMP_Text 也依赖它才能渲染。
        private static void EnsureTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                return;
            }

            string pkg = FindEssentialsPackage();
            if (pkg == null)
            {
                Debug.LogWarning("[AnchorHorror] 找不到 TMP Essential Resources 包，文本将不可见。可手动 Window/TextMeshPro/Import TMP Essential Resources。");
                return;
            }

            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
        }

        private static string FindEssentialsPackage()
        {
            const string cache = "Library/PackageCache";
            if (!System.IO.Directory.Exists(cache))
            {
                return null;
            }

            foreach (var dir in System.IO.Directory.GetDirectories(cache))
            {
                if (System.IO.Path.GetFileName(dir).StartsWith("com.unity.textmeshpro"))
                {
                    string p = dir + "/Package Resources/TMP Essential Resources.unitypackage";
                    if (System.IO.File.Exists(p))
                    {
                        return p;
                    }
                }
            }

            return null;
        }

        // 用中文特征名 + 关键词颜色填 FeatureDatabase（仅在空库时，避免覆盖手动编辑）。
        private static void PopulateFeatureDatabase(FeatureDatabase db)
        {
            var so = new SerializedObject(db);
            var list = so.FindProperty("_entries");
            if (list == null || list.arraySize > 0)
            {
                return;
            }

            var gold = new Color(1f, 0.9f, 0.62f);
            var entries = new (FeatureDimension dim, int val, string name, Color color)[]
            {
                (FeatureDimension.Color, (int)FeatureColor.Red, "红色", new Color(1f, 0.42f, 0.42f)),
                (FeatureDimension.Color, (int)FeatureColor.Blue, "蓝色", new Color(0.45f, 0.6f, 1f)),
                (FeatureDimension.Color, (int)FeatureColor.Green, "绿色", new Color(0.5f, 0.9f, 0.55f)),
                (FeatureDimension.Color, (int)FeatureColor.Yellow, "黄色", new Color(1f, 0.9f, 0.4f)),
                (FeatureDimension.Color, (int)FeatureColor.White, "白色", Color.white),
                (FeatureDimension.Color, (int)FeatureColor.Black, "黑色", new Color(0.7f, 0.7f, 0.75f)),
                (FeatureDimension.Color, (int)FeatureColor.Brown, "棕色", new Color(0.8f, 0.55f, 0.35f)),
                (FeatureDimension.Shape, (int)FeatureShape.Round, "圆形", gold),
                (FeatureDimension.Shape, (int)FeatureShape.Square, "方形", gold),
                (FeatureDimension.Shape, (int)FeatureShape.Long, "长条", gold),
                (FeatureDimension.Shape, (int)FeatureShape.Flat, "扁平", gold),
                (FeatureDimension.Shape, (int)FeatureShape.Irregular, "不规则", gold),
                (FeatureDimension.Material, (int)FeatureMaterial.Wood, "木质", gold),
                (FeatureDimension.Material, (int)FeatureMaterial.Metal, "金属", gold),
                (FeatureDimension.Material, (int)FeatureMaterial.Glass, "玻璃", gold),
                (FeatureDimension.Material, (int)FeatureMaterial.Fabric, "布料", gold),
                (FeatureDimension.Material, (int)FeatureMaterial.Paper, "纸质", gold),
                (FeatureDimension.Material, (int)FeatureMaterial.Ceramic, "陶瓷", gold),
                (FeatureDimension.Texture, (int)FeatureTexture.Smooth, "光滑", gold),
                (FeatureDimension.Texture, (int)FeatureTexture.Rough, "粗糙", gold),
                (FeatureDimension.Texture, (int)FeatureTexture.Glossy, "有光泽", gold),
                (FeatureDimension.Texture, (int)FeatureTexture.Matte, "哑光", gold),
                (FeatureDimension.Texture, (int)FeatureTexture.Patterned, "有纹路", gold),
            };

            for (int i = 0; i < entries.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("_dimension").enumValueIndex = (int)entries[i].dim;
                e.FindPropertyRelative("_value").intValue = entries[i].val;
                e.FindPropertyRelative("_displayName").stringValue = entries[i].name;
                e.FindPropertyRelative("_keywordColor").colorValue = entries[i].color;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
        }

        // 造一个中文黑体的动态 TMP 字体并加入 TMP 全局 fallback，使所有 TMP 文本缺中文字形时自动回退。
        private static void EnsureCjkFallback()
        {
            var cjk = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CjkFontPath);
            if (cjk == null)
            {
                if (!System.IO.File.Exists(CjkTtfPath))
                {
                    string[] candidates =
                    {
                        "C:/Windows/Fonts/simhei.ttf",
                        "C:/Windows/Fonts/Deng.ttf",
                        "C:/Windows/Fonts/STXIHEI.TTF",
                    };
                    string sys = candidates.FirstOrDefault(System.IO.File.Exists);
                    if (sys == null)
                    {
                        Debug.LogWarning("[AnchorHorror] 找不到系统中文字体，中文字符将缺字。");
                        return;
                    }

                    System.IO.File.Copy(sys, CjkTtfPath);
                    AssetDatabase.ImportAsset(CjkTtfPath);
                }

                var src = AssetDatabase.LoadAssetAtPath<Font>(CjkTtfPath);
                if (src == null)
                {
                    Debug.LogWarning("[AnchorHorror] 中文字体 .ttf 导入失败。");
                    return;
                }

                cjk = TMP_FontAsset.CreateFontAsset(src); // 默认 Dynamic 模式，运行时按需生成中文字形
                if (cjk == null)
                {
                    Debug.LogWarning("[AnchorHorror] CJK TMP 字体创建失败。");
                    return;
                }

                cjk.name = "AnchorCJK SDF";
                AssetDatabase.CreateAsset(cjk, CjkFontPath);
                if (cjk.material != null)
                {
                    cjk.material.name = "AnchorCJK SDF Material";
                    AssetDatabase.AddObjectToAsset(cjk.material, cjk);
                }

                if (cjk.atlasTexture != null)
                {
                    cjk.atlasTexture.name = "AnchorCJK SDF Atlas";
                    AssetDatabase.AddObjectToAsset(cjk.atlasTexture, cjk);
                }

                AssetDatabase.SaveAssets();
            }

            AddToTmpGlobalFallback(cjk);
        }

        // 把工程实际用到的中文（面板/结算/世界提示 + 特征名）预烤进 CJK 字体图集并落盘。
        // CJK 原为 Dynamic + 空烤字形，靠运行时/编辑器动态生成，编辑态重导入时机偶尔闪品红；
        // 预烤后编辑态与运行时都稳定显示，不再依赖动态生成时机。
        private static void BakeCjkGlyphs(FeatureDatabase db)
        {
            var cjk = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CjkFontPath);
            if (cjk == null)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append("记忆锚点已锚定已通关理智崩溃按重新开始返回主菜单移动交互物品记忆面板"); // UI 固定文案
            if (db != null) // 特征名从 DB 取，避免与 PopulateFeatureDatabase 硬编码漂移
            {
                var so = new SerializedObject(db);
                var list = so.FindProperty("_entries");
                if (list != null)
                {
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        var name = list.GetArrayElementAtIndex(i).FindPropertyRelative("_displayName").stringValue;
                        if (!string.IsNullOrEmpty(name))
                        {
                            sb.Append(name);
                        }
                    }
                }
            }

            try
            {
                if (cjk.TryAddCharacters(sb.ToString(), out string missing))
                {
                    Debug.Log("[AnchorHorror] CJK 字形已预烤进图集。");
                }
                else
                {
                    Debug.LogWarning($"[AnchorHorror] CJK 预烤部分缺字（图集可能已满，需增大 Atlas 尺寸）：{missing}");
                }

                EditorUtility.SetDirty(cjk);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                // 预烤仅为编辑态优化（防品红闪）；失败不影响运行时——CJK 是 Dynamic 字体，运行时按需生成字形照样显示。
                // 大字形集（如配置表全量特征名）会触发 TMP 多图集增长，在编辑脚本上下文偶发 m_AtlasTextures 引用失效；降级为告警，不中断 BuildAll。
                Debug.LogWarning($"[AnchorHorror] CJK 预烤跳过（{ex.GetType().Name}）：运行时动态字体仍会显示中文。如需消除编辑态品红闪，手动增大 AnchorCJK SDF 图集尺寸后重烤。");
            }
        }

        private static void AddToTmpGlobalFallback(TMP_FontAsset cjk)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning("[AnchorHorror] 找不到 TMP Settings，无法配置中文 fallback。");
                return;
            }

            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_fallbackFontAssets");
            if (list == null)
            {
                Debug.LogWarning("[AnchorHorror] TMP Settings 无 m_fallbackFontAssets 字段。");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == cjk)
                {
                    return; // 已在 fallback 列表
                }
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = cjk;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static Sprite GetOrCreateSquareSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (existing != null)
            {
                return existing;
            }

            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color32[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(SquareSpritePath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(SquareSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SquareSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32f;   // 32px = 1 世界单位
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void WireObj(Component c, string prop, UnityEngine.Object value)
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
                Debug.LogWarning($"[AnchorHorror] 接线失败：{c.GetType().Name} 无属性 {prop}");
            }
        }

        private static void WireStr(Component c, string prop, string value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p != null)
            {
                p.stringValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void WireEnum(Component c, string prop, int enumIndex)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p != null)
            {
                p.enumValueIndex = enumIndex;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
