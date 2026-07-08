// ------------------------------------------------------------
// BackpackPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.Collections.Generic;
using Ciga.Startup;
using Ciga.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡2 右侧背包：NightBag 羊皮纸 + 4 个物品槽图标（对齐美术 4 格），物品数超出可用上下箭头翻页。
    /// 图标用物品 Sprite，并按其首个非 None 特征的关键词色着色（BackpackItem 无独立颜色，
    /// 借 FeatureDatabase 词典色提升可读性）。仅 HorrorLevel 相位显示。
    /// </summary>
    public class BackpackPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;

        [Tooltip("物品槽图标，对齐 NightBag.PNG 的 4 个方框（从 _firstVisibleIndex 开始显示）。")]
        [SerializeField] private Image[] _slots;

        [Tooltip("溢出角标：背包件数 > 槽位数时显示当前页信息。")]
        [SerializeField] private TMP_Text _overflow;

        [Header("翻页按钮")]
        [SerializeField] private Button _pageUpButton;
        [SerializeField] private Button _pageDownButton;
        [SerializeField] private Image _pageUpImage;
        [SerializeField] private Image _pageDownImage;

        private int _firstVisibleIndex;
        private UiClickSfxBinder _clickSfxBinder;

        private void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
            EventBus.BackpackChanged += OnBackpackChanged;
            EventBus.ItemMatched += OnItemMatched;
            EventBus.ItemMismatched += OnItemMismatched;

            if (_pageUpButton != null)
            {
                _pageUpButton.onClick.AddListener(PageUp);
            }

            if (_pageDownButton != null)
            {
                _pageDownButton.onClick.AddListener(PageDown);
            }
        }

        private void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.BackpackChanged -= OnBackpackChanged;
            EventBus.ItemMatched -= OnItemMatched;
            EventBus.ItemMismatched -= OnItemMismatched;

            if (_pageUpButton != null)
            {
                _pageUpButton.onClick.RemoveListener(PageUp);
            }

            if (_pageDownButton != null)
            {
                _pageDownButton.onClick.RemoveListener(PageDown);
            }
        }

        private void Start()
        {
            if (_root == null)
            {
                Debug.LogWarning("[AnchorHorror] BackpackPanel 未接线（_root 为空）：背包不可见。" +
                                 "请运行菜单 Ciga/AnchorHorror/生成可运行装配 重建场景。");
            }

            EnsureClickSfxBinder();
            SetVisible(false);
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            bool show = newPhase == GamePhase.HorrorLevel;
            SetVisible(show);
            if (show)
            {
                _firstVisibleIndex = 0;
                Refresh();
            }
        }

        private void OnBackpackChanged(Inventory backpack)
        {
            Refresh();
        }

        private void OnItemMatched(FeatureTag item, IReadOnlyList<FeatureUnit> hits)
        {
            Refresh();
        }

        private void OnItemMismatched(FeatureTag item)
        {
            Refresh();
        }

        private void PageUp()
        {
            int pageSize = SlotCount;
            if (pageSize <= 0)
            {
                return;
            }

            _firstVisibleIndex = Mathf.Max(0, _firstVisibleIndex - pageSize);
            Refresh();
        }

        private void PageDown()
        {
            var backpack = CurrentBackpack;
            int pageSize = SlotCount;
            if (backpack == null || pageSize <= 0)
            {
                return;
            }

            int maxFirst = MaxFirstIndex(backpack.Count, pageSize);
            _firstVisibleIndex = Mathf.Min(maxFirst, _firstVisibleIndex + pageSize);
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
            int pageSize = SlotCount;

            if (pageSize > 0)
            {
                _firstVisibleIndex = Mathf.Clamp(_firstVisibleIndex, 0, MaxFirstIndex(count, pageSize));
            }
            else
            {
                _firstVisibleIndex = 0;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                int itemIndex = _firstVisibleIndex + i;
                if (backpack != null && itemIndex < backpack.Items.Count)
                {
                    var bi = backpack.Items[itemIndex];
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

            RefreshPageControls(count, pageSize);
        }

        private void RefreshPageControls(int count, int pageSize)
        {
            bool canPage = pageSize > 0 && count > pageSize;
            bool canUp = canPage && _firstVisibleIndex > 0;
            bool canDown = canPage && _firstVisibleIndex + pageSize < count;

            if (_pageUpButton != null)
            {
                _pageUpButton.interactable = canUp;
            }

            if (_pageDownButton != null)
            {
                _pageDownButton.interactable = canDown;
            }

            if (_pageUpImage != null)
            {
                _pageUpImage.enabled = canPage;
                _pageUpImage.color = canUp ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }

            if (_pageDownImage != null)
            {
                _pageDownImage.enabled = canPage;
                _pageDownImage.color = canDown ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }

            if (_overflow != null)
            {
                if (canPage)
                {
                    int page = _firstVisibleIndex / pageSize + 1;
                    int totalPages = (count + pageSize - 1) / pageSize;
                    _overflow.text = page + "/" + totalPages;
                    _overflow.enabled = true;
                }
                else if (count > 0 && pageSize > 0)
                {
                    _overflow.text = count + "/" + pageSize;
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

        private Inventory CurrentBackpack
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null ? gm.Backpack : null;
            }
        }

        private int SlotCount => _slots != null ? _slots.Length : 0;

        private static int MaxFirstIndex(int count, int pageSize)
        {
            if (count <= pageSize || pageSize <= 0)
            {
                return 0;
            }

            return ((count - 1) / pageSize) * pageSize;
        }

        private void EnsureClickSfxBinder()
        {
            if (_clickSfxBinder == null)
            {
                _clickSfxBinder = GetComponent<UiClickSfxBinder>();
                if (_clickSfxBinder == null)
                {
                    _clickSfxBinder = gameObject.AddComponent<UiClickSfxBinder>();
                }
            }

            if (_clickSfxBinder == null)
            {
                return;
            }

            var loader = SceneLoader.Instance;
            _clickSfxBinder.SetClickClip(loader != null ? loader.UiClickClip : null);
            _clickSfxBinder.RefreshButtons();
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
