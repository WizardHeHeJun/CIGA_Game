// ------------------------------------------------------------
// ItemCatalogCodegen.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// AnchorItems.csv → 回填 ItemDatabase.asset（物品目录）。菜单 Ciga/AnchorHorror/从CSV生成物品目录。
    /// 就地重建 _items（保 GUID）；特征按维度名写 ItemDefinition 的 typed 字段 _&lt;dim 小写&gt;（enumValueIndex = valueId，枚举连续故等价）。
    /// 未来维度：ItemDefinition 若尚无对应字段则跳过（安全降级），补齐字段后重生即可。校验失败弹框、不落盘。
    /// </summary>
    public static class ItemCatalogCodegen
    {
        public const string CsvPath = "Assets/Res/AnchorHorror/AnchorItems.csv";
        public const string DbPath = "Assets/Res/AnchorHorror/ItemDatabase.asset";
        public const string FallbackSpritePath = "Assets/Res/AnchorHorror/WhiteSquare.png";

        [MenuItem("Ciga/AnchorHorror/从CSV生成物品目录")]
        public static void RegenerateMenu()
        {
            try
            {
                bool changed = ImportAll();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("物品目录生成",
                    changed ? "已从 AnchorItems.csv 回填 ItemDatabase.asset。" : "CSV 无变化，ItemDatabase 已是最新（未写盘）。", "好");
            }
            catch (ItemCsvException ex)
            {
                EditorUtility.DisplayDialog("物品目录生成失败（CSV 校验）", ex.Message, "去修表");
            }
        }

        /// <summary>解析 + 校验 + 回填 ItemDatabase.asset。返回是否写盘（false = 无变化）。校验失败抛 ItemCsvException。</summary>
        public static bool ImportAll()
        {
            var data = ItemCsvParser.Parse(ReadCsv());
            var db = LoadOrCreateDatabase();
            var so = new SerializedObject(db);

            var items = so.FindProperty("_items");
            if (items == null)
            {
                Debug.LogWarning("[ItemCatalogCodegen] ItemDatabase 无 _items 字段，回填中止。");
                return false;
            }

            items.ClearArray();
            for (int i = 0; i < data.Rows.Count; i++)
            {
                var row = data.Rows[i];
                items.InsertArrayElementAtIndex(i);
                var e = items.GetArrayElementAtIndex(i);

                e.FindPropertyRelative("_id").stringValue = row.ItemId;
                e.FindPropertyRelative("_displayName").stringValue = row.DisplayName;

                foreach (var kv in row.FeatureValues)
                {
                    string fieldName = FieldName(kv.Key);
                    var p = e.FindPropertyRelative(fieldName);
                    if (p != null)
                    {
                        p.enumValueIndex = kv.Value; // 枚举连续 → index == valueId
                    }
                }

                e.FindPropertyRelative("_defaultScale").vector2Value = new Vector2(row.ScaleX, row.ScaleY);
                e.FindPropertyRelative("_collider").enumValueIndex =
                    row.ColliderCircle ? (int)ColliderKind.Circle : (int)ColliderKind.Box;

                Sprite sprite = null;
                if (!string.IsNullOrEmpty(row.SpritePath) && ItemCsvParser.ResolveSpritePath(row.SpritePath, out string sp))
                {
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sp);
                }

                e.FindPropertyRelative("_sprite").objectReferenceValue = sprite;
            }

            var fb = so.FindProperty("_fallbackSprite");
            if (fb != null && fb.objectReferenceValue == null)
            {
                var fbSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FallbackSpritePath);
                if (fbSprite != null)
                {
                    fb.objectReferenceValue = fbSprite;
                }
            }

            if (!so.hasModifiedProperties)
            {
                return false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssetIfDirty(db);
            return true;
        }

        /// <summary>dimKey → 字段名（"Color" → "_color"），与 ItemDefinition/FeatureTag 字段命名约定一致。</summary>
        private static string FieldName(string dimKey)
        {
            return "_" + char.ToLowerInvariant(dimKey[0]) + dimKey.Substring(1);
        }

        private static string ReadCsv()
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), CsvPath);
            if (!File.Exists(full))
            {
                throw new ItemCsvException($"找不到 CSV：{CsvPath}");
            }

            return File.ReadAllText(full);
        }

        private static ItemDatabase LoadOrCreateDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(db, DbPath);
            }

            return db;
        }
    }
}
