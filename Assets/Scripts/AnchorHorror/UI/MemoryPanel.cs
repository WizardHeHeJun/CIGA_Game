// ------------------------------------------------------------
// MemoryPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 记忆页签：Tab 开关，打开时暂停（Time.timeScale=0）。在 Memory 石板的 5 个椭圆槽里
    /// 显示从随机池抽得的 5 个锚点标签（中文，FeatureDatabase.GetDisplayName）。
    /// 已被背包覆盖的锚点显金色，未覆盖显暗色（Inventory.Covers，不用 AnchorTarget.IsActivated，陷阱10）。
    /// 面板由生成器接线（_root=全屏石板层，_anchorLabels=5 个椭圆槽标签）。
    /// </summary>
    public class MemoryPanel : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.84f, 0.2f);
        private static readonly Color Dim = new Color(0.62f, 0.62f, 0.66f);

        [SerializeField] private GameObject _root;

        [Tooltip("5 个锚点标签，按 Memory 石板的 5 个椭圆槽位摆放。")]
        [SerializeField] private TMP_Text[] _anchorLabels;

        [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;

        private bool _open;

        private void OnEnable()
        {
            EventBus.TargetsExtracted += OnTargetsChanged;
        }

        private void OnDisable()
        {
            EventBus.TargetsExtracted -= OnTargetsChanged;

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
                Debug.LogWarning("[AnchorHorror] MemoryPanel 未接线（_root 为空）：记忆页不可见，Tab 不暂停游戏。" +
                                 "请运行菜单 Ciga/AnchorHorror/生成可运行装配 重建场景。");
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
            // 接线缺失（极端异常）时判空降级：不接管 timeScale，避免"看不见面板还冻住游戏"。
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

        private void Refresh()
        {
            if (_anchorLabels == null)
            {
                return;
            }

            var gm = GameManager.Instance;
            var targets = gm != null && gm.Anchor != null ? gm.Anchor.Targets : null;
            var db = gm != null ? gm.Database : null;
            var backpack = gm != null ? gm.Backpack : null;

            for (int i = 0; i < _anchorLabels.Length; i++)
            {
                var label = _anchorLabels[i];
                if (label == null)
                {
                    continue;
                }

                if (targets != null && i < targets.Count)
                {
                    var t = targets[i];
                    label.text = db != null ? db.GetDisplayName(t.Feature) : t.Feature.ToString();
                    // 新模型：靠背包覆盖判定（Inventory.Covers），不靠 AnchorTarget.IsActivated（SC-5，陷阱 10）
                    label.color = backpack != null && backpack.Covers(t) ? Gold : Dim;
                    label.enabled = true;
                }
                else
                {
                    label.text = string.Empty;
                    label.enabled = false;
                }
            }
        }
    }
}
