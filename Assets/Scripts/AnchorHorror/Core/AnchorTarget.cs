// ------------------------------------------------------------
// AnchorTarget.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 一个目标锚点：需要拿取 RequiredCount 个具有 Feature 特征的物品才激活。
    /// 每个锚点独立计数；5 个全部激活即通关。运行时状态，普通类实例（不进 SO）。
    /// </summary>
    public class AnchorTarget
    {
        public FeatureUnit Feature { get; }

        /// <summary>需要匹配的数量（1~3，抽取时随机并 clamp 到场景实际数量）。</summary>
        public int RequiredCount { get; }

        /// <summary>已匹配数量。</summary>
        public int CurrentCount { get; private set; }

        public bool IsActivated => CurrentCount >= RequiredCount;

        public AnchorTarget(FeatureUnit feature, int requiredCount)
        {
            Feature = feature;
            RequiredCount = requiredCount;
            CurrentCount = 0;
        }

        /// <summary>命中一次，计数 +1。返回是否"因这次命中而"刚好激活。</summary>
        public bool Hit()
        {
            if (IsActivated)
            {
                return false;
            }

            CurrentCount++;
            return IsActivated;
        }
    }
}
