// ------------------------------------------------------------
// LoadingConfig.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.Startup
{
    /// <summary>
    /// 加载条美术 + 假进度参数配置。SceneLoader 持有此 SO 并喂给 LoadingOverlay。
    /// SO 只暴露只读属性，运行时状态不进 SO。
    /// </summary>
    [CreateAssetMenu(fileName = "LoadingConfig", menuName = "Ciga/Startup/LoadingConfig")]
    public class LoadingConfig : ScriptableObject
    {
        [Header("美术")]
        [Tooltip("加载页背景图（留空则纯色）")]
        [SerializeField] private Sprite _background;

        [Tooltip("进度条底框图（留空则纯色）")]
        [SerializeField] private Sprite _barBackground;

        [Tooltip("进度条填充图（留空则纯色）")]
        [SerializeField] private Sprite _barFill;

        [Tooltip("进度条填充颜色")]
        [SerializeField] private Color _barFillColor = new Color(0.8f, 0.8f, 0.9f, 1f);

        [Header("文案")]
        [Tooltip("加载提示文字（留空则使用默认）")]
        [SerializeField] private string _hintText = "加载中…";

        [Header("假进度参数")]
        [Tooltip("加载条从 0 到满的最短时长（秒），防止瞬间切换 / 玩法残留 timeScale 影响感知")]
        [SerializeField] private float _minDuration = 1.5f;

        public Sprite Background => _background;
        public Sprite BarBackground => _barBackground;
        public Sprite BarFill => _barFill;
        public Color BarFillColor => _barFillColor;
        public string HintText => _hintText;
        public float MinDuration => _minDuration;
    }
}
