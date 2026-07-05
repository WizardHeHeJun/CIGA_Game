// ------------------------------------------------------------
// ResultScreen.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections;
using Ciga.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 三态结算界面（uGUI + TMP）：订阅 PhaseChanged，SubClear/Victory/Fail 时激活盖屏面板。
    /// 视觉与响应由挂载的 ResultConfig SO 数据驱动（ADR-5）：
    ///   - 配了背景图（Victory=win.PNG / Fail=defeat.PNG）→ 隐藏文字标题/提示，运行时自建全屏
    ///     alpha 命中按钮层（笔触可点、透明处穿透），按钮动作 Restart/Menu/Quit/Credits 数据化；
    ///   - 未配背景图（SubClear）→ 退回纯文字 + 键盘（R/Esc）结算，向后兼容；
    ///   - SubClear：R/Esc 均关闭（只等玩家走门），防止误按重开（陷阱 5）。
    /// 通关前置名单（用户要求）：最终胜利（Victory）先全屏展示制作组名单，维持数秒后任意键关闭，
    /// 关闭后再弹胜利结算；由 ResultConfig.ShowCreditsIntroOnVictory / CreditsIntroHoldSeconds 门控。
    /// 面板结构由 AnchorHorrorSetup 生成并接线；按钮层/背景层/制作组页/GraphicRaycaster/EventSystem
    /// 均运行时自建（AnchorHorror 场景原不含按钮交互，故 Raycaster/EventSystem 缺失时自愈补上）。
    /// 只订阅 EventBus，不被逻辑依赖。
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        private const float AlphaHitThreshold = 0.1f;    // alpha > 0.1 才可点，过滤笔触羽化透明边
        private const float CreditsLockSeconds = 0.25f;  // 制作组页打开后忽略点击的秒数（防同击秒关）

        [Header("UI 引用（生成器接线）")]
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _hint;

        [Header("结算配置（ADR-5）")]
        [SerializeField] private ResultConfig _resultConfig;

        [Header("按键")]
        [SerializeField] private KeyCode _restartKey = KeyCode.R;
        [SerializeField] private KeyCode _menuKey = KeyCode.Escape;

        [Header("过渡")]
        [SerializeField] private float _fadeDuration = 0.4f; // 结算面板淡入时长（#1）

        private GamePhase _phase = GamePhase.Boot;
        private CanvasGroup _canvasGroup;
        private Coroutine _fadeRoutine;

        // 运行时自建，不序列化
        private GameObject _artRoot;         // 背景图 + 按钮层容器（每次进入结算态重建）
        private GameObject _creditsRoot;     // 制作组页
        private CanvasGroup _creditsGroup;
        private TMP_Text _creditsText;
        private bool _creditsShowing;
        private float _creditsUnlockTime;
        private bool _creditsIsIntro;                          // true = 通关前置名单（关闭后接着弹结算）
        private ResultConfig.ResultEntry _pendingResultEntry; // intro 关闭后待展示的结算数据

        private void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        private void Start()
        {
            // 初始隐藏；即便生成器未接线（_root 为空）也安全。
            ApplyVisual();
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            _phase = newPhase;
            ApplyVisual();
        }

        private void Update()
        {
            // 制作组页开启期间：吞掉其它输入，维持时长内不响应；解锁后任意键（含鼠标）关闭。
            if (_creditsShowing)
            {
                if (Time.unscaledTime >= _creditsUnlockTime && Input.anyKeyDown)
                {
                    HideCredits();
                }

                return;
            }

            if (!IsResult())
            {
                return;
            }

            // 键盘仍作为便捷通道保留（与按钮并行），由 ResultConfig 开关门控
            // （陷阱 5：SubClear 两者 false，防止玩家误按 R 直接重开）。
            var entry = _resultConfig != null ? _resultConfig.Get(_phase) : null;
            var gm = GameManager.Instance;
            if (gm == null || entry == null)
            {
                return;
            }

            if (entry.RespondsRestart && Input.GetKeyDown(_restartKey))
            {
                gm.RestartGame();
            }
            else if (entry.RespondsMenu && Input.GetKeyDown(_menuKey))
            {
                gm.ReturnToMainMenu();
            }
        }

        private void ApplyVisual()
        {
            bool show = IsResult();

            if (!show)
            {
                if (_fadeRoutine != null)
                {
                    StopCoroutine(_fadeRoutine);
                    _fadeRoutine = null;
                }

                DestroyArt();
                HideCredits();
                _pendingResultEntry = null;

                if (_root != null)
                {
                    _root.SetActive(false);
                }

                return;
            }

            if (_root != null)
            {
                _root.SetActive(true);
            }

            var entry = _resultConfig != null ? _resultConfig.Get(_phase) : null;

            // 通关前置名单（用户要求）：最终胜利先全屏展示制作组名单，维持数秒后任意键关闭，
            // 关闭（HideCredits）再接着弹结算；其余状态（SubClear/Fail）直接结算。
            if (_phase == GamePhase.Victory && ShouldShowCreditsIntro())
            {
                _pendingResultEntry = entry;

                // 确保根层 CanvasGroup 不透明，名单才可见（避免上一态淡入残留的 alpha 把名单一并压暗）。
                var rootCg = EnsureCanvasGroup();
                if (rootCg != null)
                {
                    rootCg.alpha = 1f;
                }

                ShowCredits(true);
                return;
            }

            PresentResult(entry);
        }

        /// <summary>是否走通关前置名单：配置开启且配了制作组图。</summary>
        private bool ShouldShowCreditsIntro()
        {
            return _resultConfig != null
                   && _resultConfig.ShowCreditsIntroOnVictory
                   && _resultConfig.CreditsImage != null;
        }

        /// <summary>正式展示结算面板：美术路径（背景图 + 按钮层）或纯文字路径，并淡入（#1）。</summary>
        private void PresentResult(ResultConfig.ResultEntry entry)
        {
            // 分流：配了背景图 → 美术路径（图 + 按钮层，隐藏文字）；否则 → 纯文字路径。
            if (entry != null && entry.Background != null)
            {
                SetTextActive(false);
                BuildArt(entry);
            }
            else
            {
                DestroyArt();
                SetTextActive(true);
                ApplyText(entry);
            }

            // 结算面板淡入（#1）：CanvasGroup alpha 0→1，比瞬间弹出更柔和。
            var cg = EnsureCanvasGroup();
            if (cg != null)
            {
                cg.alpha = 0f;
                if (_fadeRoutine != null)
                {
                    StopCoroutine(_fadeRoutine);
                }

                _fadeRoutine = StartCoroutine(FadeInRoutine(cg));
            }
        }

        // 纯文字路径（无美术，如 SubClear）：把标题/提示按配置渲染。
        private void ApplyText(ResultConfig.ResultEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (_title != null)
            {
                _title.text = entry.Title;
                _title.color = entry.Color;
            }

            if (_hint != null)
            {
                _hint.text = entry.Hint;
            }
        }

        private void SetTextActive(bool active)
        {
            if (_title != null)
            {
                _title.gameObject.SetActive(active);
            }

            if (_hint != null)
            {
                _hint.gameObject.SetActive(active);
            }
        }

        // ──────────────────────────────────────────────────────
        //  美术路径：背景层 + 全屏图层按钮
        // ──────────────────────────────────────────────────────

        private void BuildArt(ResultConfig.ResultEntry entry)
        {
            if (_root == null)
            {
                return;
            }

            DestroyArt(); // 每次进入结算态重建，避免上一态残留按钮

            EnsureRaycastInfra(); // AnchorHorror 场景原无按钮交互，缺 Raycaster/EventSystem 时自愈补上

            _artRoot = new GameObject("ResultArt(runtime)", typeof(RectTransform));
            _artRoot.transform.SetParent(_root.transform, false);
            StretchFull(_artRoot.transform as RectTransform);

            // 背景层（不接收点击，纯铺底；点击交给上方按钮层）
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(_artRoot.transform, false);
            StretchFull(bgGo.transform as RectTransform);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = entry.Background;
            bgImg.color = Color.white;
            bgImg.type = Image.Type.Simple;
            bgImg.raycastTarget = false;

            // 按钮层（各自全屏，笔触区不重叠即可堆叠）
            var buttons = entry.Buttons;
            for (int i = 0; i < buttons.Length; i++)
            {
                BuildFullScreenButton(_artRoot.transform, buttons[i]);
            }
        }

        private void BuildFullScreenButton(Transform parent, ResultConfig.ResultButton def)
        {
            if (def == null || def.Sprite == null)
            {
                return;
            }

            var go = new GameObject("ResultButton_" + def.Action, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.transform as RectTransform;
            StretchFull(rt);

            var img = go.AddComponent<Image>();
            img.sprite = def.Sprite;
            img.color = Color.white;
            img.type = Image.Type.Simple;
            img.raycastTarget = true;

            // 能否按笔触命中：成功 → 该按钮可点、加悬浮/按压缩放反馈；失败（纹理未开 Read/Write）
            // → 关掉整层 raycast，宁可暂不可点，也不让全屏层整块遮挡其它按钮，且不加反馈（无指针事件）。
            bool clickable = TrySetAlphaHit(img);
            if (!clickable)
            {
                img.raycastTarget = false;
            }

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None; // 全屏层用 Color Tint 会给整张图染色闪烁
            btn.targetGraphic = img;
            var action = def.Action; // 捕获值，避免闭包引用循环变量
            btn.onClick.AddListener(() => OnResultButton(action));

            // 鼠标反馈：先把 pivot 移到图标不透明区中心，缩放才是"图标原地放大/缩小"而非朝屏幕中心漂移。
            // 复用主菜单同款通用组件 UIPressScaleFeedback（Ciga.UI），避免两套等价实现。
            if (clickable)
            {
                rt.pivot = UIPressScaleFeedback.OpaqueCenterNormalized(def.Sprite);
                go.AddComponent<UIPressScaleFeedback>();
            }
        }

        private void OnResultButton(ResultAction action)
        {
            var gm = GameManager.Instance;
            switch (action)
            {
                case ResultAction.Restart:
                    if (gm != null)
                    {
                        gm.RestartGame();
                    }

                    break;
                case ResultAction.Menu:
                    if (gm != null)
                    {
                        gm.ReturnToMainMenu();
                    }

                    break;
                case ResultAction.Quit:
                    if (gm != null)
                    {
                        gm.QuitGame();
                    }

                    break;
                case ResultAction.Credits:
                    ShowCredits();
                    break;
            }
        }

        private void DestroyArt()
        {
            if (_artRoot != null)
            {
                Destroy(_artRoot);
                _artRoot = null;
            }
        }

        // ──────────────────────────────────────────────────────
        //  制作组页（运行时自建全屏遮罩，点任意处返回）
        // ──────────────────────────────────────────────────────

        // Credits 按钮点开入口（isIntro=false）：维持 CreditsLockSeconds 防同击秒关，关闭仅返回结算。
        private void ShowCredits()
        {
            ShowCredits(false);
        }

        // 制作组页两种入口共用：
        //   isIntro=true  → 通关前置名单：维持 CreditsIntroHoldSeconds 秒后任意键关闭，关闭后接着弹结算（用户要求）；
        //   isIntro=false → Credits 按钮点开：维持 CreditsLockSeconds 防同击秒关，关闭仅返回结算。
        private void ShowCredits(bool isIntro)
        {
            EnsureRaycastInfra();
            EnsureCreditsPanel();
            if (_creditsRoot == null)
            {
                return;
            }

            _creditsIsIntro = isIntro;
            _creditsRoot.transform.SetAsLastSibling(); // 置于最上层，盖住结算按钮
            _creditsRoot.SetActive(true);
            if (_creditsGroup != null)
            {
                _creditsGroup.alpha = 1f;
                _creditsGroup.blocksRaycasts = true; // 挡住下方按钮，避免误触
                _creditsGroup.interactable = true;
            }

            _creditsShowing = true;
            float hold = isIntro && _resultConfig != null ? _resultConfig.CreditsIntroHoldSeconds : CreditsLockSeconds;
            _creditsUnlockTime = Time.unscaledTime + Mathf.Max(0f, hold);
        }

        private void HideCredits()
        {
            bool wasIntro = _creditsIsIntro;
            _creditsShowing = false;
            _creditsIsIntro = false;
            if (_creditsGroup != null)
            {
                _creditsGroup.blocksRaycasts = false;
                _creditsGroup.interactable = false;
            }

            if (_creditsRoot != null)
            {
                _creditsRoot.SetActive(false);
            }

            // 前置名单关闭后接着弹胜利结算（用户要求）。仅在仍处结算态时继续，
            // 避免过渡 / 离开结算的清理路径里误触发。
            if (wasIntro && IsResult())
            {
                var entry = _pendingResultEntry;
                _pendingResultEntry = null;
                PresentResult(entry);
            }
        }

        private void EnsureCreditsPanel()
        {
            if (_creditsRoot != null || _root == null)
            {
                return;
            }

            _creditsRoot = new GameObject("CreditsPanel(runtime)", typeof(RectTransform));
            _creditsRoot.transform.SetParent(_root.transform, false);
            StretchFull(_creditsRoot.transform as RectTransform);
            _creditsGroup = _creditsRoot.AddComponent<CanvasGroup>();

            // 全屏图层（有制作组图则铺图，否则深色占位底），同时充当点击捕获层
            var img = _creditsRoot.AddComponent<Image>();
            img.type = Image.Type.Simple;
            img.raycastTarget = true;
            var creditsSprite = _resultConfig != null ? _resultConfig.CreditsImage : null;
            if (creditsSprite != null)
            {
                img.sprite = creditsSprite;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.05f, 0.05f, 0.08f, 0.96f);
            }

            // 占位文字（有制作组图时留空；无图时显示名单占位 + 返回提示）
            var textGo = new GameObject("CreditsText", typeof(RectTransform));
            textGo.transform.SetParent(_creditsRoot.transform, false);
            StretchFull(textGo.transform as RectTransform);
            _creditsText = textGo.AddComponent<TextMeshProUGUI>();
            _creditsText.alignment = TextAlignmentOptions.Center;
            _creditsText.fontSize = 58f;
            _creditsText.color = new Color(1f, 1f, 1f, 0.9f);
            _creditsText.raycastTarget = false;
            if (creditsSprite != null)
            {
                _creditsText.text = string.Empty;
            }
            else
            {
                string body = _resultConfig != null && !string.IsNullOrEmpty(_resultConfig.CreditsText)
                    ? _resultConfig.CreditsText
                    : "制作组";
                _creditsText.text = body + "\n\n<size=42>点击屏幕任意处返回</size>";
            }

            _creditsRoot.SetActive(false);
        }

        // ──────────────────────────────────────────────────────
        //  基础设施 / 工具
        // ──────────────────────────────────────────────────────

        /// <summary>确保当前 Canvas 有 GraphicRaycaster、场景有 EventSystem（否则按钮点不动）。</summary>
        private void EnsureRaycastInfra()
        {
            if (_root != null)
            {
                var canvas = _root.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }
            }

            if (EventSystem.current == null && FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                // 结算界面独立于关卡场景，随其存活即可；不 DontDestroyOnLoad，避免跨场景残留多个。
                es.hideFlags = HideFlags.None;
            }
        }

        /// <summary>
        /// 开 alpha 命中：只有笔触（非透明）可点、透明处穿透到下层按钮。
        /// 前置是纹理 Read/Write Enabled，否则赋值抛异常。返回是否成功启用（调用方据此降级）。
        /// </summary>
        private bool TrySetAlphaHit(Image img)
        {
            if (img == null || img.sprite == null)
            {
                return false;
            }

            var tex = img.sprite.texture;
            if (tex != null && tex.isReadable)
            {
                img.alphaHitTestMinimumThreshold = AlphaHitThreshold;
                return true;
            }

            Debug.LogError("[ResultScreen] 结算按钮图纹理未开 Read/Write Enabled，无法按笔触命中；" +
                           "已关闭该全屏层 raycast 以免遮挡其它按钮。请在导入设置勾选 Read/Write Enabled。", this);
            return false;
        }

        // _root 上懒挂 CanvasGroup（生成器未加也能自愈，无需重跑装配）。
        private CanvasGroup EnsureCanvasGroup()
        {
            if (_root == null)
            {
                return null;
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = _root.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = _root.AddComponent<CanvasGroup>();
                }
            }

            return _canvasGroup;
        }

        private IEnumerator FadeInRoutine(CanvasGroup cg)
        {
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime; // 结算可能处于暂停/timeScale 异常，用 unscaled 保证淡入照常
                cg.alpha = _fadeDuration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _fadeDuration)) : 1f;
                yield return null;
            }

            cg.alpha = 1f;
            _fadeRoutine = null;
        }

        private static void StretchFull(RectTransform rt)
        {
            if (rt == null)
            {
                return;
            }

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private bool IsResult()
        {
            return _phase == GamePhase.SubClear || _phase == GamePhase.Victory || _phase == GamePhase.Fail;
        }
    }
}
