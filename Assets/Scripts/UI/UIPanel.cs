// ------------------------------------------------------------
// UIPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.UI
{
    /// <summary>
    /// 面板基类：Show/Hide（切 _root.SetActive）、Initialize、抽象 ApplyConfig。
    /// PlaceholderWarnOnce 保证同一字段只 LogWarning 一次，避免每帧刷 Console。
    /// 子类在 ApplyConfig 里读 Config SO 渲染内容；若引用为空则调用 PlaceholderWarnOnce 占位。
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        [Header("面板根节点（生成器接线 / 拖入）")]
        [SerializeField] private GameObject _root;

        private readonly HashSet<string> _warnedFields = new HashSet<string>();

        /// <summary>面板根节点，由子类或外部查询可见性。</summary>
        public GameObject Root => _root;

        /// <summary>初始化：申请资源、ApplyConfig。由 UIManager 或自身 Start 调用。</summary>
        public virtual void Initialize()
        {
            ApplyConfig();
        }

        /// <summary>显示面板（激活 Root）。</summary>
        public void Show()
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }
        }

        /// <summary>隐藏面板（停用 Root）。</summary>
        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        /// <summary>子类在此读 Config SO、把内容写入 UI 控件；引用为空时调 PlaceholderWarnOnce。</summary>
        protected abstract void ApplyConfig();

        /// <summary>
        /// 判空占位门闩：同一 field 名只 LogWarning 一次，避免每帧刷 Console。
        /// 当某个 Config 字段为 null / 空字符串时调用此方法提示。
        /// </summary>
        /// <param name="field">字段名（用于去重 key 与日志显示）</param>
        protected void PlaceholderWarnOnce(string field)
        {
            if (_warnedFields.Contains(field))
            {
                return;
            }

            _warnedFields.Add(field);
            Debug.LogWarning($"[{GetType().Name}] 字段 '{field}' 未配置，使用占位默认值。", this);
        }
    }
}
