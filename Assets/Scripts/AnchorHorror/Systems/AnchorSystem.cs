// ------------------------------------------------------------
// AnchorSystem.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 锚点系统（模块最核心）：候选收集 → 目标抽取（含防死局 clamp）→ 满足判定。
    /// 纯逻辑类，由 GameManager 注入依赖。
    /// 关卡2 匹配改由 Inventory.Satisfies 判定，TryMatch 已删除（ADR-6）。
    /// </summary>
    public class AnchorSystem
    {
        private readonly GlobalConfig _config;
        private readonly LevelConfig _levelConfig;
        private readonly SanitySystem _sanity;

        private readonly HashSet<FeatureUnit> _candidateFeatures = new HashSet<FeatureUnit>();
        private int _candidateItemCount;

        private readonly List<AnchorTarget> _targets = new List<AnchorTarget>();

        // 复用缓冲，减少 ExtractTargets 路径的分配
        private readonly HashSet<FeatureUnit> _itemFeatureBuffer = new HashSet<FeatureUnit>();

        public AnchorSystem(GlobalConfig config, LevelConfig levelConfig, SanitySystem sanity)
        {
            _config = config;
            _levelConfig = levelConfig;
            _sanity = sanity;
        }

        public IReadOnlyList<AnchorTarget> Targets => _targets;

        /// <summary>已交互的候选物品件数（用于 InitRoom 的 8 件阈值判定）。</summary>
        public int CandidateItemCount => _candidateItemCount;

        /// <summary>InitRoom 阶段：交互一个物品，记录其特征进候选（特征去重，件数累加）。</summary>
        public void CollectCandidate(FeatureTag item)
        {
            if (item == null || item.Consumed)
            {
                return;
            }

            _candidateItemCount++;

            var features = item.GetFeatures();
            for (int i = 0; i < features.Count; i++)
            {
                var unit = features[i];
                if (!unit.IsNone)
                {
                    _candidateFeatures.Add(unit);
                }
            }

            EventBus.RaiseCandidateCollected(_candidateItemCount);
        }

        /// <summary>
        /// 过渡阶段（旧 additive/registry 路径）：在关卡场景已加载并被 registry 扫描后调用。
        /// 从候选（+ 必要时 fallback）中抽 targetCount 个目标锚点，RequiredCount 随机并 clamp 到场景实际数量。
        /// 注：新两关卡流程改用 ExtractTargetsFromSelection，此方法仅保留供旧代码路径兼容。
        /// </summary>
        public void ExtractTargets(LevelFeatureRegistry registry)
        {
            _targets.Clear();

            // 1. 候选中"场景里找得到且通过白名单"的特征
            var pool = new List<FeatureUnit>();
            foreach (var f in _candidateFeatures)
            {
                if (registry.CountOf(f) >= 1 && IsAllowed(f))
                {
                    pool.Add(f);
                }
            }

            // 2. 不足 targetCount → 从 fallbackFeaturePool 补齐（同样校验数量、去重）
            if (pool.Count < _config.TargetCount && _levelConfig != null)
            {
                var fallback = _levelConfig.FallbackFeaturePool;
                for (int i = 0; i < fallback.Count && pool.Count < _config.TargetCount; i++)
                {
                    var f = fallback[i].ToUnit();
                    if (!f.IsNone && registry.CountOf(f) >= 1 && IsAllowed(f) && !pool.Contains(f))
                    {
                        pool.Add(f);
                    }
                }
            }

            // 3. 洗牌后取前 targetCount 个
            Shuffle(pool);
            int take = Mathf.Min(_config.TargetCount, pool.Count);
            for (int i = 0; i < take; i++)
            {
                var feature = pool[i];
                int available = registry.CountOf(feature);
                int required = Random.Range(_config.RequiredCountMin, _config.RequiredCountMax + 1);
                required = Mathf.Clamp(required, 1, available); // clamp 防死局
                _targets.Add(new AnchorTarget(feature, required));
            }

            if (_targets.Count < _config.TargetCount)
            {
                Debug.LogWarning(
                    $"[AnchorHorror] 目标锚点仅抽到 {_targets.Count}/{_config.TargetCount}（候选+保底不足），降级为有几个抽几个。");
            }

            EventBus.RaiseTargetsExtracted(_targets);
        }

        /// <summary>
        /// 关卡1 锁定时调用（ADR-4，陷阱 2）：从已选物品特征去重取 distinct，
        /// 抽 TargetCount(5) 个目标锚点，RequiredCount 恒为 1，不依赖 registry。
        /// 切走廊/房间时绝不调用（SC-2，陷阱 3）。
        /// </summary>
        public void ExtractTargetsFromSelection(IReadOnlyList<BackpackItem> selected)
        {
            ExtractTargetsFromSelection(selected, null);
        }

        /// <summary>
        /// 同上，另接收"关卡2 可获得特征集合"做死局防护：
        /// 已选物品的特征若在第二关任何场景都拿不到，则不进抽取池（抽中即必输，无物品可满足）。
        /// obtainable 传 null 或空集合时不过滤（向后兼容 / 测试路径）。
        /// </summary>
        public void ExtractTargetsFromSelection(IReadOnlyList<BackpackItem> selected, HashSet<FeatureUnit> obtainable)
        {
            _targets.Clear();

            bool filter = obtainable != null && obtainable.Count > 0;

            // 1. 从已选物品收集所有 distinct 非-None 特征（可选剔除关卡2拿不到的）
            var pool = new List<FeatureUnit>();
            var dropped = filter ? new HashSet<FeatureUnit>() : null;
            for (int i = 0; i < selected.Count; i++)
            {
                var features = selected[i].Features;
                for (int f = 0; f < features.Count; f++)
                {
                    var unit = features[f];
                    if (unit.IsNone || pool.Contains(unit))
                    {
                        continue;
                    }

                    if (filter && !obtainable.Contains(unit))
                    {
                        dropped.Add(unit);
                        continue;
                    }

                    pool.Add(unit);
                }
            }

            if (dropped != null && dropped.Count > 0)
            {
                Debug.LogWarning(
                    $"[AnchorHorror] ExtractTargetsFromSelection：剔除 {dropped.Count} 个关卡2无物品可满足的特征" +
                    $"（{string.Join(", ", dropped)}）。建议补齐第二关物品特征覆盖。");
            }

            // 2. 洗牌后取前 TargetCount 个
            Shuffle(pool);
            int take = Mathf.Min(_config.TargetCount, pool.Count);
            for (int i = 0; i < take; i++)
            {
                // RequiredCount 恒 1（ADR-4，新模型靠 Inventory.Satisfies 覆盖判定）
                _targets.Add(new AnchorTarget(pool[i], 1));
            }

            if (_targets.Count < _config.TargetCount)
            {
                Debug.LogWarning(
                    $"[AnchorHorror] ExtractTargetsFromSelection：仅抽到 {_targets.Count}/{_config.TargetCount} 个锚点" +
                    $"（已选物品 distinct 特征不足）。请确保关卡1 物品特征种类 ≥ {_config.TargetCount}。");
            }

            EventBus.RaiseTargetsExtracted(_targets);
        }

        /// <summary>重开局清状态。</summary>
        public void Reset()
        {
            _candidateFeatures.Clear();
            _candidateItemCount = 0;
            _targets.Clear();
        }

        private bool IsAllowed(FeatureUnit unit)
        {
            var allowed = _levelConfig != null ? _levelConfig.AllowedFeatures : null;
            if (allowed == null || allowed.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < allowed.Count; i++)
            {
                if (allowed[i].ToUnit() == unit)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Shuffle(List<FeatureUnit> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
