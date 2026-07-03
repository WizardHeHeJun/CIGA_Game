// ------------------------------------------------------------
// LevelFeatureRegistry.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡特征登记：扫描关卡场景内所有 FeatureTag，统计每个特征（FeatureUnit）对应的物品数量。
    /// 供 AnchorSystem 抽取锚点时校验数量、clamp RequiredCount，杜绝死局（设计决策 C）。
    /// 普通类实例，由 GameManager 在关卡加载后调用 Scan。
    /// </summary>
    public class LevelFeatureRegistry
    {
        private readonly Dictionary<FeatureUnit, int> _featureCount = new Dictionary<FeatureUnit, int>();

        public IReadOnlyCollection<FeatureUnit> AllFeatures => _featureCount.Keys;

        /// <summary>扫描指定（已加载的）场景，统计特征数量。会先清空旧数据。</summary>
        public void Scan(Scene scene)
        {
            _featureCount.Clear();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning($"[AnchorHorror] LevelFeatureRegistry.Scan: 场景无效或未加载 ({scene.name})");
                return;
            }

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var tags = roots[i].GetComponentsInChildren<FeatureTag>(includeInactive: true);
                for (int j = 0; j < tags.Length; j++)
                {
                    Accumulate(tags[j]);
                }
            }
        }

        /// <summary>直接从一组 FeatureTag 统计（便于 EditMode 测试，不依赖场景）。</summary>
        public void Scan(IReadOnlyList<FeatureTag> tags)
        {
            _featureCount.Clear();
            if (tags == null)
            {
                return;
            }

            for (int i = 0; i < tags.Count; i++)
            {
                Accumulate(tags[i]);
            }
        }

        /// <summary>某特征在关卡里的物品数量；不存在返回 0。None 特征恒返回 0。</summary>
        public int CountOf(FeatureUnit unit)
        {
            if (unit.IsNone)
            {
                return 0;
            }

            return _featureCount.TryGetValue(unit, out int count) ? count : 0;
        }

        private void Accumulate(FeatureTag tag)
        {
            if (tag == null)
            {
                return;
            }

            var features = tag.GetFeatures();
            for (int i = 0; i < features.Count; i++)
            {
                var unit = features[i];
                if (unit.IsNone)
                {
                    continue;
                }

                _featureCount.TryGetValue(unit, out int c);
                _featureCount[unit] = c + 1;
            }
        }
    }
}
