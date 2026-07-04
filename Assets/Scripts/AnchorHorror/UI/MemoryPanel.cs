// ------------------------------------------------------------
// MemoryPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 记忆面板：Tab 开关，打开时暂停（Time.timeScale=0），列出 5 个目标锚点及激活状态。
    /// 激活锚点显示金色"已锚定"。数据来自 AnchorSystem + 订阅 AnchorActivated / TargetsExtracted。
    /// </summary>
    public class MemoryPanel : MonoBehaviour
    {
        private const string GoldHex = "#FFD700";
        private const string DimHex = "#AAAAAA";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _content;
        [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;

        private readonly StringBuilder _sb = new StringBuilder(256);
        private bool _open;

        private void OnEnable()
        {
            EventBus.TargetsExtracted += OnTargetsChanged;
            EventBus.AnchorActivated += OnAnchorActivated;
        }

        private void OnDisable()
        {
            EventBus.TargetsExtracted -= OnTargetsChanged;
            EventBus.AnchorActivated -= OnAnchorActivated;

            // 面板打开（timeScale=0）时被禁用/销毁，避免游戏永久暂停
            if (_open)
            {
                _open = false;
                Time.timeScale = 1f;
            }
        }

        private void Start()
        {
            if (_root == null)
            {
                Debug.LogWarning("[AnchorHorror] MemoryPanel 未接线（_root 为空）：记忆面板不可见，Tab 将不暂停游戏。" +
                                 "请运行菜单 Ciga/AnchorHorror/生成可运行装配 重建场景以接上面板。");
            }

            SetOpen(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                SetOpen(!_open);
            }
        }

        private void SetOpen(bool open)
        {
            // 生成器未接线（_root 为空）时绝不接管 timeScale：否则会"看不见面板还把游戏冻住"，
            // 表现为按一次 Tab 像死机、再按一次才恢复。对齐 ResultScreen 的判空降级。
            if (_root == null)
            {
                _open = false;
                return;
            }

            _open = open;
            _root.SetActive(open);
            Time.timeScale = open ? 0f : 1f;
            if (open)
            {
                Refresh();
            }
        }

        private void OnTargetsChanged(IReadOnlyList<AnchorTarget> targets)
        {
            if (_open)
            {
                Refresh();
            }
        }

        private void OnAnchorActivated(AnchorTarget anchor)
        {
            if (_open)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_content == null)
            {
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null || gm.Anchor == null)
            {
                _content.text = string.Empty;
                return;
            }

            var targets = gm.Anchor.Targets;
            var db = gm.Database;

            _sb.Clear();
            _sb.AppendLine("<b>记忆锚点</b>");
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                string name = db != null ? db.GetDisplayName(t.Feature) : t.Feature.ToString();
                if (t.IsActivated)
                {
                    _sb.AppendLine($"<color={GoldHex}>{name}　已锚定 ({t.CurrentCount}/{t.RequiredCount})</color>");
                }
                else
                {
                    _sb.AppendLine($"<color={DimHex}>{name}　{t.CurrentCount}/{t.RequiredCount}</color>");
                }
            }

            _content.text = _sb.ToString();
        }
    }
}
