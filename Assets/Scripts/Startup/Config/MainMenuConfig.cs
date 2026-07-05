// ------------------------------------------------------------
// MainMenuConfig.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace Ciga.Startup
{
    /// <summary>
    /// 主菜单美术 + 文案配置。策划在 Inspector 填写，零重编译修改外观。
    /// SO 只暴露只读属性，运行时状态不进 SO。
    /// </summary>
    [CreateAssetMenu(fileName = "MainMenuConfig", menuName = "Ciga/Startup/MainMenuConfig")]
    public class MainMenuConfig : ScriptableObject
    {
        [Header("背景 / 图标")]
        [Tooltip("主菜单背景图（留空则纯色）")]
        [SerializeField] private Sprite _background;

        [Tooltip("游戏 Logo 图（留空则跳过）")]
        [SerializeField] private Sprite _logo;

        [Header("文案")]
        [Tooltip("启动流 UI 统一字体（留空则使用 TMP 默认字体）")]
        [SerializeField] private TMP_FontAsset _uiFont;

        [Tooltip("主标题文字")]
        [SerializeField] private string _titleText = "旧室";

        [Tooltip("开始按钮图（留空则纯色按钮）")]
        [SerializeField] private Sprite _startButtonSprite;

        [Tooltip("开始按钮文字")]
        [SerializeField] private string _startButtonText = "开始游戏";

        [Tooltip("退出按钮图（留空则纯色按钮）")]
        [SerializeField] private Sprite _quitButtonSprite;

        [Tooltip("退出按钮文字")]
        [SerializeField] private string _quitButtonText = "退出";

        [Header("操作指引")]
        [Tooltip("操作指引按钮图（全屏图层，留空则不显示该按钮）")]
        [SerializeField] private Sprite _guideButtonSprite;

        [Tooltip("操作指引页显示的全屏图（留空则用深色占位底 + 提示文字）")]
        [SerializeField] private Sprite _guidePageImage;

        public Sprite Background => _background;
        public Sprite Logo => _logo;
        public TMP_FontAsset UiFont => _uiFont;
        public string TitleText => _titleText;
        public Sprite StartButtonSprite => _startButtonSprite;
        public string StartButtonText => _startButtonText;
        public Sprite QuitButtonSprite => _quitButtonSprite;
        public string QuitButtonText => _quitButtonText;
        public Sprite GuideButtonSprite => _guideButtonSprite;
        public Sprite GuidePageImage => _guidePageImage;
    }
}
