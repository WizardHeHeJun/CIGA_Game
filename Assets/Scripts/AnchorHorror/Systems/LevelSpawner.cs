// ------------------------------------------------------------
// LevelSpawner.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 运行时关卡 Spawner：遍历 LevelData.Items，调 ItemFactory 把每个物品挂到调用方传入的关卡根，
    /// 返回生成的 FeatureTag 列表供 LevelFeatureRegistry 隔离扫描（不扫 activeScene 防 InitRoom 叠加）。
    /// </summary>
    public static class LevelSpawner
    {
        /// <summary>
        /// 按 <paramref name="level"/> 数据在 <paramref name="levelRoot"/> 下生成所有物品。
        /// </summary>
        /// <param name="level">关卡数据资产（含 ItemDatabase 引用）。</param>
        /// <param name="levelRoot">关卡根 Transform，所有物品挂此节点下。</param>
        /// <returns>
        /// 本次生成的所有 <see cref="FeatureTag"/> 列表；
        /// level / ItemDatabase 为 null 或 Items 为空时返回空列表。
        /// </returns>
        public static IReadOnlyList<FeatureTag> Spawn(LevelData level, Transform levelRoot)
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

                var go = ItemFactory.Create(def, placed, fallback, levelRoot);
                if (go.TryGetComponent<FeatureTag>(out var tag))
                {
                    result.Add(tag);
                }
            }

            return result;
        }

    }
}
