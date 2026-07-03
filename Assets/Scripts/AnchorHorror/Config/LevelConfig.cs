// ------------------------------------------------------------
// LevelConfig.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 因 FeatureUnit 是 readonly struct 不便直接在 Inspector 编辑，用本类型中转序列化。
    /// </summary>
    [System.Serializable]
    public struct SerializableFeatureUnit
    {
        [SerializeField] private FeatureDimension _dimension;
        [SerializeField] private int _value;

        public FeatureUnit ToUnit()
        {
            return new FeatureUnit(_dimension, _value);
        }
    }

    /// <summary>
    /// 每关一个：候选不足 5 个时的保底特征池，以及可选的允许特征白名单。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Ciga/AnchorHorror/LevelConfig")]
    public class LevelConfig : ScriptableObject
    {
        [SerializeField] private string _levelName;

        [Tooltip("候选<5 时从这里补齐目标锚点（仍会按场景实际数量校验）。")]
        [SerializeField] private List<SerializableFeatureUnit> _fallbackFeaturePool = new List<SerializableFeatureUnit>();

        [Tooltip("可选：本关允许出现的特征白名单，空则不限制。")]
        [SerializeField] private List<SerializableFeatureUnit> _allowedFeatures = new List<SerializableFeatureUnit>();

        public string LevelName => _levelName;

        public IReadOnlyList<SerializableFeatureUnit> FallbackFeaturePool => _fallbackFeaturePool;
        public IReadOnlyList<SerializableFeatureUnit> AllowedFeatures => _allowedFeatures;
    }
}
