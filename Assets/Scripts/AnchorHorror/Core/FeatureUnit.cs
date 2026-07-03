// ------------------------------------------------------------
// FeatureUnit.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 单个特征单元（维度 + 值）。作为匹配 / 字典键 / 集合元素的统一比较单位。
    /// 值用 int 承载各维度枚举，比较只看 (维度, 值)。
    /// </summary>
    public readonly struct FeatureUnit : IEquatable<FeatureUnit>
    {
        public readonly FeatureDimension Dimension;
        public readonly int Value;

        public FeatureUnit(FeatureDimension dimension, int value)
        {
            Dimension = dimension;
            Value = value;
        }

        /// <summary>该维度取值是否为 None（约定：枚举 None 恒为 0）。抽锚点时用于剔除。</summary>
        public bool IsNone => Value == 0;

        public bool Equals(FeatureUnit other)
        {
            return Dimension == other.Dimension && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is FeatureUnit other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Dimension << 8) ^ Value;
        }

        public static bool operator ==(FeatureUnit a, FeatureUnit b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(FeatureUnit a, FeatureUnit b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return $"{Dimension}:{Value}";
        }
    }
}
