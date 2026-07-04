// ------------------------------------------------------------
// LevelData.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡内一个物品实例的序列化描述。
    /// 用 class 而非 struct，防止 List 元素赋值时值拷贝陷阱。
    /// </summary>
    [System.Serializable]
    public class PlacedItem
    {
        [SerializeField] private string _itemId;
        [SerializeField] private Vector2 _position;
        [SerializeField] private float _rotationZ;
        [SerializeField] private Vector2 _scale = Vector2.one;

        [Tooltip("为 true 时，以下四维特征覆盖物品定义的默认值。")]
        [SerializeField] private bool _overrideFeatures;
        [SerializeField] private FeatureColor _color;
        [SerializeField] private FeatureShape _shape;
        [SerializeField] private FeatureMaterial _material;
        [SerializeField] private FeatureTexture _texture;
        [SerializeField] private FeatureSound _sound;

        [Tooltip("为 true 时，_sprite 覆盖物品定义的默认图片。")]
        [SerializeField] private bool _overrideSprite;
        [SerializeField] private Sprite _sprite;

        /// <summary>对应 ItemDefinition.Id。</summary>
        public string ItemId => _itemId;

        /// <summary>世界坐标（或关卡根局部坐标，由 ItemFactory 决定）。</summary>
        public Vector2 Position => _position;

        /// <summary>Z 轴旋转角度（度）。</summary>
        public float RotationZ => _rotationZ;

        /// <summary>缩放，默认 (1,1)。</summary>
        public Vector2 Scale => _scale;

        /// <summary>是否覆盖四维特征。</summary>
        public bool OverrideFeatures => _overrideFeatures;

        /// <summary>覆盖颜色特征（仅 OverrideFeatures=true 时生效）。</summary>
        public FeatureColor Color => _color;

        /// <summary>覆盖形状特征（仅 OverrideFeatures=true 时生效）。</summary>
        public FeatureShape Shape => _shape;

        /// <summary>覆盖材质特征（仅 OverrideFeatures=true 时生效）。</summary>
        public FeatureMaterial Material => _material;

        /// <summary>覆盖纹理特征（仅 OverrideFeatures=true 时生效）。</summary>
        public FeatureTexture Texture => _texture;

        /// <summary>覆盖声音特征（仅 OverrideFeatures=true 时生效）。</summary>
        public FeatureSound Sound => _sound;

        /// <summary>是否覆盖 Sprite。</summary>
        public bool OverrideSprite => _overrideSprite;

        /// <summary>覆盖图片（仅 OverrideSprite=true 时生效）。</summary>
        public Sprite Sprite => _sprite;
    }

    /// <summary>
    /// 一关的数据资产：物品实例列表 + ItemDatabase 引用 + LevelConfig 引用 + 玩家出生点。
    /// GameManager 持有并在过渡时交给 LevelSpawner 生成物品。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "Ciga/AnchorHorror/LevelData")]
    public class LevelData : ScriptableObject
    {
        [SerializeField] private string _levelName;
        [SerializeField] private List<PlacedItem> _items = new List<PlacedItem>();
        [SerializeField] private ItemDatabase _itemDatabase;
        [SerializeField] private LevelConfig _levelConfig;
        [SerializeField] private Vector2 _playerSpawn;

        /// <summary>关卡名称，编辑器 / UI 展示用。</summary>
        public string LevelName => _levelName;

        /// <summary>本关所有物品实例，只读视图。</summary>
        public IReadOnlyList<PlacedItem> Items => _items;

        /// <summary>物品目录引用，ItemFactory/LevelSpawner 查定义用。</summary>
        public ItemDatabase ItemDatabase => _itemDatabase;

        /// <summary>本关配置（保底特征池、白名单等）。</summary>
        public LevelConfig LevelConfig => _levelConfig;

        /// <summary>玩家出生世界坐标。</summary>
        public Vector2 PlayerSpawn => _playerSpawn;
    }
}
