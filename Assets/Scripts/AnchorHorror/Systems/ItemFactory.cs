// ------------------------------------------------------------
// ItemFactory.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 纯装配工厂：将 ItemDefinition + PlacedItem 组合成带 SpriteRenderer / Collider2D / FeatureTag 的 GameObject。
    /// 编辑器预览（LevelPreviewSession）与运行时 Spawner（LevelSpawner）共用此路径，消除编辑↔运行漂移。
    /// 不引用任何 UnityEditor API，纯运行时代码。
    /// 装配顺序（硬约束）：Collider2D → SpriteRenderer → FeatureTag → Configure。
    ///   原因：FeatureTag 带 [RequireComponent(Collider2D)]，且 Awake 里 GetComponent&lt;SpriteRenderer&gt;() 取基色；
    ///   先挂碰撞体+渲染器，再挂 FeatureTag（Awake 随即跑），最后 Configure 覆盖默认枚举建的缓存。
    /// </summary>
    public static class ItemFactory
    {
        /// <summary>
        /// 根据物品定义与实例描述装配 GameObject。
        /// </summary>
        /// <param name="def">物品定义（非空）。</param>
        /// <param name="placed">关卡中的物品实例描述（非空）。</param>
        /// <param name="fallback">全局兜底 Sprite；def 与 placed 均无 Sprite 时使用。</param>
        /// <param name="parent">挂载父节点（关卡根 Transform）。</param>
        /// <returns>装配完毕的 GameObject，含 Collider2D / SpriteRenderer / FeatureTag。</returns>
        public static GameObject Create(ItemDefinition def, PlacedItem placed, Sprite fallback, Transform parent)
        {
            // --- 1. 建 GameObject，名称取显示名，无则用 ID ---
            string goName = !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : def.Id;
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);

            // --- 2. 设 Transform（局部，SetParent 后再设避免世界坐标污染）---
            go.transform.localPosition = placed.Position;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, placed.RotationZ);
            go.transform.localScale = placed.Scale;

            // --- 3. 挂 Collider2D（按 ColliderKind 选 Box/Circle；FeatureTag 依赖此组件）---
            switch (def.Collider)
            {
                case ColliderKind.Circle:
                    go.AddComponent<CircleCollider2D>();
                    break;
                default: // ColliderKind.Box
                    go.AddComponent<BoxCollider2D>();
                    break;
            }

            // --- 4. 挂 SpriteRenderer，选取 Sprite（优先级：PlacedItem 覆盖 → def 默认 → fallback）---
            var sr = go.AddComponent<SpriteRenderer>();
            Sprite sprite = null;
            if (placed.OverrideSprite)
            {
                sprite = placed.Sprite;
            }
            if (sprite == null)
            {
                sprite = def.Sprite;
            }
            if (sprite == null)
            {
                sprite = fallback;
            }
            sr.sprite = sprite;

            // --- 5. 挂 FeatureTag（Awake 在 AddComponent 后立即运行，会 GetComponent<SpriteRenderer> 取基色）---
            var tag = go.AddComponent<FeatureTag>();

            // --- 6. Configure 覆盖特征（幂等；覆盖 Awake 用 Inspector 默认枚举建的缓存）---
            FeatureColor color;
            FeatureShape shape;
            FeatureMaterial material;
            FeatureTexture texture;

            if (placed.OverrideFeatures)
            {
                color = placed.Color;
                shape = placed.Shape;
                material = placed.Material;
                texture = placed.Texture;
            }
            else
            {
                color = def.Color;
                shape = def.Shape;
                material = def.Material;
                texture = def.Texture;
            }

            tag.Configure(color, shape, material, texture);

            return go;
        }
    }
}
