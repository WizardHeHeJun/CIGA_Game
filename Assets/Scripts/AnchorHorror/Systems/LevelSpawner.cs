// ------------------------------------------------------------
// LevelSpawner.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 运行时关卡 Spawner：遍历 LevelData.Items，调 ItemFactory 把每个物品挂到调用方传入的关卡根，
    /// 返回生成的 FeatureTag 列表供 LevelFeatureRegistry 隔离扫描（不扫 activeScene 防 InitRoom 叠加）。
    /// </summary>
    public static class LevelSpawner
    {
        public readonly struct SpawnContext
        {
            private readonly HashSet<string> _consumedKeys;

            public SpawnContext(HashSet<string> consumedKeys)
            {
                _consumedKeys = consumedKeys;
            }

            public bool IsConsumed(string runtimeKey)
            {
                return !string.IsNullOrEmpty(runtimeKey)
                    && _consumedKeys != null
                    && _consumedKeys.Contains(runtimeKey);
            }
        }

        /// <summary>
        /// 按 <paramref name="level"/> 数据在 <paramref name="levelRoot"/> 下生成所有物品（无运行时状态恢复时的兼容入口）。
        /// </summary>
        public static IReadOnlyList<FeatureTag> Spawn(LevelData level, Transform levelRoot)
        {
            return Spawn(level, levelRoot, default);
        }

        /// <summary>
        /// 按 <paramref name="level"/> 数据在 <paramref name="levelRoot"/> 下生成所有物品。
        /// </summary>
        /// <param name="level">关卡数据资产（含 ItemDatabase 引用）。</param>
        /// <param name="levelRoot">关卡根 Transform，所有物品挂此节点下。</param>
        /// <param name="context">运行时恢复上下文：用于根据实例键恢复已消费/隐藏态。</param>
        /// <returns>
         /// 本次生成的所有 <see cref="FeatureTag"/> 列表；
        /// level / ItemDatabase 为 null 或 Items 为空时返回空列表。
        /// </returns>
        public static IReadOnlyList<FeatureTag> Spawn(LevelData level, Transform levelRoot, SpawnContext context)
        {
            var result = new List<FeatureTag>();

            if (level == null)
            {
                Debug.LogWarning("[LevelSpawner] LevelData 为 null，跳过生成。");
                return result;
            }

            if (level.ItemDatabase == null)
            {
                Debug.LogWarning($"[LevelSpawner] LevelData '{level.LevelName}' 的 ItemDatabase 为 null，跳过生成。");
                return result;
            }

            var items = level.Items;
            if (items == null || items.Count == 0)
            {
                return result;
            }

            var db = level.ItemDatabase;
            var fallback = db.FallbackSprite;

            for (int i = 0; i < items.Count; i++)
            {
                var placed = items[i];
                if (placed == null)
                {
                    continue;
                }

                if (!db.TryGetById(placed.ItemId, out var def))
                {
                    Debug.LogWarning($"[LevelSpawner] ItemId '{placed.ItemId}' 在 ItemDatabase 中不存在，跳过。");
                    continue;
                }

                string runtimeKey = ResolveRuntimeKey(level, placed);
                string hideKey = ResolveHideKey(placed, runtimeKey);
                bool consumed = context.IsConsumed(runtimeKey);
                bool hidden = consumed || context.IsConsumed(hideKey);
                var runtimeState = new ItemFactory.SpawnRuntimeState
                {
                    RuntimeKey = runtimeKey,
                    Consumed = consumed,
                    Hidden = hidden
                };

                var go = ItemFactory.Create(def, placed, fallback, levelRoot, runtimeState);
                if (go.TryGetComponent<FeatureTag>(out var tag))
                {
                    result.Add(tag);
                }
            }

            return result;
        }

        private static string ResolveRuntimeKey(LevelData level, PlacedItem placed)
        {
            if (!string.IsNullOrEmpty(placed.RuntimeKey))
            {
                return placed.RuntimeKey;
            }

            string levelName = level != null ? level.LevelName : string.Empty;
            Vector2 pos = placed.Position;
            return string.Format(CultureInfo.InvariantCulture,
                "{0}:{1}:{2:0.###}:{3:0.###}:{4:0.###}:{5:0.###}",
                levelName,
                placed.ItemId,
                pos.x,
                pos.y,
                placed.RotationZ,
                placed.Scale.x + placed.Scale.y * 0.0001f);
        }

        private static string ResolveHideKey(PlacedItem placed, string runtimeKey)
        {
            return !string.IsNullOrEmpty(placed.HideWhenConsumedOf)
                ? placed.HideWhenConsumedOf
                : runtimeKey;
        }

    }
}
