// ------------------------------------------------------------
// BackpackPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡2 右侧背包：Bag 羊皮纸 + 4 个物品槽图标（对齐美术 4 格），物品数超出用 "+N" 角标。
    /// 图标用物品 Sprite，并按其首个非 None 特征的关键词色着色（BackpackItem 无独立颜色，
    /// 借 FeatureDatabase 词典色提升可读性）。背包无变更事件，靠 ItemMatched/ItemMismatched +
    /// 显示时轮询刷新（陷阱：无 backpack-changed 事件）。仅 HorrorLevel 相位显示。
    /// </summary>
    public class BackpackPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;

        [Tooltip("物品槽图标，对齐 Bag.PNG 的 4 个方框（前 N 件背包物品）。")]
        [SerializeField] private Image[] _slots;

        [Tooltip("溢出角标：背包件数 > 槽位数时显示 +N。")]
        [SerializeField] private TMP_Text _overflow;

        private void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
            EventBus.ItemMatched += OnItemMatched;
            EventBus.ItemMismatched += OnItemMismatched;
        }

        private void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.ItemMatched -= OnItemMatched;
            EventBus.ItemMismatched -= OnItemMismatched;
        }

        private void Start()
        {
            if (_root == null)
            {
                Debug.LogWarning("[AnchorHorror] BackpackPanel 未接线（_root 为空）：背包不可见。" +
                                 "请运行菜单 Ciga/AnchorHorror/生成可运行装配 重建场景。");
            }

            SetVisible(false);
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            bool show = newPhase == GamePhase.HorrorLevel;
            SetVisible(show);
            if (show)
            {
                Refresh();
            }
        }

        private void OnItemMatched(FeatureTag item, IReadOnlyList<FeatureUnit> hits)
        {
            Refresh();
        }

        private void OnItemMismatched(FeatureTag item)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_slots == null)
            {
                return;
            }

            var gm = GameManager.Instance;
            var backpack = gm != null ? gm.Backpack : null;
            var db = gm != null ? gm.Database : null;
            int count = backpack != null ? backpack.Count : 0;

            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (backpack != null && i < backpack.Items.Count)
                {
                    var bi = backpack.Items[i];
                    slot.sprite = bi.Sprite;
                    slot.enabled = bi.Sprite != null;
                    slot.color = TintFor(bi, db);
                }
                else
                {
                    slot.sprite = null;
                    slot.enabled = false;
                }
            }

            if (_overflow != null)
            {
                int extra = count - _slots.Length;
                if (extra > 0)
                {
                    _overflow.text = "+" + extra;
                    _overflow.enabled = true;
                }
                else
                {
                    _overflow.enabled = false;
                }
            }
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

        private void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }
    }
}
