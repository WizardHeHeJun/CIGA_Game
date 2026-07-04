// ------------------------------------------------------------
// AnchorLevelDemoSetup.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 生成占位示例关卡：确保 ItemDatabase（从 CSV 导入）→ 建 2 个 LevelData（物品网格摆放、按定义缩放、接好 DB/Config/出生点）。
    /// 菜单 Ciga/AnchorHorror/生成示例关卡。供策划开箱即用、也作关卡编辑器/运行时联调数据。幂等：已存在则就地重建。
    /// </summary>
    public static class AnchorLevelDemoSetup
    {
        private const string Dir = "Assets/Res/AnchorHorror";
        private const string LevelsDir = Dir + "/Levels";
        private const string LevelConfigPath = Dir + "/LevelConfig.asset";

        [MenuItem("Ciga/AnchorHorror/生成示例关卡")]
        public static void BuildAllMenu()
        {
            try
            {
                int n = BuildAll();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("示例关卡", $"已生成/更新 {n} 个示例关卡（{LevelsDir}）。", "好");
            }
            catch (ItemCsvException ex)
            {
                EditorUtility.DisplayDialog("示例关卡生成失败（物品表校验）", ex.Message, "去修表");
            }
        }

        /// <summary>确保物品目录 → 建 2 个示例关卡。返回生成的关卡数。</summary>
        public static int BuildAll()
        {
            var db = EnsureItemDatabase();
            var cfg = EnsureLevelConfig();
            EnsureFolder(LevelsDir);

            // Demo1：全部物品（从 ItemDatabase 动态取，随 AnchorItems.csv 自动跟随，2×N 网格）
            var allIds = new List<string>();
            for (int i = 0; i < db.Items.Count; i++)
            {
                var def = db.Items[i];
                if (def != null && !string.IsNullOrEmpty(def.Id))
                {
                    allIds.Add(def.Id);
                }
            }

            BuildLevel(LevelsDir + "/DemoLevel1.asset", "示例关卡·全物品", db, cfg, allIds.ToArray());

            // Demo2：木质/金属主题子集（校验存在性，缺则告警）
            var subset = new[] { "chair_wood", "box_wood", "lamp_metal", "clock_metal" };
            foreach (var id in subset)
            {
                if (!db.TryGetById(id, out _))
                {
                    Debug.LogWarning($"[AnchorLevelDemoSetup] DemoLevel2 引用的 itemId '{id}' 不在 ItemDatabase（将被跳过）。");
                }
            }

            BuildLevel(LevelsDir + "/DemoLevel2.asset", "示例关卡·木与金属", db, cfg, subset);

            return 2;
        }

        private static void BuildLevel(string path, string levelName, ItemDatabase db, LevelConfig cfg, string[] itemIds)
        {
            var level = LoadOrCreate<LevelData>(path);
            var so = new SerializedObject(level);

            so.FindProperty("_levelName").stringValue = levelName;
            so.FindProperty("_itemDatabase").objectReferenceValue = db;
            so.FindProperty("_levelConfig").objectReferenceValue = cfg;
            so.FindProperty("_playerSpawn").vector2Value = new Vector2(0f, -3.5f);

            var items = so.FindProperty("_items");
            items.ClearArray();

            const int cols = 4;
            const float gapX = 2f;
            const float gapY = 2f;
            for (int i = 0; i < itemIds.Length; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float x = (col - (cols - 1) * 0.5f) * gapX;
                float y = 1.5f - row * gapY;

                Vector2 scale = Vector2.one;
                if (db != null && db.TryGetById(itemIds[i], out var def))
                {
                    scale = def.DefaultScale;
                }

                items.InsertArrayElementAtIndex(i);
                var e = items.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("_itemId").stringValue = itemIds[i];
                e.FindPropertyRelative("_position").vector2Value = new Vector2(x, y);
                e.FindPropertyRelative("_rotationZ").floatValue = 0f;
                e.FindPropertyRelative("_scale").vector2Value = scale;
                e.FindPropertyRelative("_overrideFeatures").boolValue = false;
                e.FindPropertyRelative("_overrideSprite").boolValue = false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssetIfDirty(level);
        }

        private static ItemDatabase EnsureItemDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemCatalogCodegen.DbPath);
            if (db == null || db.Items.Count == 0)
            {
                ItemCatalogCodegen.ImportAll();
                db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemCatalogCodegen.DbPath);
            }

            return db;
        }

        private static LevelConfig EnsureLevelConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<LevelConfig>();
                AssetDatabase.CreateAsset(cfg, LevelConfigPath);
            }

            return cfg;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
