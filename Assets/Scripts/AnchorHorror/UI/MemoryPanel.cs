// ------------------------------------------------------------
// MemoryPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 记忆页签：Tab 开关，打开时暂停（Time.timeScale=0）。在 Memory 石板的 5 个椭圆槽里
    /// 显示从随机池抽得的 5 个锚点标签（中文，FeatureDatabase.GetDisplayName）。
    /// 已被背包覆盖的锚点显金色并显示第一件覆盖它的背包物品图标，未覆盖显暗色。
    /// 面板由生成器接线（_root=全屏石板层，_anchorLabels=5 个椭圆槽标签，_coveredItemIcons=5 个物品图标）。
    /// </summary>
    public class MemoryPanel : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.84f, 0.2f);
        private static readonly Color Dim = new Color(0.62f, 0.62f, 0.66f);

        [SerializeField] private GameObject _root;

        [Tooltip("5 个锚点标签，按 Memory 石板的 5 个椭圆槽位摆放。")]
        [SerializeField] private TMP_Text[] _anchorLabels;

        [Tooltip("5 个锚点对应的已满足物品图标。背包覆盖该锚点时显示第一件覆盖物品。")]
        [SerializeField] private Image[] _coveredItemIcons;

        [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;

        private bool _open;

        private void OnEnable()
        {
            EventBus.TargetsExtracted += OnTargetsChanged;
            EventBus.BackpackChanged += OnBackpackChanged;
            EventBus.ItemMatched += OnItemMatched;
            EventBus.ItemMismatched += OnItemMismatched;
        }

        private void OnDisable()
        {
            EventBus.TargetsExtracted -= OnTargetsChanged;
            EventBus.BackpackChanged -= OnBackpackChanged;
            EventBus.ItemMatched -= OnItemMatched;
            EventBus.ItemMismatched -= OnItemMismatched;

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
            if (open)
            {
                _root.transform.SetAsLastSibling();
            }

            _root.SetActive(open);
            Time.timeScale = open ? 0f : 1f;
            if (open)
            {
                Refresh();
            }
        }

        private void OnTargetsChanged(IReadOnlyList<AnchorTarget> targets)
        {
            RefreshIfOpen();
        }

        private void OnBackpackChanged(Inventory backpack)
        {
            RefreshIfOpen();
        }

        private void OnItemMatched(FeatureTag item, IReadOnlyList<FeatureUnit> hits)
        {
            RefreshIfOpen();
        }

        private void OnItemMismatched(FeatureTag item)
        {
            RefreshIfOpen();
        }

        private void RefreshIfOpen()
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
                    bool covered = backpack != null && backpack.Covers(t);
                    label.text = db != null ? db.GetDisplayName(t.Feature) : t.Feature.ToString();
                    // 新模型：靠背包覆盖判定（Inventory.Covers），不靠 AnchorTarget.IsActivated（SC-5，陷阱 10）
                    label.color = covered ? Gold : Dim;
                    label.enabled = true;
                    RefreshCoveredIcon(i, covered, t, backpack, db);
                }
                else
                {
                    label.text = string.Empty;
                    label.enabled = false;
                    ClearCoveredIcon(i);
                }
            }
        }

        private void RefreshCoveredIcon(int index, bool covered, AnchorTarget target, Inventory backpack, FeatureDatabase db)
        {
            var icon = IconAt(index);
            if (icon == null)
            {
                return;
            }

            if (!covered || backpack == null || !backpack.TryGetCoveringItem(target, out var item) || item == null)
            {
                ClearIcon(icon);
                return;
            }

            icon.sprite = item.Sprite;
            icon.enabled = item.Sprite != null;
            icon.color = TintFor(item, db);
        }

        private void ClearCoveredIcon(int index)
        {
            var icon = IconAt(index);
            if (icon != null)
            {
                ClearIcon(icon);
            }
        }

        private Image IconAt(int index)
        {
            if (_coveredItemIcons == null || index < 0 || index >= _coveredItemIcons.Length)
            {
                return null;
            }

            return _coveredItemIcons[index];
        }

        private static void ClearIcon(Image icon)
        {
            icon.sprite = null;
            icon.enabled = false;
            icon.color = Color.white;
        }

        /// <summary>取物品首个非 None 特征的关键词色作为图标着色（无则白）。</summary>
        private static Color TintFor(BackpackItem bi, FeatureDatabase db)
        {
            if (db == null || bi == null)
            {
                return Color.white;
            }

            for (int f = 0; f < bi.Features.Count; f++)
            {
                if (!bi.Features[f].IsNone)
                {
                    return db.GetKeywordColor(bi.Features[f]);
                }
            }

            return Color.white;
        }
    }
}
