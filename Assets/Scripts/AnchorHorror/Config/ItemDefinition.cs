// ------------------------------------------------------------
// ItemDefinition.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 碰撞体类型，决定 ItemFactory 为物品挂 BoxCollider2D 还是 CircleCollider2D。
    /// </summary>
    public enum ColliderKind
    {
        Box,
        Circle
    }

    /// <summary>
    /// 物品目录元素：描述一类物品的默认外观与特征，被 ItemDatabase 持有。
    /// </summary>
    [System.Serializable]
    public class ItemDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _sprite;
        [SerializeField] private FeatureColor _color;
        [SerializeField] private FeatureShape _shape;
        [SerializeField] private FeatureMaterial _material;
        [SerializeField] private FeatureTexture _texture;
        [SerializeField] private FeatureSound _sound;
        [SerializeField] private Vector2 _defaultScale = Vector2.one;
        [SerializeField] private ColliderKind _collider;

        /// <summary>物品唯一 ID，对应 PlacedItem.ItemId。</summary>
        public string Id => _id;

        /// <summary>编辑器 / UI 显示名。</summary>
        public string DisplayName => _displayName;

        /// <summary>默认图片；PlacedItem 未覆盖时使用。</summary>
        public Sprite Sprite => _sprite;

        /// <summary>默认颜色特征。</summary>
        public FeatureColor Color => _color;

        /// <summary>默认形状特征。</summary>
        public FeatureShape Shape => _shape;

        /// <summary>默认材质特征。</summary>
        public FeatureMaterial Material => _material;

        /// <summary>默认纹理特征。</summary>
        public FeatureTexture Texture => _texture;

        /// <summary>默认声音特征。</summary>
        public FeatureSound Sound => _sound;

        /// <summary>默认缩放，默认值 (1,1)。</summary>
        public Vector2 DefaultScale => _defaultScale;

        /// <summary>碰撞体种类：Box 或 Circle。</summary>
        public ColliderKind Collider => _collider;
    }
}
