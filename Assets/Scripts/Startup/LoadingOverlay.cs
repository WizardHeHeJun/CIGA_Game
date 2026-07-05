// ------------------------------------------------------------
// LoadingOverlay.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections;
using Ciga.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.Startup
{
    /// <summary>
    /// 常驻全屏加载遮罩（纯视图）：进度条 + 提示文本。
    /// 由 SceneLoader 在 Awake 懒创建并挂到 DontDestroyOnLoad Canvas 下；
    /// 进度由 SceneLoader 每帧调 SetProgress 喂入，不自驱动。
    /// ApplyConfig 读 LoadingConfig 渲染底图/填充色/提示文案。
    /// </summary>
    public class LoadingOverlay : UIPanel
    {
        // UI 子控件（Initialize 里代码构建）
        private Image _bgImage;
        private Image _barBgImage;
        private Image _barFillImage;
        private TMP_Text _hintLabel;
        private CanvasGroup _canvasGroup;

        private LoadingConfig _config;

        /// <summary>SceneLoader 在构建 Overlay GameObject 后立即调用此方法注入 Config 并初始化。</summary>
        public void Setup(LoadingConfig config)
        {
            _config = config;
            Initialize();
        }

        private void Awake()
        {
            // BuildUi 必须在 Setup/Initialize 之前执行（Awake 早于 SceneLoader 的 Setup 调用）。
            // 不要把 BuildUi 移进 Initialize，否则 Awake + Initialize 会重复构建子 UI。
            BuildUi();
        }

        /// <summary>读 LoadingConfig 渲染底图 / 填充色 / 提示文案。</summary>
        protected override void ApplyConfig()
        {
            if (_config == null)
            {
                return;
            }

            if (_bgImage != null)
            {
                _bgImage.sprite = _config.Background;
                _bgImage.color = _config.Background != null ? Color.white : new Color(0f, 0f, 0f, 0.9f);
            }

            if (_barBgImage != null)
            {
                _barBgImage.sprite = _config.BarBackground;
            }

            if (_barFillImage != null)
            {
                // 仅在配置了填充图时覆盖，否则保留 BuildUi 里的默认白 sprite（Filled 才有效）。
                if (_config.BarFill != null)
                {
                    _barFillImage.sprite = _config.BarFill;
                }

                _barFillImage.color = _config.BarFillColor;
            }

            if (_hintLabel != null)
            {
                if (_config.UiFont != null)
                {
                    _hintLabel.font = _config.UiFont;
                }

                _hintLabel.text = !string.IsNullOrEmpty(_config.HintText) ? _config.HintText : "加载中…";
            }
        }

        /// <summary>设置进度条进度（0～1）。SceneLoader 每帧调用。</summary>
        public void SetProgress(float v01)
        {
            if (_barFillImage != null)
            {
                _barFillImage.fillAmount = Mathf.Clamp01(v01);
            }
        }

        /// <summary>显示 / 隐藏整个 Overlay（直接切自身 GameObject，Overlay 无独立子根）。瞬时，无淡入淡出。</summary>
        public void SetVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f; // 复位，避免上次淡出残留的半透明
            }

            gameObject.SetActive(visible);
        }

        /// <summary>淡入显示（alpha 0→1，#2）。由 SceneLoader 在加载开始时 yield。</summary>
        public IEnumerator FadeIn(float duration)
        {
            gameObject.SetActive(true);
            yield return FadeTo(0f, 1f, duration);
        }

        /// <summary>淡出并隐藏（alpha 1→0 后停用，#2）。由 SceneLoader 在加载结束时 yield。</summary>
        public IEnumerator FadeOut(float duration)
        {
            if (gameObject.activeSelf)
            {
                yield return FadeTo(1f, 0f, duration);
            }

            gameObject.SetActive(false);
        }

        // CanvasGroup.alpha 平滑插值（unscaled：加载期间 timeScale 可能被玩法残留改动）。
        private IEnumerator FadeTo(float from, float to, float duration)
        {
            if (_canvasGroup == null)
            {
                yield break;
            }

            _canvasGroup.alpha = from;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }

            _canvasGroup.alpha = to;
        }

        // 在 Awake 里代码构建子 UI（Overlay 常驻不在具体场景，无法靠生成器接线）。
        private void BuildUi()
        {
            // RectTransform 已由父 Canvas 挂载时自动存在，直接获取
            var rt = gameObject.GetComponent<RectTransform>();
            if (rt == null)
            {
                rt = gameObject.AddComponent<RectTransform>();
            }

            StretchFull(rt);

            // 淡入淡出用 CanvasGroup（挂自身，覆盖所有子控件透明度，#2）
            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = 1f;

            // 背景层（全屏暗色/图片）
            var bgGo = new GameObject("LoadingBg", typeof(RectTransform));
            bgGo.transform.SetParent(transform, false);
            StretchFull((RectTransform)bgGo.transform);
            _bgImage = bgGo.AddComponent<Image>();
            _bgImage.color = new Color(0f, 0f, 0f, 0.9f);
            _bgImage.raycastTarget = true; // 遮挡下方交互

            // 进度条底框（水平居中，底部偏上）
            var barBgGo = new GameObject("BarBg", typeof(RectTransform));
            barBgGo.transform.SetParent(transform, false);
            var barBgRt = (RectTransform)barBgGo.transform;
            barBgRt.anchorMin = new Vector2(0.1f, 0.2f);
            barBgRt.anchorMax = new Vector2(0.9f, 0.2f);
            barBgRt.pivot = new Vector2(0.5f, 0.5f);
            barBgRt.sizeDelta = new Vector2(0f, 24f);
            barBgRt.anchoredPosition = Vector2.zero;
            _barBgImage = barBgGo.AddComponent<Image>();
            _barBgImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            _barBgImage.raycastTarget = false;

            // 进度条填充层（fillMethod = Horizontal）
            var barFillGo = new GameObject("BarFill", typeof(RectTransform));
            barFillGo.transform.SetParent(barBgGo.transform, false);
            var barFillRt = (RectTransform)barFillGo.transform;
            StretchFull(barFillRt);
            _barFillImage = barFillGo.AddComponent<Image>();
            // 默认白 sprite：Filled 类型无 sprite 时 fillAmount 不生效（进度条不动，砸 SC-2）。
            // Texture2D.whiteTexture 是内置资源，不产生额外持久 GC。
            _barFillImage.sprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f));
            _barFillImage.color = new Color(0.8f, 0.8f, 0.9f, 1f);
            _barFillImage.type = Image.Type.Filled;
            _barFillImage.fillMethod = Image.FillMethod.Horizontal;
            _barFillImage.fillAmount = 0f;
            _barFillImage.raycastTarget = false;

            // 提示文本（进度条上方）
            var hintGo = new GameObject("HintText", typeof(RectTransform));
            hintGo.transform.SetParent(transform, false);
            var hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = new Vector2(0f, 0.26f);
            hintRt.anchorMax = new Vector2(1f, 0.26f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(0f, 110f);
            hintRt.anchoredPosition = Vector2.zero;
            _hintLabel = hintGo.AddComponent<TextMeshProUGUI>();
            _hintLabel.text = "加载中…";
            _hintLabel.fontSize = 64f;
            _hintLabel.alignment = TextAlignmentOptions.Center;
            _hintLabel.color = Color.white;
            _hintLabel.raycastTarget = false;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
