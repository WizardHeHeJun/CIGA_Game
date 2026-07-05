// ------------------------------------------------------------
// MainMenuPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using Ciga.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.Startup
{
    /// <summary>
    /// 主菜单面板（全屏图层版）：背景 + 开始/操作指引/结束三张 1920x1080 全屏图层堆叠，
    /// 靠 Image.alphaHitTestMinimumThreshold 让毛笔笔触外的透明处穿透，只有笔触可点。
    /// 「开始」→ LoadScene(Bootstrap)；「结束」→ Application.Quit；
    /// 「操作指引」→ 场景内全屏引导页 overlay（打开后前 3 秒忽略点击，之后点屏幕任意处返回）。
    /// 文字已烙进美术，故运行时隐藏旧的 Title/Logo/按钮文案标签。
    /// 操作指引按钮与引导页：生成器接线则直接用；留空则运行时自建（兼容旧场景）。
    /// </summary>
    public class MainMenuPanel : UIPanel
    {
        private const float AlphaHitThreshold = 0.1f;   // alpha > 0.1 才可点，过滤羽化透明边
        private const float GuideLockSeconds = 3f;       // 引导页打开后忽略点击的秒数

        [Header("配置（Inspector 拖入）")]
        [SerializeField] private MainMenuConfig _config;

        [Header("UI 引用（生成器接线）")]
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _logoImage;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _startButtonLabel;
        [SerializeField] private Button _quitButton;
        [SerializeField] private TMP_Text _quitButtonLabel;

        [Header("操作指引（生成器接线；留空则运行时自建）")]
        [SerializeField] private Button _guideButton;
        [SerializeField] private GameObject _guideRoot;
        [SerializeField] private Image _guideImage;
        [SerializeField] private TMP_Text _guideHint;

        private CanvasGroup _guideCanvasGroup;
        private bool _guideShowing;
        private float _guideUnlockTime;
        private bool _guideReturnHintShown;
        private UITextBreathe _guideHintBreathe;
        private UIFadePanel _guideFade;
        private UiClickSfxBinder _clickSfxBinder;

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (_startButton != null)
            {
                _startButton.onClick.AddListener(OnStartClicked);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.AddListener(OnQuitClicked);
            }

            // 已接线的操作指引按钮在此挂监听；运行时自建的按钮由 EnsureGuideButton 首次补挂。
            // 之后每轮启用重新挂上，与 OnDisable 成对，避免残留订阅导致状态与 UI 不同步。
            if (_guideButton != null)
            {
                _guideButton.onClick.AddListener(OnGuideClicked);
            }
        }

        private void OnDisable()
        {
            if (_startButton != null)
            {
                _startButton.onClick.RemoveListener(OnStartClicked);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.RemoveListener(OnQuitClicked);
            }

            if (_guideButton != null)
            {
                _guideButton.onClick.RemoveListener(OnGuideClicked);
            }
        }

        private void Update()
        {
            if (!_guideShowing)
            {
                return;
            }

            bool unlocked = Time.unscaledTime >= _guideUnlockTime;
            if (unlocked && !_guideReturnHintShown)
            {
                _guideReturnHintShown = true;
                if (_guideHint != null)
                {
                    _guideHint.text = "点击屏幕任意处返回";
                }

                // 解锁后提示淡入 + 呼吸（不再一进来就直白显示）
                if (_guideHintBreathe != null)
                {
                    _guideHintBreathe.Play();
                }
            }

            if (unlocked && Input.GetMouseButtonDown(0))
            {
                HideGuide();
            }
        }

        protected override void ApplyConfig()
        {
            if (_config == null)
            {
                PlaceholderWarnOnce("_config");
                return;
            }

            ApplyBackground();

            // 文字已烙进美术 → 隐藏叠加的 Logo / 标题 / 按钮文案标签（新场景已不生成它们，留空即跳过）
            HideIfPresent(_logoImage != null ? _logoImage.gameObject : null);
            HideIfPresent(_titleLabel != null ? _titleLabel.gameObject : null);
            HideIfPresent(_startButtonLabel != null ? _startButtonLabel.gameObject : null);
            HideIfPresent(_quitButtonLabel != null ? _quitButtonLabel.gameObject : null);

            // 开始 / 结束：把按钮改造成全屏 alpha 命中图层（小按钮或已是全屏都幂等）
            RetrofitFullScreenButton(_startButton, _config.StartButtonSprite, "StartButtonSprite");
            RetrofitFullScreenButton(_quitButton, _config.QuitButtonSprite, "QuitButtonSprite");

            // 操作指引：已接线则配置、留空则自建
            EnsureGuideButton();
            EnsureGuidePanel();
            EnsureClickSfxBinder();
        }

        private void ApplyBackground()
        {
            if (_backgroundImage == null)
            {
                return;
            }

            if (_config.Background != null)
            {
                _backgroundImage.sprite = _config.Background;
                _backgroundImage.color = Color.white;
            }
            else
            {
                // 无背景图 → 置透明，露出面板深色占位底
                _backgroundImage.sprite = null;
                _backgroundImage.color = new Color(1f, 1f, 1f, 0f);
                PlaceholderWarnOnce("Background");
            }
        }

        /// <summary>把按钮拉伸铺满、换全屏图层 sprite、开 alpha 命中、关按钮变色过渡。图为空则保留原样并告警。</summary>
        private void RetrofitFullScreenButton(Button btn, Sprite sprite, string fieldKey)
        {
            if (btn == null)
            {
                PlaceholderWarnOnce(fieldKey + ":button");
                return;
            }

            if (sprite == null)
            {
                // 图未配 → 不改造（保留可点的旧按钮），仅告警
                PlaceholderWarnOnce(fieldKey);
                return;
            }

            StretchFull(btn.transform as RectTransform);

            var img = (btn.targetGraphic as Image) ?? btn.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.type = Image.Type.Simple;
                img.raycastTarget = true;
                if (!TrySetAlphaHit(img, fieldKey))
                {
                    // 无法笔触命中 → 关掉全屏层 raycast，避免整块遮挡其它按钮（宁可该按钮暂不可点也不锁死全菜单）
                    img.raycastTarget = false;
                }

                btn.targetGraphic = img;
            }

            // 全屏图层若用默认 Color Tint 过渡，点击/悬停会给整张 1920x1080 染色闪烁
            btn.transition = Selectable.Transition.None;

            EnsureScaleFeedback(btn, sprite);
        }

        /// <summary>确保按钮挂了悬浮放大/按下缩小反馈：生成器已烙则跳过；否则运行时补挂并按笔触中心设 pivot。</summary>
        private void EnsureScaleFeedback(Button btn, Sprite sprite)
        {
            if (btn == null || btn.GetComponent<UIPressScaleFeedback>() != null)
            {
                return;
            }

            var rt = btn.transform as RectTransform;
            if (rt != null && sprite != null)
            {
                rt.pivot = UIPressScaleFeedback.OpaqueCenterNormalized(sprite); // 缩放绕笔触中心，避免漂移
            }

            btn.gameObject.AddComponent<UIPressScaleFeedback>();
        }

        /// <summary>操作指引按钮：已接线则配置外观；留空则运行时自建全屏图层。</summary>
        private void EnsureGuideButton()
        {
            bool justBuilt = false;
            if (_guideButton == null)
            {
                if (_config.GuideButtonSprite == null)
                {
                    PlaceholderWarnOnce("GuideButtonSprite");
                    return;
                }

                var parent = Root != null ? Root.transform : transform;
                var go = new GameObject("GuideButton(runtime)", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                _guideButton = go.AddComponent<Button>();
                justBuilt = true;
            }

            StretchFull(_guideButton.transform as RectTransform);

            var img = (_guideButton.targetGraphic as Image) ?? _guideButton.GetComponent<Image>();
            if (img == null)
            {
                img = _guideButton.gameObject.AddComponent<Image>();
            }

            if (_config.GuideButtonSprite != null)
            {
                img.sprite = _config.GuideButtonSprite;
                img.color = Color.white;
            }

            img.type = Image.Type.Simple;
            img.raycastTarget = true;
            if (!TrySetAlphaHit(img, "GuideButtonSprite"))
            {
                img.raycastTarget = false;
            }

            _guideButton.transition = Selectable.Transition.None;
            _guideButton.targetGraphic = img;

            if (justBuilt)
            {
                // 自建按钮首次 OnEnable 时尚不存在，这里补挂一次；之后由 OnEnable/OnDisable 成对管理
                _guideButton.onClick.AddListener(OnGuideClicked);
            }

            EnsureScaleFeedback(_guideButton, _config.GuideButtonSprite);
        }

        /// <summary>引导页：已接线则配置；留空则运行时自建。默认隐藏。</summary>
        private void EnsureGuidePanel()
        {
            if (_guideRoot == null)
            {
                BuildGuidePanel();
            }

            if (_guideRoot == null)
            {
                return;
            }

            _guideCanvasGroup = _guideRoot.GetComponent<CanvasGroup>();
            if (_guideCanvasGroup == null)
            {
                _guideCanvasGroup = _guideRoot.AddComponent<CanvasGroup>();
            }

            if (_guideImage != null)
            {
                if (_config.GuidePageImage != null)
                {
                    _guideImage.sprite = _config.GuidePageImage;
                    _guideImage.color = Color.white;
                }
                else
                {
                    // 引导图待补 → 深色占位底
                    _guideImage.sprite = null;
                    _guideImage.color = new Color(0.05f, 0.05f, 0.08f, 0.96f);
                    PlaceholderWarnOnce("GuidePageImage");
                }

                _guideImage.raycastTarget = true;
            }

            ApplyFont(_guideHint);

            if (_guideHint != null && _guideHintBreathe == null)
            {
                _guideHintBreathe = _guideHint.GetComponent<UITextBreathe>()
                                    ?? _guideHint.gameObject.AddComponent<UITextBreathe>();
                _guideHintBreathe.Stop();
            }

            // 引导页整体淡入淡出（手感对齐关卡切换，不再硬切）
            if (_guideFade == null)
            {
                _guideFade = _guideRoot.GetComponent<UIFadePanel>() ?? _guideRoot.AddComponent<UIFadePanel>();
            }

            _guideRoot.SetActive(false);
        }

        private void BuildGuidePanel()
        {
            var parent = Root != null ? Root.transform : transform;
            _guideRoot = new GameObject("GuidePanel(runtime)", typeof(RectTransform));
            _guideRoot.transform.SetParent(parent, false);
            StretchFull(_guideRoot.transform as RectTransform);

            // 图片层（全屏），同时充当点击捕获层
            var imgGo = new GameObject("GuideImage", typeof(RectTransform));
            imgGo.transform.SetParent(_guideRoot.transform, false);
            StretchFull(imgGo.transform as RectTransform);
            _guideImage = imgGo.AddComponent<Image>();
            _guideImage.type = Image.Type.Simple;

            // 提示文字（底部居中）
            var hintGo = new GameObject("GuideHint", typeof(RectTransform));
            hintGo.transform.SetParent(_guideRoot.transform, false);
            var hrt = (RectTransform)hintGo.transform;
            hrt.anchorMin = new Vector2(0.5f, 0.08f);
            hrt.anchorMax = new Vector2(0.5f, 0.08f);
            hrt.sizeDelta = new Vector2(1400f, 120f);
            hrt.anchoredPosition = Vector2.zero;
            _guideHint = hintGo.AddComponent<TextMeshProUGUI>();
            _guideHint.alignment = TextAlignmentOptions.Center;
            _guideHint.fontSize = 56f;
            _guideHint.color = new Color(1f, 1f, 1f, 0.85f);
            _guideHint.raycastTarget = false;
        }

        private void OnGuideClicked()
        {
            EnsureGuidePanel();
            if (_guideRoot == null)
            {
                return;
            }

            _guideRoot.transform.SetAsLastSibling(); // 置于最上层
            if (_guideCanvasGroup != null)
            {
                _guideCanvasGroup.blocksRaycasts = true; // 挡住下方菜单按钮，避免误触
                _guideCanvasGroup.interactable = true;
            }

            // 从透明淡入（手感对齐关卡切换，不再硬切）
            if (_guideFade != null)
            {
                _guideFade.SetInstant(0f);
                _guideFade.FadeIn();
            }
            else
            {
                _guideRoot.SetActive(true);
                if (_guideCanvasGroup != null)
                {
                    _guideCanvasGroup.alpha = 1f;
                }
            }

            _guideShowing = true;
            _guideReturnHintShown = false;
            _guideUnlockTime = Time.unscaledTime + GuideLockSeconds;
            if (_guideHint != null)
            {
                bool placeholder = _config != null && _config.GuidePageImage == null;
                if (placeholder)
                {
                    // 图缺失：静态显示占位提示（不呼吸）
                    if (_guideHintBreathe != null)
                    {
                        _guideHintBreathe.Stop();
                    }

                    _guideHint.text = "操作指引（引导图待补）";
                    _guideHint.alpha = 0.85f;
                }
                else
                {
                    // 正常：锁定期隐藏提示，解锁后由 Update 淡入 + 呼吸
                    _guideHint.text = string.Empty;
                    if (_guideHintBreathe != null)
                    {
                        _guideHintBreathe.Stop();
                    }
                }
            }
        }

        private void ApplyFont(TMP_Text label)
        {
            if (label != null && _config.UiFont != null)
            {
                label.font = _config.UiFont;
            }
        }

        private void HideGuide()
        {
            _guideShowing = false;

            // 淡出后失活（手感对齐关卡切换，不再硬切）。淡出期间保持 blocksRaycasts=true
            // 拦截点击、防止穿透到下方菜单按钮；失活时拦截随之自然解除（下次打开重新置 true）。
            if (_guideFade != null)
            {
                _guideFade.FadeOut(true);
            }
            else if (_guideRoot != null)
            {
                if (_guideCanvasGroup != null)
                {
                    _guideCanvasGroup.blocksRaycasts = false;
                    _guideCanvasGroup.interactable = false;
                }

                _guideRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 开 alpha 命中：只有毛笔笔触（非透明）可点、透明处穿透到下层按钮。
        /// 前置是纹理 Read/Write Enabled，否则赋值会抛异常。返回是否成功启用（调用方据此决定降级）。
        /// </summary>
        private bool TrySetAlphaHit(Image img, string fieldKey)
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

            // 纹理未开 Read/Write：无法按笔触命中；若仍全屏 raycast 会整块遮挡下层按钮 → 由调用方关掉 raycast。
            Debug.LogError($"[MainMenuPanel] 按钮图 '{fieldKey}' 纹理未开 Read/Write Enabled，无法 alpha 命中；" +
                           "已关闭该全屏层 raycast 以免遮挡其它按钮。请在导入设置勾选 Read/Write Enabled。", this);
            return false;
        }

        private static void HideIfPresent(GameObject go)
        {
            if (go != null)
            {
                go.SetActive(false);
            }
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

        private void EnsureClickSfxBinder()
        {
            if (_clickSfxBinder == null)
            {
                _clickSfxBinder = GetComponent<UiClickSfxBinder>();
                if (_clickSfxBinder == null)
                {
                    _clickSfxBinder = gameObject.AddComponent<UiClickSfxBinder>();
                }
            }

            if (_clickSfxBinder == null)
            {
                return;
            }

            var loader = SceneLoader.Instance;
            _clickSfxBinder.SetClickClip(loader != null ? loader.UiClickClip : null);
            _clickSfxBinder.RefreshButtons();
        }

        private void OnStartClicked()
        {
            var loader = SceneLoader.Instance;
            if (loader == null || loader.IsLoading)
            {
                return;
            }

            if (_startButton != null)
            {
                _startButton.interactable = false;
            }

            loader.LoadScene(SceneNames.Bootstrap);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
