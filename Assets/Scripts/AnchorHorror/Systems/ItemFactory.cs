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
    /// 装配顺序（硬约束）：SpriteRenderer → Collider2D → FeatureTag → Configure。
    ///   原因：FeatureTag 带 [RequireComponent(Collider2D)]，且 Awake 里 GetComponent&lt;SpriteRenderer&gt;() 取基色；
    ///   先挂渲染器+碰撞体，再挂 FeatureTag（Awake 随即跑），最后 Configure 覆盖默认枚举建的缓存。
    /// </summary>
    public static class ItemFactory
    {
        public struct SpawnRuntimeState
        {
            public string RuntimeKey;
            public bool Consumed;
            public bool Hidden;
        }

        /// <summary>
        /// 根据物品定义与实例描述装配 GameObject。
        /// </summary>
        /// <param name="def">物品定义（非空）。</param>
        /// <param name="placed">关卡中的物品实例描述（非空）。</param>
        /// <param name="fallback">全局兜底 Sprite；def 与 placed 均无 Sprite 时使用。</param>
        /// <param name="parent">挂载父节点（关卡根 Transform）。</param>
        /// <param name="runtimeState">运行时状态：实例键与是否应以已消费/隐藏态生成。</param>
        /// <returns>装配完毕的 GameObject，含 Collider2D / SpriteRenderer / FeatureTag。</returns>
        public static GameObject Create(ItemDefinition def, PlacedItem placed, Sprite fallback, Transform parent,
            SpawnRuntimeState runtimeState)
        {
            // --- 1. 建 GameObject，名称取显示名，无则用 ID ---
            string goName = !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : def.Id;
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);

            // --- 2. 设 Transform（局部，SetParent 后再设避免世界坐标污染）---
            if (placed.AlignWithBackground)
            {
                // 美术物件与背景同画布，Sprite 本身已按同 PPU 导入；放原点即可和背景重合。
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }
            else
            {
                go.transform.localPosition = placed.Position;
                go.transform.localRotation = Quaternion.Euler(0f, 0f, placed.RotationZ);
                // Vector2→Vector3 会把 z 截断为 0（污染子节点世界缩放/gizmo）；显式给 z=1。
                go.transform.localScale = new Vector3(placed.Scale.x, placed.Scale.y, 1f);
            }

            // --- 3. 挂 SpriteRenderer，选取 Sprite（优先级：PlacedItem 覆盖 → def 默认 → fallback）---
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

            if (runtimeState.Hidden)
            {
                go.SetActive(false);
            }

            if (placed.VisualOnly)
            {
                return go;
            }

            // --- 4. 挂 Collider2D（按 ColliderKind 选 Box/Circle；FeatureTag 依赖此组件）---
            // 交互靠 InteractionSystem 的 OverlapCircle 检测，不需要物理阻挡；设 isTrigger 避免玩家撞在物品上卡住/旋转（用户反馈）。
            Collider2D itemCol;
            switch (def.Collider)
            {
                case ColliderKind.Circle:
                    itemCol = go.AddComponent<CircleCollider2D>();
                    break;
                default: // ColliderKind.Box
                    itemCol = go.AddComponent<BoxCollider2D>();
                    break;
            }

            itemCol.isTrigger = true;
            ApplyColliderShape(itemCol, placed, sprite);

            // --- 5. 挂 FeatureTag（Awake 在 AddComponent 后立即运行，会 GetComponent<SpriteRenderer> 取基色）---
            var tag = go.AddComponent<FeatureTag>();
            tag.ConfigureRuntimeKey(runtimeState.RuntimeKey);
            tag.Consumed = runtimeState.Consumed;
            tag.ConfigureSprites(sprite, placed.ActiveSprite);

            // --- 6. Configure 覆盖特征（幂等；覆盖 Awake 用 Inspector 默认枚举建的缓存）---
            FeatureColor color;
            FeatureShape shape;
            FeatureMaterial material;
            FeatureTexture texture;
            FeatureSound sound;

            if (placed.OverrideFeatures)
            {
                color = placed.Color;
                shape = placed.Shape;
                material = placed.Material;
                texture = placed.Texture;
                sound = placed.Sound;
            }
            else
            {
                color = def.Color;
                shape = def.Shape;
                material = def.Material;
                texture = def.Texture;
                sound = def.Sound;
            }

            tag.Configure(color, shape, material, texture, sound);

            return go;
        }

        private static void ApplyColliderShape(Collider2D itemCol, PlacedItem placed, Sprite sprite)
        {
            var size = placed.ColliderSize;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            }

            if (itemCol is BoxCollider2D box)
            {
                box.offset = placed.ColliderOffset;
                box.size = size;
                return;
            }

            if (itemCol is CircleCollider2D circle)
            {
                circle.offset = placed.ColliderOffset;
                circle.radius = Mathf.Max(size.x, size.y) * 0.5f;
            }
        }
    }
}
