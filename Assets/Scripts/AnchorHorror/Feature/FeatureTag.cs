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
    /// 挂在每个可交互物品上的特征标签：N 维枚举 + 是否已消耗。
    /// 实现 IInteractable：InitRoom/HorrorLevel 阶段且未消耗时可交互；
    /// Interact 内联原 InteractionSystem 的 Collect/TryMatch 分派（ADR-2）。需 Collider2D 以供 2D 交互检测。
    /// 维度字段（_color/.../_sound）、只读属性、BuildFeaturesGenerated 实现在 FeatureTag.Generated.cs
    /// （由 AnchorFeatures.csv 生成，与本文件同提交；加维度只改 CSV 重生成，本文件不动）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public partial class FeatureTag : MonoBehaviour, IInteractable
    {
        [Header("高亮（可选，缺省自动取本体 SpriteRenderer）")]
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private UnityEngine.Color _highlightColor = new UnityEngine.Color(1f, 1f, 0.6f, 1f);

        private readonly List<FeatureUnit> _features = new List<FeatureUnit>(8);
        private UnityEngine.Color _baseColor = UnityEngine.Color.white;
        private bool _highlighted;

        /// <summary>关卡中已交互（命中或不命中）的物品置 true，之后不可再交互。见设计决策 B。</summary>
        public bool Consumed { get; set; }

        /// <summary>生成半（FeatureTag.Generated.cs）实现：逐维度把当前枚举值装配进 buffer。</summary>
        partial void BuildFeaturesGenerated(List<FeatureUnit> buffer);

        /// <summary>
        /// 运行时安全写入特征（幂等，后向兼容 4 维签名，声音取 None）。
        /// ItemFactory 装配后调用；编辑器预览也走此路径，避免两套写法漂移。
        /// </summary>
        public void Configure(FeatureColor color, FeatureShape shape, FeatureMaterial material, FeatureTexture texture)
        {
            Configure(color, shape, material, texture, FeatureSound.None);
        }

        /// <summary>运行时安全写入 5 维特征（含声音，幂等）。设私有字段后调 RebuildFeatures 重建缓存。</summary>
        public void Configure(FeatureColor color, FeatureShape shape, FeatureMaterial material, FeatureTexture texture, FeatureSound sound)
        {
            _color = color;
            _shape = shape;
            _material = material;
            _texture = texture;
            _sound = sound;
            RebuildFeatures();
        }

        /// <summary>
        /// 按 (维度,值) 列表通用写入特征：反射到生成的 _&lt;dim&gt; 字段，天然容纳 CSV 新增/移除维度。
        /// 仅编辑器动态维度编辑路径调用（非运行时热路径）。写完 RebuildFeatures 重建缓存。
        /// </summary>
        public void SetFeatures(IReadOnlyList<FeatureUnit> features)
        {
            if (features == null)
            {
                return;
            }

            var type = typeof(FeatureTag); // 用具体类型而非 GetType()：字段在本类的 generated partial，防子类反射漏字段
            for (int i = 0; i < features.Count; i++)
            {
                var unit = features[i];
                string dimKey = unit.Dimension.ToString();
                string fieldName = "_" + char.ToLowerInvariant(dimKey[0]) + dimKey.Substring(1);
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null && field.FieldType.IsEnum)
                {
                    field.SetValue(this, System.Enum.ToObject(field.FieldType, unit.Value));
                }
            }

            RebuildFeatures();
        }

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

        /// <summary>返回该物品的各维特征（缓存，避免每次交互重新分配）。</summary>
        public IReadOnlyList<FeatureUnit> GetFeatures()
        {
            // 惰性兜底：EditMode / 单元测试下 Awake 不一定跑过，首次访问时按当前枚举值构建。
            if (_features.Count == 0)
            {
                RebuildFeatures();
            }

            return _features;
        }

        // -------- IInteractable 实现 --------

        /// <summary>InitRoom 或 HorrorLevel 阶段且未消耗时可交互（ADR-2/6）。</summary>
        public bool CanInteract(GamePhase phase)
        {
            if (Consumed)
            {
                return false;
            }

            return phase == GamePhase.InitRoom || phase == GamePhase.HorrorLevel;
        }

        /// <summary>
        /// 按当前阶段分派：InitRoom → CollectCandidate；HorrorLevel → TryMatch。
        /// 原 InteractionSystem 的相位 switch 搬入此处（ADR-2）。
        /// </summary>
        public void Interact()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Anchor == null)
            {
                return;
            }

            switch (gm.CurrentPhase)
            {
                case GamePhase.InitRoom:
                    gm.Anchor.CollectCandidate(this);
                    Consumed = true; // 初始房间：交互过的物品不再重复计入
                    break;

                case GamePhase.HorrorLevel:
                    gm.Anchor.TryMatch(this);
                    break;
            }
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
            BuildFeaturesGenerated(_features);
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
