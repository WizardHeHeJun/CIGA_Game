// ------------------------------------------------------------
// ItemDatabase.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 物品目录资产：持有所有 ItemDefinition，提供按 ID 查询与全局兜底 Sprite。
    /// 运行时不走 AssetDatabase，兜底图直接引用序列化字段。
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Ciga/AnchorHorror/ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> _items = new List<ItemDefinition>();
        [SerializeField] private Sprite _fallbackSprite;

        /// <summary>所有物品定义，只读视图。</summary>
        public IReadOnlyList<ItemDefinition> Items => _items;

        /// <summary>全局兜底图；itemId 对应定义无 Sprite 时使用。</summary>
        public Sprite FallbackSprite => _fallbackSprite;

        /// <summary>
        /// 按 ID 查找物品定义。查不到返回 false，def 置 null。
        /// </summary>
        public bool TryGetById(string id, out ItemDefinition def)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null && _items[i].Id == id)
                {
                    def = _items[i];
                    return true;
                }
            }

            def = null;
            return false;
        }
    }
}
