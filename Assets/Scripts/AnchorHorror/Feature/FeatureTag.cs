// ------------------------------------------------------------
// FeatureTag.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 挂在每个可交互物品上的特征标签：四维枚举 + 是否已消耗。
    /// 纯数据组件（零业务依赖），最底层。需 Collider2D 以供 2D 交互检测。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class FeatureTag : MonoBehaviour
    {
        [Header("四维特征")]
        [SerializeField] private FeatureColor _color;
        [SerializeField] private FeatureShape _shape;
        [SerializeField] private FeatureMaterial _material;
        [SerializeField] private FeatureTexture _texture;

        [Header("高亮（可选，缺省自动取本体 SpriteRenderer）")]
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0.6f, 1f);

        private readonly List<FeatureUnit> _features = new List<FeatureUnit>(4);
        private Color _baseColor = Color.white;
        private bool _highlighted;

        /// <summary>关卡中已交互（命中或不命中）的物品置 true，之后不可再交互。见设计决策 B。</summary>
        public bool Consumed { get; set; }

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_renderer != null)
            {
                _baseColor = _renderer.color;
            }

            RebuildFeatures();
        }

        /// <summary>返回该物品的 4 个特征（缓存，避免每次交互重新分配）。</summary>
        public IReadOnlyList<FeatureUnit> GetFeatures()
        {
            // 惰性兜底：EditMode / 单元测试下 Awake 不一定跑过，首次访问时按当前枚举值构建。
            if (_features.Count == 0)
            {
                RebuildFeatures();
            }

            return _features;
        }

        /// <summary>切换高亮（占位实现：改 SpriteRenderer 颜色）。</summary>
        public void SetHighlight(bool on)
        {
            if (_highlighted == on || _renderer == null)
            {
                return;
            }

            _highlighted = on;
            _renderer.color = on ? _highlightColor : _baseColor;
        }

        private void RebuildFeatures()
        {
            _features.Clear();
            _features.Add(new FeatureUnit(FeatureDimension.Color, (int)_color));
            _features.Add(new FeatureUnit(FeatureDimension.Shape, (int)_shape));
            _features.Add(new FeatureUnit(FeatureDimension.Material, (int)_material));
            _features.Add(new FeatureUnit(FeatureDimension.Texture, (int)_texture));
        }

#if UNITY_EDITOR
        // 编辑器下改枚举即时刷新缓存，方便调试；不影响运行时热路径。
        private void OnValidate()
        {
            RebuildFeatures();
        }
#endif
    }
}
