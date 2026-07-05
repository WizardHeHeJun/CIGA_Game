// ------------------------------------------------------------
// Inventory.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 拾取时从 FeatureTag 拷出的背包物品记录（值快照，独立于场景对象生命周期）。
    /// 子场景切换 Destroy(_levelRoot) 后背包内容依然完整（ADR-2，陷阱 1）。
    /// </summary>
    public sealed class BackpackItem
    {
        /// <summary>拾取瞬间从 FeatureTag.GetFeatures() 拷出的特征列表（只读）。</summary>
        public IReadOnlyList<FeatureUnit> Features { get; }

        /// <summary>物品 Sprite（可为 null）。</summary>
        public Sprite Sprite { get; }

        /// <summary>物品唯一 ID（ItemDefinition.Id 或 GameObject 名）。</summary>
        public string Id { get; }

        public BackpackItem(IReadOnlyList<FeatureUnit> features, Sprite sprite, string id)
        {
            // 拷贝特征列表，确保独立于 FeatureTag 实例
            var copied = new FeatureUnit[features != null ? features.Count : 0];
            if (features != null)
            {
                for (int i = 0; i < features.Count; i++)
                {
                    copied[i] = features[i];
                }
            }
            Features = copied;
            Sprite = sprite;
            Id = id ?? string.Empty;
        }
    }

    /// <summary>
    /// 背包：持 BackpackItem 值记录列表，按 Capacity 限制数量。
    /// 普通类（不进 SO/MonoBehaviour），运行时状态由 GameManager 持有（ADR-2）。
    /// Satisfies：每个 AnchorTarget 至少有一个背包物品含 target.Feature → 通关判定。
    /// Covers：单锚点是否被背包覆盖（供 MemoryPanel 逐条显示满足态）。
    /// </summary>
    public sealed class Inventory
    {
        private readonly List<BackpackItem> _items = new List<BackpackItem>();

        /// <summary>背包容量上限：关卡1=5，关卡2=8，切关时由 GameManager 设置。</summary>
        public int Capacity { get; set; }

        /// <summary>当前物品件数。</summary>
        public int Count => _items.Count;

        /// <summary>当前背包内容（只读视图）。</summary>
        public IReadOnlyList<BackpackItem> Items => _items;

        /// <summary>
        /// 尝试将 FeatureTag 加入背包（拾取瞬间拷贝特征快照）。
        /// 满（Count >= Capacity）或 item 为 null 时返回 false，不加。
        /// </summary>
        public bool TryAdd(FeatureTag item)
        {
            if (item == null)
            {
                return false;
            }

            if (_items.Count >= Capacity)
            {
                return false;
            }

            // 取 Sprite：优先 SpriteRenderer；无则 null
            var sr = item.GetComponent<SpriteRenderer>();
            var sprite = sr != null ? sr.sprite : null;

            var backpackItem = new BackpackItem(item.GetFeatures(), sprite, item.name);
            _items.Add(backpackItem);
            return true;
        }

        /// <summary>清空背包（关卡1→关卡2 过渡时调用，SC-3）。</summary>
        public void Clear()
        {
            _items.Clear();
        }

        /// <summary>取第一件覆盖指定锚点的背包物品；UI 用于在记忆石板上显示“是哪件物品满足了它”。</summary>
        public bool TryGetCoveringItem(AnchorTarget target, out BackpackItem item)
        {
            item = null;
            if (target == null)
            {
                return false;
            }

            var feature = target.Feature;
            for (int i = 0; i < _items.Count; i++)
            {
                var features = _items[i].Features;
                for (int f = 0; f < features.Count; f++)
                {
                    if (!features[f].IsNone && features[f] == feature)
                    {
                        item = _items[i];
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 判定背包是否满足全部锚点目标（SC-5）：
        /// 每个 target，背包中至少存在一个物品含 target.Feature（RequiredCount 恒为 1）。
        /// </summary>
        public bool Satisfies(IReadOnlyList<AnchorTarget> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return false;
            }

            for (int t = 0; t < targets.Count; t++)
            {
                if (!Covers(targets[t]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 判定单个锚点是否被背包中某件物品覆盖（背包中 ∃ item 含 target.Feature）。
        /// 供 MemoryPanel 逐条显示"已满足/未满足"（陷阱：不使用 AnchorTarget.IsActivated，新模型不靠 Hit 累计）。
        /// </summary>
        public bool Covers(AnchorTarget target)
        {
            if (target == null)
            {
                return false;
            }

            var feature = target.Feature;
            for (int i = 0; i < _items.Count; i++)
            {
                var features = _items[i].Features;
                for (int f = 0; f < features.Count; f++)
                {
                    if (!features[f].IsNone && features[f] == feature)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
