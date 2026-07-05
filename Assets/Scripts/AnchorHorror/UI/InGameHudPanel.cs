// ------------------------------------------------------------
// InGameHudPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡2（HorrorLevel）常驻画面 HUD：边框（Frame）+ 顶部命中数
    /// （No Collect 底图 + Collected1..5 叠加图标）。命中数 = 背包覆盖的锚点数
    /// （Inventory.Covers，不用 AnchorTarget.IsActivated，陷阱10），命中 k 个则点亮前 k 个图标。
    /// 仅 HorrorLevel 相位显示，其余相位隐藏（镜像 CountdownPanel）。
    /// 所有图为全屏叠加层（美术按 1920x1080 定位，自动对齐），本组件只切显隐、不摆坐标。
    /// </summary>
    public class InGameHudPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;

        [Tooltip("Collected1..5 叠加图标（各为一颗填充菱形）；命中 k 个则显示前 k 个。")]
        [SerializeField] private Image[] _collectedIcons;

        private int _lastHit = -1;

        private void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
            EventBus.TargetsExtracted += OnTargetsExtracted;
            EventBus.ItemMatched += OnItemMatched;
            EventBus.ItemMismatched += OnItemMismatched;
            EventBus.AllAnchorsActivated += OnAllActivated;
        }

        private void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.TargetsExtracted -= OnTargetsExtracted;
            EventBus.ItemMatched -= OnItemMatched;
            EventBus.ItemMismatched -= OnItemMismatched;
            EventBus.AllAnchorsActivated -= OnAllActivated;
        }

        private void Start()
        {
            if (_root == null)
            {
                Debug.LogWarning("[AnchorHorror] InGameHudPanel 未接线（_root 为空）：边框/命中数 HUD 不可见。" +
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
                _lastHit = -1; // 相位切入时强制刷新
                RefreshHit();
            }
        }

        private void OnTargetsExtracted(IReadOnlyList<AnchorTarget> targets)
        {
            _lastHit = -1;
            RefreshHit();
        }

        private void OnItemMatched(FeatureTag item, IReadOnlyList<FeatureUnit> hits)
        {
            RefreshHit();
        }

        private void OnItemMismatched(FeatureTag item)
        {
            RefreshHit();
        }

        private void OnAllActivated()
        {
            RefreshHit();
        }

        /// <summary>命中数 = 背包覆盖的锚点数；仅在数值变化时切换 Collected 图标显隐。</summary>
        private void RefreshHit()
        {
            if (_collectedIcons == null)
            {
                return;
            }

            var gm = GameManager.Instance;
            int hit = 0;
            if (gm != null && gm.Anchor != null)
            {
                var targets = gm.Anchor.Targets;
                var backpack = gm.Backpack;
                if (targets != null && backpack != null)
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        if (backpack.Covers(targets[i]))
                        {
                            hit++;
                        }
                    }
                }
            }

            if (hit == _lastHit)
            {
                return;
            }

            _lastHit = hit;
            for (int i = 0; i < _collectedIcons.Length; i++)
            {
                if (_collectedIcons[i] != null)
                {
                    _collectedIcons[i].enabled = i < hit;
                }
            }
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
