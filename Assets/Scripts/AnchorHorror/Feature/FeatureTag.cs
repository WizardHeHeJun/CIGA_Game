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
    /// 实现 IInteractable：InitRoom/HorrorLevel 阶段且未消耗且背包未满时可交互（ADR-2/3，陷阱 5）；
    /// Interact 按相位分派：InitRoom→gm.SelectInLevel1(this)、HorrorLevel→gm.PickupInLevel2(this)。
    /// 需 Collider2D 以供 2D 交互检测。
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
        [SerializeField] private Sprite _defaultSprite;
        [SerializeField] private Sprite _activeSprite;

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

        /// <summary>运行时配置默认/高亮图片。高亮图为空时继续使用颜色高亮降级。</summary>
        public void ConfigureSprites(Sprite defaultSprite, Sprite activeSprite)
        {
            _defaultSprite = defaultSprite;
            _activeSprite = activeSprite;

            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_renderer != null)
            {
                if (_defaultSprite == null)
                {
                    _defaultSprite = _renderer.sprite;
                }

                if (!_highlighted && _defaultSprite != null)
                {
                    _renderer.sprite = _defaultSprite;
                }
            }
        }

        /// <summary>背包/记忆面板展示用 Sprite：优先静默图，避免把整场景 Active overlay 放进背包。</summary>
        public Sprite IconSprite => _defaultSprite != null ? _defaultSprite : (_renderer != null ? _renderer.sprite : null);

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
                if (_defaultSprite == null)
                {
                    _defaultSprite = _renderer.sprite;
                }
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

        /// <summary>
        /// InitRoom：未消耗且背包未满（Inventory.Count &lt; Capacity）时可交互（陷阱 5，SC-1）。
        /// HorrorLevel：未消耗且背包未满时可交互（cap 8，满则封锁）。
        /// 其余相位不可交互。
        /// </summary>
        public bool CanInteract(GamePhase phase)
        {
            if (Consumed)
            {
                return false;
            }

            var gm = GameManager.Instance;
            if (phase == GamePhase.InitRoom)
            {
                // 关卡1：背包满（已选 5 件）则不可再交互（SC-1，陷阱 5）
                if (gm != null && gm.Backpack != null &&
                    gm.Backpack.Count >= gm.Config.Level1SelectCap)
                {
                    return false;
                }
                return true;
            }

            if (phase == GamePhase.HorrorLevel)
            {
                // 关卡2：背包满（8 件）则不可再交互（SC-4）
                if (gm != null && gm.Backpack != null &&
                    gm.Backpack.Count >= gm.Config.Level2BackpackCap)
                {
                    return false;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 按当前阶段分派：InitRoom → gm.SelectInLevel1(this)；HorrorLevel → gm.PickupInLevel2(this)。
        /// ADR-3/6：相位 switch 搬入此处，GameManager 封装背包逻辑。
        /// </summary>
        public void Interact()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            switch (gm.CurrentPhase)
            {
                case GamePhase.InitRoom:
                    gm.SelectInLevel1(this);
                    break;

                case GamePhase.HorrorLevel:
                    gm.PickupInLevel2(this);
                    break;
            }
        }

        /// <summary>检视物品（R 键）：广播 ItemInspected，由 MatchFeedback 播声音 + 浮出特征信息。不入包、无业务后果。</summary>
        public void Inspect()
        {
            EventBus.RaiseItemInspected(this);
        }

        /// <summary>切换高亮：优先切 Active/Default Sprite，缺高亮图时退回改 SpriteRenderer 颜色。</summary>
        public void SetHighlight(bool on)
        {
            if (_highlighted == on || _renderer == null)
            {
                return;
            }

            _highlighted = on;
            if (_activeSprite != null)
            {
                _renderer.sprite = on ? _activeSprite : _defaultSprite;
                _renderer.color = _baseColor;
                return;
            }

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
