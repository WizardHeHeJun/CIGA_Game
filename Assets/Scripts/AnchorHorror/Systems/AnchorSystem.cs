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
    /// 锚点系统（模块最核心）：候选收集 → 目标抽取（含防死局 clamp）→ 匹配判定。
    /// 纯逻辑类，由 GameManager 注入依赖；数值改动通过注入的 SanitySystem 直接调用，不走 EventBus。
    /// </summary>
    public class AnchorSystem
    {
        private readonly GlobalConfig _config;
        private readonly LevelConfig _levelConfig;
        private readonly SanitySystem _sanity;

        private readonly HashSet<FeatureUnit> _candidateFeatures = new HashSet<FeatureUnit>();
        private int _candidateItemCount;

        private readonly List<AnchorTarget> _targets = new List<AnchorTarget>();

        // 复用缓冲，减少 TryMatch 的分配
        private readonly HashSet<FeatureUnit> _itemFeatureBuffer = new HashSet<FeatureUnit>();
        private readonly List<FeatureUnit> _hitBuffer = new List<FeatureUnit>();

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
        /// 过渡阶段：在关卡场景已加载并被 registry 扫描后调用。
        /// 从候选（+ 必要时 fallback）中抽 targetCount 个目标锚点，RequiredCount 随机并 clamp 到场景实际数量。
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
                required = Mathf.Clamp(required, 1, available); // ★clamp 防死局
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
        /// HorrorLevel 阶段：拿起一个物品做匹配。
        /// 命中≥1 特征 → 每个命中锚点 +1 且 San +MatchGain（可叠加）；不命中 → San -MismatchLoss。
        /// 决策 B：无论命中与否，触碰后物品即消耗，不能再交互 / 再扣分。
        /// </summary>
        public void TryMatch(FeatureTag item)
        {
            if (item == null || item.Consumed)
            {
                return;
            }

            _itemFeatureBuffer.Clear();
            _hitBuffer.Clear();

            var features = item.GetFeatures();
            for (int i = 0; i < features.Count; i++)
            {
                var u = features[i];
                if (!u.IsNone)
                {
                    _itemFeatureBuffer.Add(u);
                }
            }

            for (int i = 0; i < _targets.Count; i++)
            {
                var anchor = _targets[i];
                if (anchor.IsActivated)
                {
                    continue;
                }

                if (_itemFeatureBuffer.Contains(anchor.Feature))
                {
                    bool justActivated = anchor.Hit();
                    _hitBuffer.Add(anchor.Feature);
                    _sanity.Modify(_config.MatchGain);
                    if (justActivated)
                    {
                        EventBus.RaiseAnchorActivated(anchor);
                    }
                }
            }

            // 决策 B：触碰即消耗（命中/不命中都锁定，杜绝重复扣分）
            item.Consumed = true;

            if (_hitBuffer.Count > 0)
            {
                EventBus.RaiseItemMatched(item, _hitBuffer);
                if (AllActivated())
                {
                    EventBus.RaiseAllAnchorsActivated();
                }
            }
            else
            {
                _sanity.Modify(-_config.MismatchLoss);
                EventBus.RaiseItemMismatched(item);
            }
        }

        /// <summary>重开局清状态。</summary>
        public void Reset()
        {
            _candidateFeatures.Clear();
            _candidateItemCount = 0;
            _targets.Clear();
        }

        private bool AllActivated()
        {
            if (_targets.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _targets.Count; i++)
            {
                if (!_targets[i].IsActivated)
                {
                    return false;
                }
            }

            return true;
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
