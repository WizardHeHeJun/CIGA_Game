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

                // itemId 允许为空（正式关卡生成器约定）：物品自带覆盖 Sprite/特征，不依赖 ItemDatabase 定义。
                // 仅当 itemId 非空却查不到定义、且实例又没有任何自包含信息时才跳过（真数据错误）。
                db.TryGetById(placed.ItemId, out var def);
                if (def == null && !placed.OverrideSprite && !placed.OverrideFeatures && !placed.VisualOnly)
                {
                    Debug.LogWarning($"[LevelSpawner] ItemId '{placed.ItemId}' 在 ItemDatabase 中不存在且无覆盖信息，跳过。");
                    continue;
                }

                string runtimeKey = ResolveRuntimeKey(level, placed, i);
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

        private static string ResolveRuntimeKey(LevelData level, PlacedItem placed, int index)
        {
            if (!string.IsNullOrEmpty(placed.RuntimeKey))
            {
                return placed.RuntimeKey;
            }

            // 兜底键必须带列表序号：整场景 overlay 物品的 itemId/位置/缩放可能完全相同
            // （正式关卡资产均为 chair_wood + 原点对齐），仅靠这些字段会撞键，
            // 导致"捡走 1 件、重进房间后同键物品全部被视为已消费而消失"。
            string levelName = level != null ? level.LevelName : string.Empty;
            return string.Format(CultureInfo.InvariantCulture,
                "{0}:{1}#{2}",
                levelName,
                placed.ItemId,
                index);
        }

        private static string ResolveHideKey(PlacedItem placed, string runtimeKey)
        {
            return !string.IsNullOrEmpty(placed.HideWhenConsumedOf)
                ? placed.HideWhenConsumedOf
                : runtimeKey;
        }

    }
}
