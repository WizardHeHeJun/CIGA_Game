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
using UnityEngine.EventSystems;
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
        private const string GameFontTtfPath = SoDir + "/HanyiLotus.ttf";
        private const string GameFontPath = SoDir + "/HanyiLotus SDF.asset";
        private const string LiberationFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // 关卡2 HUD 美术：从 acts/ 原始美术拷入 Assets 并导为 Sprite（全屏叠加层，按 1920x1080 定位）
        private const string InGameUiDir = "Assets/Res/UI/InGame";
        private static readonly string[] UiSrcParts = { "acts", "ciga美术资产", "ciga美术资产", "ui" };

        private static Sprite _squareSprite;

        // 关卡2 HUD sprite（由 EnsureInGameHudSprites 填充；缺图为 null，运行时判空/透明降级）
        private static Sprite _frameSprite;
        private static Sprite _bagSprite;
        private static Sprite _memorySprite;
        private static Sprite _noCollectSprite;
        private static Sprite[] _collectedSprites;
        private static Sprite _sanFrameSprite;
        private static Sprite _sanFillSprite;

        // 游戏主字体（汉仪新蒂莲花体），设为 TMP 默认字体；缺失时为 null（沿用默认）
        private static TMPro.TMP_FontAsset _gameFont;

        [MenuItem("Ciga/AnchorHorror/生成可运行装配")]
        public static void BuildAll()
        {
            EnsureFolder(SoDir);

            var cfg = CreateOrLoad<GlobalConfig>(SoDir + "/GlobalConfig.asset");
            var db = CreateOrLoad<FeatureDatabase>(SoDir + "/FeatureDatabase.asset");
            var level = CreateOrLoad<LevelConfig>(SoDir + "/LevelConfig.asset");
            PopulateFeatureDatabase(db);               // 填中文特征名/关键词颜色（空库时）
            _squareSprite = GetOrCreateSquareSprite(); // 玩家/物品可见所需的方块 sprite
            EnsureInGameHudSprites();                   // 关卡2 HUD 美术（边框/背包/记忆石板/命中数/San 条）拷入并导为 Sprite
            EnsureTmpEssentials();                     // 保证 TMP 通用字体(LiberationSans)+着色器可用（浮字/面板文本）
            EnsureCjkFallback();                       // 中文字形回退（黑体动态字体加入 TMP 全局 fallback）
            EnsureGameFont();                          // 游戏主字体（汉仪新蒂莲花体）设为 TMP 默认字体（全局主字体）
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
            var inGameHud = root.AddComponent<InGameHudPanel>(); // 边框 + 顶部命中数（HorrorLevel）
            var sanBar = root.AddComponent<SanBarPanel>();        // San 条（HorrorLevel）
            var backpack = root.AddComponent<BackpackPanel>();    // 右侧背包（HorrorLevel）
            var hud = root.AddComponent<DebugHUD>();
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var whisper = root.AddComponent<AudioSource>();
            whisper.playOnAwake = false;
            var noise = root.AddComponent<AudioSource>();
            noise.playOnAwake = false;

            // 三态结算配置（修 bug：之前没建 ResultConfig，导致 ResultScreen 文案不更新、失败也显"已通关"）
            var resultCfg = BuildResultConfig();

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
            if (_gameFont != null)
            {
                htmp.font = _gameFont;
            }

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
            WireObj(gm, "_cameraFollow", camFollow); // 背景边界/镜头 clamp 需要持有 CameraRig 跟随组件
            WireObj(shake, "_camera", camGo.transform);
            WireObj(camFollow, "_target", player.transform);
            WireObj(camFollow, "_cam", cam);
            WireObj(resultScreen, "_resultConfig", resultCfg);
            // MatchFeedback._font 留空：FloatingText 的 TextMeshPro 会自动用 TMP 默认字体(LiberationSans)

            // --- uGUI 界面（记忆页 + 结算 + 倒计时 + 关卡2 HUD），挂在常驻 root 下，随 GameManager 跨场景常驻 ---
            BuildAndWireUi(root.transform, memoryPanel, resultScreen, countdown, tutorial, inGameHud, sanBar, backpack);
        }

        // 构建共享的 Screen Space Overlay Canvas，内含记忆面板、结算界面与倒计时面板（初始均隐藏），并接线到组件。
        // 结算界面的胜/负图层按钮需要点击，故加 GraphicRaycaster 并确保场景有 EventSystem
        // （ResultScreen 运行时也会自愈补上，这里烘进场景使其无需自愈即可点击）。
        private static void BuildAndWireUi(Transform parent, MemoryPanel memory, ResultScreen result, CountdownPanel countdown, TutorialPanel tutorial,
            InGameHudPanel inGameHud, SanBarPanel sanBar, BackpackPanel backpack)
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
            canvasGo.AddComponent<GraphicRaycaster>(); // 结算图层按钮需要射线命中

            // 场景无 EventSystem 则建一个（uGUI 点击必需；用 StandaloneInputModule 走旧版 Input）
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            // 关卡2 HUD 三层（边框+命中数 / San 条 / 背包）先建：作为 memory/result/countdown/tutorial 的底层，
            // 被记忆页与结算图覆盖。均全屏叠加、初始隐藏，各自组件按 HorrorLevel 相位显隐。
            BuildInGameHud(canvas.transform, inGameHud);
            BuildSanBar(canvas.transform, sanBar);
            BuildBackpack(canvas.transform, backpack);

            // --- 记忆页签：全屏暗底 + Memory 石板 + 5 椭圆槽锚点标签 ---
            var memRoot = NewUiNode(canvas.transform, "MemoryPanelRoot");
            StretchFull(memRoot);
            var memDim = memRoot.gameObject.AddComponent<Image>();
            memDim.color = new Color(0f, 0f, 0f, 0.55f);
            memDim.raycastTarget = false;
            AddFullScreenImage(memRoot, "MemoryTablet", _memorySprite);

            // 5 椭圆槽中心（顶左像素，实测 Memory.PNG）：TL TR C BL BR
            var ovalCenters = new[]
            {
                new Vector2(600f, 410f), new Vector2(1130f, 410f),
                new Vector2(875f, 515f), new Vector2(600f, 640f), new Vector2(1120f, 640f),
            };
            var anchorLabels = new TMP_Text[ovalCenters.Length];
            for (int i = 0; i < ovalCenters.Length; i++)
            {
                var lbl = CreateText(memRoot, "AnchorLabel" + i, 34f, TextAlignmentOptions.Center);
                lbl.fontStyle = FontStyles.Bold;
                var lrt = (RectTransform)lbl.transform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.zero;
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = new Vector2(ovalCenters[i].x, 1080f - ovalCenters[i].y); // 顶左像素 → 画布底左
                lrt.sizeDelta = new Vector2(240f, 70f);
                anchorLabels[i] = lbl;
            }

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
            WireObjArray(memory, "_anchorLabels", anchorLabels);
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

            // --- 教程图盖屏（迭代B）：全屏教程图 + 提示，任意键继续 ---
            var tutorialRoot = NewUiNode(canvas.transform, "TutorialRoot");
            StretchFull(tutorialRoot);
            var tutorialDim = tutorialRoot.gameObject.AddComponent<Image>();
            tutorialDim.color = new Color(0.03f, 0.03f, 0.05f, 1f);
            tutorialDim.raycastTarget = false;

            var tutorialImageRt = NewUiNode(tutorialRoot, "TutorialImage");
            StretchFull(tutorialImageRt);
            var tutorialImg = tutorialImageRt.gameObject.AddComponent<Image>();
            tutorialImg.raycastTarget = false;
            tutorialImg.preserveAspect = true;
            var tutorialSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Res/UI/Education.png");
            if (tutorialSprite != null)
            {
                tutorialImg.sprite = tutorialSprite;
                tutorialImg.color = Color.white;
            }
            else
            {
                tutorialImg.color = new Color(0.5f, 0.5f, 0.5f, 1f); // 缺美术资源时灰底占位
            }

            var tutorialLabel = CreateText(tutorialImageRt, "TutorialLabel", 44f, TextAlignmentOptions.Center);
            tutorialLabel.text = tutorialSprite != null ? string.Empty : "教程（占位）";

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

        // ────────────────────────────────────────────────────────────
        //  关卡2 HUD 构建（边框 / San 条 / 背包）——全屏叠加层，初始隐藏，各组件按 HorrorLevel 相位显隐
        // ────────────────────────────────────────────────────────────

        // 边框（Frame）+ 顶部命中数（No Collect 底 + Collected1..5 叠加）。
        private static void BuildInGameHud(Transform canvas, InGameHudPanel inGameHud)
        {
            var hudRoot = NewUiNode(canvas, "InGameHudRoot");
            StretchFull(hudRoot);
            AddFullScreenImage(hudRoot, "Frame", _frameSprite);
            AddFullScreenImage(hudRoot, "HitCountBase", _noCollectSprite);

            var collectedIcons = new Image[5];
            for (int i = 0; i < collectedIcons.Length; i++)
            {
                Sprite s = _collectedSprites != null && i < _collectedSprites.Length ? _collectedSprites[i] : null;
                var img = AddFullScreenImage(hudRoot, "Collected" + (i + 1), s);
                img.enabled = false; // 命中后由 InGameHudPanel 逐个点亮
                collectedIcons[i] = img;
            }

            hudRoot.gameObject.SetActive(false);
            WireObj(inGameHud, "_root", hudRoot.gameObject);
            WireObjArray(inGameHud, "_collectedIcons", collectedIcons);
        }

        // San 条：san frame 外框（全屏）+ san 填充（裁剪图放到条形位置，Image.Filled 水平从左）。
        private static void BuildSanBar(Transform canvas, SanBarPanel sanBar)
        {
            var sanRoot = NewUiNode(canvas, "SanBarRoot");
            StretchFull(sanRoot);
            AddFullScreenImage(sanRoot, "SanFrame", _sanFrameSprite);

            // San 填充条形区域（画布底左原点）：x=254, y=30, 宽=1192, 高=38（实测 san.PNG 填充 bbox）
            var fillRt = NewUiNode(sanRoot, "SanFill");
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.zero;
            fillRt.pivot = Vector2.zero;
            fillRt.anchoredPosition = new Vector2(254f, 30f);
            fillRt.sizeDelta = new Vector2(1192f, 38f);
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.sprite = _sanFillSprite;
            fillImg.color = _sanFillSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            fillImg.raycastTarget = false;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;

            sanRoot.gameObject.SetActive(false);
            WireObj(sanBar, "_root", sanRoot.gameObject);
            WireObj(sanBar, "_fill", fillImg);
        }

        // 右侧背包：Bag 羊皮纸（全屏）+ 4 个物品槽图标 + 溢出 +N 角标。
        private static void BuildBackpack(Transform canvas, BackpackPanel backpack)
        {
            var bagRoot = NewUiNode(canvas, "BackpackRoot");
            StretchFull(bagRoot);
            AddFullScreenImage(bagRoot, "Bag", _bagSprite);

            // 4 槽中心（顶左像素，实测 Bag.PNG）：x≈1745，y=264/426/618/774
            float[] slotTopY = { 264f, 426f, 618f, 774f };
            var slots = new Image[slotTopY.Length];
            for (int i = 0; i < slotTopY.Length; i++)
            {
                var srt = NewUiNode(bagRoot, "Slot" + i);
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.zero;
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = new Vector2(1745f, 1080f - slotTopY[i]); // 顶左像素 → 画布底左
                srt.sizeDelta = new Vector2(110f, 110f);
                var img = srt.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                img.enabled = false; // 有物品时由 BackpackPanel 填充
                slots[i] = img;
            }

            // 溢出角标 +N（4 槽下方）
            var overflow = CreateText(bagRoot, "BackpackOverflow", 34f, TextAlignmentOptions.Center);
            overflow.color = new Color(1f, 0.9f, 0.6f);
            var ort = (RectTransform)overflow.transform;
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.zero;
            ort.pivot = new Vector2(0.5f, 0.5f);
            ort.anchoredPosition = new Vector2(1745f, 1080f - 872f);
            ort.sizeDelta = new Vector2(160f, 50f);
            overflow.enabled = false;

            bagRoot.gameObject.SetActive(false);
            WireObj(backpack, "_root", bagRoot.gameObject);
            WireObjArray(backpack, "_slots", slots);
            WireObj(backpack, "_overflow", overflow);
        }

        private static Image AddFullScreenImage(RectTransform parent, string name, Sprite sprite)
        {
            var rt = NewUiNode(parent, name);
            StretchFull(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f); // 缺图透明降级
            img.raycastTarget = false;
            return img;
        }

        // ────────────────────────────────────────────────────────────
        //  关卡2 HUD 美术导入（从 acts/ 拷入 Assets 并导为 Sprite；缺源图静默透明降级）
        // ────────────────────────────────────────────────────────────

        private static void EnsureInGameHudSprites()
        {
            EnsureFolder(InGameUiDir);
            string uiSrc = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                System.IO.Path.Combine(UiSrcParts));

            _frameSprite = CopyImportUiSprite(System.IO.Path.Combine(uiSrc, "Previous Page", "Frame.PNG"), "Frame.png");
            _bagSprite = CopyImportUiSprite(System.IO.Path.Combine(uiSrc, "Previous Page", "Bag.PNG"), "Bag.png");
            _memorySprite = CopyImportUiSprite(System.IO.Path.Combine(uiSrc, "Memory", "Memory.PNG"), "Memory.png");
            _noCollectSprite = CopyImportUiSprite(System.IO.Path.Combine(uiSrc, "Memory", "No Collect.PNG"), "NoCollect.png");

            _collectedSprites = new Sprite[5];
            for (int i = 0; i < _collectedSprites.Length; i++)
            {
                _collectedSprites[i] = CopyImportUiSprite(
                    System.IO.Path.Combine(uiSrc, "Memory", "Collected" + (i + 1) + ".PNG"),
                    "Collected" + (i + 1) + ".png");
            }

            _sanFrameSprite = CopyImportUiSprite(System.IO.Path.Combine(uiSrc, "Night page", "san frame.PNG"), "SanFrame.png");
            _sanFillSprite = EnsureSanFillSprite(System.IO.Path.Combine(uiSrc, "Night page", "san.PNG"));
        }

        private static Sprite CopyImportUiSprite(string sourceAbs, string targetFile)
        {
            string assetPath = InGameUiDir + "/" + targetFile;
            if (!System.IO.File.Exists(assetPath))
            {
                if (!System.IO.File.Exists(sourceAbs))
                {
                    Debug.LogWarning($"[AnchorHorror] 找不到 UI 美术源：{sourceAbs}（{targetFile} 将缺图，运行时透明降级）。");
                    return null;
                }

                System.IO.File.Copy(sourceAbs, assetPath, true);
                AssetDatabase.ImportAsset(assetPath);
            }

            ConfigureUiSprite(assetPath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void ConfigureUiSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
            {
                importer.spritePixelsPerUnit = 100f;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        // San 填充图：把 san.PNG 裁剪到填充条 bbox 生成 SanFill.png，供 Image.Filled 线性缩减。
        // （全屏图直接 Filled 会按整屏宽度比例裁，而填充条不从 x=0 起，低 San 时会提前消失，故必须裁剪。）
        private static Sprite EnsureSanFillSprite(string sourceAbs)
        {
            string assetPath = InGameUiDir + "/SanFill.png";
            if (!System.IO.File.Exists(assetPath))
            {
                if (!System.IO.File.Exists(sourceAbs))
                {
                    Debug.LogWarning($"[AnchorHorror] 找不到 San 源图：{sourceAbs}（San 填充缺图）。");
                    return null;
                }

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(System.IO.File.ReadAllBytes(sourceAbs)); // 加载后 tex = 源图尺寸(1920x1080)，可读
                const int cropX = 254, cropW = 1192, cropY = 30, cropH = 38; // 顶左 y[1012,1050] → 底左 y0=30 高38
                bool ok = tex.width >= cropX + cropW && tex.height >= cropY + cropH;
                if (ok)
                {
                    var crop = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
                    crop.SetPixels(tex.GetPixels(cropX, cropY, cropW, cropH));
                    crop.Apply();
                    System.IO.File.WriteAllBytes(assetPath, crop.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(crop);
                }

                UnityEngine.Object.DestroyImmediate(tex);
                if (!ok)
                {
                    Debug.LogWarning("[AnchorHorror] San 源图尺寸异常，跳过 SanFill 裁剪。");
                    return null;
                }

                AssetDatabase.ImportAsset(assetPath);
            }

            ConfigureUiSprite(assetPath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void WireObjArray(Component c, string prop, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p == null)
            {
                Debug.LogWarning($"[AnchorHorror] 接线失败（数组）：{c.GetType().Name} 无属性 {prop}");
                return;
            }

            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
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
            if (_gameFont != null)
            {
                tmp.font = _gameFont; // 游戏主字体（汉仪新蒂莲花体）
            }

            return tmp;
        }

        /// <summary>建/更新三态结算配置 ResultConfig（Victory/Fail/SubClear 区分文案，修：之前没建导致失败也显"已通关"）。</summary>
        private static ResultConfig BuildResultConfig()
        {
            var rc = CreateOrLoad<ResultConfig>("Assets/Res/AnchorHorror/ResultConfig.asset");
            var so = new SerializedObject(rc);

            SetResultEntry(so.FindProperty("_subClear"),
                "过 关", new Color(1f, 0.84f, 0.2f), "走到门按 E 前往下一层", false, false);
            SetResultEntry(so.FindProperty("_victory"),
                "已 通 关", new Color(1f, 0.84f, 0.2f), "按 R 重新开始      按 Esc 返回主菜单", true, true);
            SetResultEntry(so.FindProperty("_fail"),
                "失 败", new Color(1f, 0.3f, 0.3f), "按 R 重新开始      按 Esc 返回主菜单", true, true);

            // 美术接线（Victory=win 全屏图 + 返回标题/制作组/退出；Fail=defeat 全屏图 + 重新开始/返回标题/退出）。
            // 素材在 Assets/Res/UI/Result/，按钮图开 Read/Write 供 alpha 笔触命中。
            const string dir = "Assets/Res/UI/Result/";
            SetResultArt(so.FindProperty("_victory"), dir + "VictoryBackground.png",
                new[] { dir + "VictoryMenuButton.png", dir + "CreditsButton.png", dir + "VictoryQuitButton.png" },
                new[] { ResultAction.Menu, ResultAction.Credits, ResultAction.Quit });
            SetResultArt(so.FindProperty("_fail"), dir + "DefeatBackground.png",
                new[] { dir + "RestartButton.png", dir + "DefeatMenuButton.png", dir + "DefeatQuitButton.png" },
                new[] { ResultAction.Restart, ResultAction.Menu, ResultAction.Quit });

            var creditsText = so.FindProperty("_creditsText");
            if (creditsText != null && string.IsNullOrEmpty(creditsText.stringValue))
            {
                creditsText.stringValue = "制作组（名单待补）";
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rc);
            return rc;
        }

        /// <summary>为某结算态接线全屏背景图与图层按钮（图未导入时静默跳过该项，不清已有引用）。</summary>
        private static void SetResultArt(SerializedProperty entry, string bgPath, string[] buttonPaths, ResultAction[] actions)
        {
            if (entry == null)
            {
                return;
            }

            var bg = AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);
            if (bg != null)
            {
                entry.FindPropertyRelative("_background").objectReferenceValue = bg;
            }

            var buttons = entry.FindPropertyRelative("_buttons");
            buttons.arraySize = buttonPaths.Length;
            for (int i = 0; i < buttonPaths.Length; i++)
            {
                var el = buttons.GetArrayElementAtIndex(i);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(buttonPaths[i]);
                if (sprite != null)
                {
                    el.FindPropertyRelative("_sprite").objectReferenceValue = sprite;
                }

                el.FindPropertyRelative("_action").enumValueIndex = (int)actions[i];
            }
        }

        private static void SetResultEntry(
            SerializedProperty entry, string title, Color color, string hint, bool restart, bool menu)
        {
            entry.FindPropertyRelative("_title").stringValue = title;
            entry.FindPropertyRelative("_color").colorValue = color;
            entry.FindPropertyRelative("_hint").stringValue = hint;
            entry.FindPropertyRelative("_respondsRestart").boolValue = restart;
            entry.FindPropertyRelative("_respondsMenu").boolValue = menu;
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

            string chars = sb.ToString();
            BakeGlyphsInto(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameFontPath), chars); // 游戏主字体（汉仪莲花体）
            BakeGlyphsInto(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CjkFontPath), chars);  // CJK 兜底字体
        }

        /// <summary>把指定字形预烤进某动态字体图集并落盘。失败仅告警（运行时动态生成仍显示）。</summary>
        private static void BakeGlyphsInto(TMP_FontAsset font, string chars)
        {
            if (font == null || string.IsNullOrEmpty(chars))
            {
                return;
            }

            try
            {
                if (font.TryAddCharacters(chars, out string missing))
                {
                    Debug.Log($"[AnchorHorror] 字形已预烤进 {font.name} 图集。");
                }
                else
                {
                    Debug.LogWarning($"[AnchorHorror] {font.name} 预烤部分缺字（图集可能已满，需增大 Atlas 尺寸）：{missing}");
                }

                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                // 预烤仅为编辑态优化（防品红闪）；失败不影响运行时——动态字体运行时按需生成字形照样显示。
                Debug.LogWarning($"[AnchorHorror] {font.name} 预烤跳过（{ex.GetType().Name}）：运行时动态字体仍会显示中文。");
            }
        }

        // 游戏主字体（汉仪新蒂莲花体 HanyiSentyLotus）：从 acts/ 拷入并建 Dynamic TMP 字体，
        // 设为 TMP 默认字体（全局主字体）；LiberationSans 挂 fallback（拉丁/数字/符号兜底），
        // AnchorCJK 已由 EnsureCjkFallback 挂 fallback（罕见 CJK 兜底）。
        private static void EnsureGameFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameFontPath);
            if (font == null)
            {
                if (!System.IO.File.Exists(GameFontTtfPath))
                {
                    string src = System.IO.Path.Combine(
                        System.IO.Directory.GetParent(Application.dataPath).FullName,
                        "acts", "ciga美术资产", "ciga美术资产", "HanyiSentyLotus-2.ttf");
                    if (!System.IO.File.Exists(src))
                    {
                        Debug.LogWarning($"[AnchorHorror] 找不到游戏字体源：{src}，沿用默认字体。");
                        return;
                    }

                    System.IO.File.Copy(src, GameFontTtfPath, true);
                    AssetDatabase.ImportAsset(GameFontTtfPath);
                }

                var srcFont = AssetDatabase.LoadAssetAtPath<Font>(GameFontTtfPath);
                if (srcFont == null)
                {
                    Debug.LogWarning("[AnchorHorror] 游戏字体 .ttf 导入失败。");
                    return;
                }

                font = TMP_FontAsset.CreateFontAsset(srcFont); // Dynamic，运行时按需生成字形
                if (font == null)
                {
                    Debug.LogWarning("[AnchorHorror] 游戏字体 TMP 资产创建失败。");
                    return;
                }

                font.name = "HanyiLotus SDF";
                AssetDatabase.CreateAsset(font, GameFontPath);
                if (font.material != null)
                {
                    font.material.name = "HanyiLotus SDF Material";
                    AssetDatabase.AddObjectToAsset(font.material, font);
                }

                if (font.atlasTexture != null)
                {
                    font.atlasTexture.name = "HanyiLotus SDF Atlas";
                    AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
                }

                AssetDatabase.SaveAssets();
            }

            _gameFont = font;
            SetTmpDefaultFont(font); // 设为 TMP 全局默认字体（游戏内所有 TMP 文本主字体）
            EnsureLatinFallback();   // LiberationSans 挂 fallback，拉丁/数字/符号兜底
        }

        // 设 TMP 默认字体资产（TMP Settings 的 m_defaultFontAsset）。
        private static void SetTmpDefaultFont(TMP_FontAsset font)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning("[AnchorHorror] 找不到 TMP Settings，无法设默认字体。");
                return;
            }

            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultFontAsset");
            if (prop == null)
            {
                Debug.LogWarning("[AnchorHorror] TMP Settings 无 m_defaultFontAsset 字段。");
                return;
            }

            if (prop.objectReferenceValue == font)
            {
                return;
            }

            prop.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // LiberationSans SDF 加入 TMP 全局 fallback（拉丁/数字/符号兜底；主字体缺字时回退）。
        private static void EnsureLatinFallback()
        {
            var lib = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFontPath);
            if (lib != null)
            {
                AddToTmpGlobalFallback(lib);
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
