// ------------------------------------------------------------
// ResultConfig.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 三态结算配置 SO（SubClear / Victory / Fail）。
    /// ResultScreen 挂此资产，按当前 GamePhase 查 ResultEntry 渲染 UI 与控制按键响应（ADR-5）。
    /// 策划可在 Inspector 直接调文案、颜色与按键开关，零修改代码。
    /// </summary>
    [CreateAssetMenu(fileName = "ResultConfig", menuName = "Ciga/AnchorHorror/ResultConfig")]
    public class ResultConfig : ScriptableObject
    {
        /// <summary>单态结算数据：标题、颜色、提示文、按键响应开关。</summary>
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
        }

        [Header("子关通关（锚点全激活，等待过门）")]
        [SerializeField] private ResultEntry _subClear = new ResultEntry();

        [Header("最终胜利（末关通关）")]
        [SerializeField] private ResultEntry _victory = new ResultEntry();

        [Header("失败（理智归零）")]
        [SerializeField] private ResultEntry _fail = new ResultEntry();

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
