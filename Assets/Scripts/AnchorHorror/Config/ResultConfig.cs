// ------------------------------------------------------------
// ResultConfig.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>结算界面按钮触发的动作（数据驱动，策划在 Inspector 为每张按钮图指定）。</summary>
    public enum ResultAction
    {
        Restart,   // 重开本局
        Menu,      // 返回主菜单 / 标题
        Quit,      // 退出游戏
        Credits,   // 显示制作组页
    }

    /// <summary>
    /// 三态结算配置 SO（SubClear / Victory / Fail）。
    /// ResultScreen 挂此资产，按当前 GamePhase 查 ResultEntry 渲染 UI 与控制按键响应（ADR-5）。
    /// 每态可选配一张全屏背景图（win.PNG / defeat.PNG）与若干全屏图层按钮：
    ///   配了背景图 → 隐藏 TMP 标题/提示（文字已烙进美术），运行时自建 alpha 命中按钮层；
    ///   未配背景图（如 SubClear）→ 退回纯文字 + 键盘（R/Esc）结算，向后兼容。
    /// 策划可在 Inspector 直接调文案、颜色、按键开关、背景图与按钮，零修改代码。
    /// </summary>
    [CreateAssetMenu(fileName = "ResultConfig", menuName = "Ciga/AnchorHorror/ResultConfig")]
    public class ResultConfig : ScriptableObject
    {
        /// <summary>单张全屏图层按钮：一张 1920x1080 图（笔触外透明）+ 点击触发的动作。</summary>
        [Serializable]
        public class ResultButton
        {
            [Tooltip("全屏图层按钮图（1920x1080，笔触外透明）。须开 Read/Write Enabled 才能按笔触命中。")]
            [SerializeField] private Sprite _sprite;

            [Tooltip("点击此按钮触发的动作。")]
            [SerializeField] private ResultAction _action = ResultAction.Menu;

            /// <summary>按钮全屏图层图。</summary>
            public Sprite Sprite => _sprite;

            /// <summary>点击触发的动作。</summary>
            public ResultAction Action => _action;
        }

        /// <summary>单态结算数据：标题、颜色、提示文、按键响应开关、背景图与按钮。</summary>
        [Serializable]
        public class ResultEntry
        {
            [SerializeField] private string _title = "标题";
            [SerializeField] private Color _color = Color.white;
            [SerializeField] private string _hint = "提示文";

            [Tooltip("此状态下 R 键是否触发重开局。SubClear 应关闭，Victory/Fail 可按需开启。")]
            [SerializeField] private bool _respondsRestart;

            [Tooltip("此状态下 Esc 键是否触发返回主菜单。SubClear 应关闭。")]
            [SerializeField] private bool _respondsMenu;

            [Header("美术（可选）")]
            [Tooltip("全屏背景图（如 win.PNG / defeat.PNG）。配了则隐藏文字标题/提示，改用图 + 按钮层；留空则退回纯文字结算。")]
            [SerializeField] private Sprite _background;

            [Tooltip("全屏图层按钮（各自 1920x1080，笔触区不重叠即可堆叠）。运行时按此列表自建可点击层。")]
            [SerializeField] private ResultButton[] _buttons = Array.Empty<ResultButton>();

            /// <summary>结算标题文本。</summary>
            public string Title => _title;

            /// <summary>结算标题颜色。</summary>
            public Color Color => _color;

            /// <summary>操作提示文本。</summary>
            public string Hint => _hint;

            /// <summary>此状态下是否响应重开局按键。</summary>
            public bool RespondsRestart => _respondsRestart;

            /// <summary>此状态下是否响应返回主菜单按键。</summary>
            public bool RespondsMenu => _respondsMenu;

            /// <summary>全屏背景图（null 表示走纯文字路径）。</summary>
            public Sprite Background => _background;

            /// <summary>全屏图层按钮列表（永不为 null，未配为空数组）。</summary>
            public ResultButton[] Buttons => _buttons ?? Array.Empty<ResultButton>();
        }

        [Header("子关通关（锚点全激活，等待过门）")]
        [SerializeField] private ResultEntry _subClear = new ResultEntry();

        [Header("最终胜利（末关通关）")]
        [SerializeField] private ResultEntry _victory = new ResultEntry();

        [Header("失败（理智归零）")]
        [SerializeField] private ResultEntry _fail = new ResultEntry();

        [Header("制作组页（可选，供 Credits 按钮）")]
        [Tooltip("制作组按钮点开后显示的全屏图；留空则用深色占位底 + 文字。")]
        [SerializeField] private Sprite _creditsImage;

        [Tooltip("制作组占位文字（无制作组图时显示）。")]
        [TextArea(3, 8)]
        [SerializeField] private string _creditsText = "制作组\n\n（名单待补）";

        [Header("通关前置名单（用户要求）")]
        [Tooltip("最终胜利时，先全屏展示制作组名单（CreditsImage），维持数秒后任意键关闭，再弹出胜利结算。")]
        [SerializeField] private bool _showCreditsIntroOnVictory = true;

        [Tooltip("前置名单维持多少秒后才允许任意键关闭。")]
        [SerializeField] private float _creditsIntroHoldSeconds = 2f;

        /// <summary>制作组页全屏图（可空）。</summary>
        public Sprite CreditsImage => _creditsImage;

        /// <summary>制作组占位文字（无图时显示）。</summary>
        public string CreditsText => _creditsText;

        /// <summary>最终胜利时是否先展示制作组名单再弹结算（用户要求）。</summary>
        public bool ShowCreditsIntroOnVictory => _showCreditsIntroOnVictory;

        /// <summary>前置名单维持秒数（此后才允许任意键关闭）。</summary>
        public float CreditsIntroHoldSeconds => _creditsIntroHoldSeconds;

        /// <summary>按阶段取结算数据；传入非结算阶段返回 null。</summary>
        public ResultEntry Get(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.SubClear:
                    return _subClear;
                case GamePhase.Victory:
                    return _victory;
                case GamePhase.Fail:
                    return _fail;
                default:
                    return null;
            }
        }
    }
}
