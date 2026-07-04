// ------------------------------------------------------------
// FeatureDatabase.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 特征词典（1 个实例）：特征 → 显示名 / 颜色。浮字、记忆面板文案都走这里，改文案不动代码。
    /// 查不到的特征回退用维度+值兜底（见 GetDisplayName）。
    /// </summary>
    [CreateAssetMenu(fileName = "FeatureDatabase", menuName = "Ciga/AnchorHorror/FeatureDatabase")]
    public class FeatureDatabase : ScriptableObject
    {
        [System.Serializable]
        public class FeatureEntry
        {
            [SerializeField] private FeatureDimension _dimension;
            [SerializeField] private int _value;
            [SerializeField] private string _displayName;
            [SerializeField] private Color _keywordColor = Color.white;
            [Tooltip("可选：该特征的音效（当前仅 Sound 维度用；空则匹配时回退程序化暖音）。")]
            [SerializeField] private AudioClip _clip;

            public FeatureDimension Dimension => _dimension;
            public int Value => _value;
            public string DisplayName => _displayName;
            public Color KeywordColor => _keywordColor;
            public AudioClip Clip => _clip;
        }

        [SerializeField] private List<FeatureEntry> _entries = new List<FeatureEntry>();

        /// <summary>取特征显示名；查不到则回退为对应枚举名（如 Red/Wood/Round/Smooth）。</summary>
        public string GetDisplayName(FeatureUnit unit)
        {
            var entry = Find(unit);
            if (entry != null && !string.IsNullOrEmpty(entry.DisplayName))
            {
                return entry.DisplayName;
            }

            return FallbackName(unit);
        }

        private static string FallbackName(FeatureUnit unit)
        {
            switch (unit.Dimension)
            {
                case FeatureDimension.Color: return ((FeatureColor)unit.Value).ToString();
                case FeatureDimension.Shape: return ((FeatureShape)unit.Value).ToString();
                case FeatureDimension.Material: return ((FeatureMaterial)unit.Value).ToString();
                case FeatureDimension.Texture: return ((FeatureTexture)unit.Value).ToString();
                case FeatureDimension.Sound: return ((FeatureSound)unit.Value).ToString();
                default: return unit.ToString();
            }
        }

        /// <summary>取特征关键词颜色；查不到则返回白色。</summary>
        public Color GetKeywordColor(FeatureUnit unit)
        {
            var entry = Find(unit);
            return entry != null ? entry.KeywordColor : Color.white;
        }

        /// <summary>取特征音效；查不到或未配置则返回 null（调用方回退程序化暖音）。当前仅 Sound 维度会配置。</summary>
        public AudioClip GetAudioClip(FeatureUnit unit)
        {
            var entry = Find(unit);
            return entry != null ? entry.Clip : null;
        }

        private FeatureEntry Find(FeatureUnit unit)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Dimension == unit.Dimension && e.Value == unit.Value)
                {
                    return e;
                }
            }

            return null;
        }
    }
}
