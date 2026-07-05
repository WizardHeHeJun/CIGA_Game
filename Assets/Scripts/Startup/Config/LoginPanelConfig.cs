// ------------------------------------------------------------
// LoginPanelConfig.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace Ciga.Startup
{
    /// <summary>
    /// 登录面板美术 + 文案配置。策划在 Inspector 填写，零重编译修改外观。
    /// SO 只暴露只读属性，运行时状态不进 SO。
    /// </summary>
    [CreateAssetMenu(fileName = "LoginPanelConfig", menuName = "Ciga/Startup/LoginPanelConfig")]
    public class LoginPanelConfig : ScriptableObject
    {
        [Header("背景 / 图标")]
        [Tooltip("登录页背景图（留空则纯色）")]
        [SerializeField] private Sprite _background;

        [Tooltip("游戏 Logo 图（留空则跳过）")]
        [SerializeField] private Sprite _logo;

        [Tooltip("进入按钮图（留空则纯色按钮）")]
        [SerializeField] private Sprite _enterButtonSprite;

        [Header("文案")]
        [Tooltip("启动流 UI 统一字体（留空则使用 TMP 默认字体）")]
        [SerializeField] private TMP_FontAsset _uiFont;

        [Tooltip("进入按钮文字")]
        [SerializeField] private string _enterButtonText = "进入游戏";

        [Tooltip("副标题 / 版本说明（留空则隐藏）")]
        [SerializeField] private string _subtitle;

        public Sprite Background => _background;
        public Sprite Logo => _logo;
        public Sprite EnterButtonSprite => _enterButtonSprite;
        public TMP_FontAsset UiFont => _uiFont;
        public string EnterButtonText => _enterButtonText;
        public string Subtitle => _subtitle;
    }
}
