// ------------------------------------------------------------
// UIManager.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.UI
{
    /// <summary>
    /// 每场景单例（不 DontDestroyOnLoad）。
    /// 按名称注册/查询 UIPanel；Show(name) 先 HideAll 再显目标，同时刻单面板。
    /// 无栈 / 无动画 / 无遮罩——够三面板场景即停（ADR-1）。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;

        /// <summary>当前场景的 UIManager 单例，场景切换后自动失效。</summary>
        public static UIManager Instance => _instance;

        private readonly Dictionary<string, UIPanel> _panels = new Dictionary<string, UIPanel>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[UIManager] 场景内已有一个 UIManager，销毁多余实例。", this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>向 Manager 注册一个面板，键为面板名称。</summary>
        /// <param name="panelName">面板名称（作为查找键）</param>
        /// <param name="panel">面板实例</param>
        public void Register(string panelName, UIPanel panel)
        {
            if (string.IsNullOrEmpty(panelName) || panel == null)
            {
                return;
            }

            _panels[panelName] = panel;
        }

        /// <summary>隐藏所有已注册面板，再显示目标面板（同时刻单面板）。</summary>
        /// <param name="panelName">要显示的面板名称</param>
        public void Show(string panelName)
        {
            HideAll();

            if (_panels.TryGetValue(panelName, out var target))
            {
                target.Show();
            }
            else
            {
                Debug.LogWarning($"[UIManager] 找不到面板 '{panelName}'，请确认已 Register。", this);
            }
        }

        /// <summary>隐藏所有已注册面板。</summary>
        public void HideAll()
        {
            foreach (var kvp in _panels)
            {
                kvp.Value.Hide();
            }
        }
    }
}
