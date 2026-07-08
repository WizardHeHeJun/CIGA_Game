// ------------------------------------------------------------
// ItemCatalogTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.IO;
using Ciga.AnchorHorror.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>
    /// 物品配置表（AnchorItems.csv）解析 + 校验 + 回填 ItemDatabase 的回归。
    /// 表头驱动 / 特征成员对齐生成枚举 / 坏数据校验 / ImportAll 端到端（顺带产出 ItemDatabase.asset）。
    /// </summary>
    public class ItemCatalogTests
    {
        private static string ReadRealCsv()
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), ItemCatalogCodegen.CsvPath));
        }

        // 合成 CSV：解析器行为回归用（真表已清空——正式物品全部自包含，不走 itemId 引用）
        private const string SyntheticCsv =
            "itemId,displayName,Color,Shape,Material,Texture,Sound,spritePath,scaleX,scaleY,collider,note\n" +
            "chair_x,测试椅,DarkBrown,Square,Wood,Rough,WoodFriction,WhiteSquare.png,1,1.2,Box,合成\n" +
            "lamp_x,测试灯,Gray,Round,Metal,Glossy,,WhiteSquare.png,0.8,1,Circle,合成·无声\n";

        [Test]
        public void Parse_RealCsv_IsEmptyCatalog_WithAllDims()
        {
            var data = ItemCsvParser.Parse(ReadRealCsv());
            Assert.AreEqual(0, data.Rows.Count, "真表应为空目录（正式物品自包含，不走 itemId 引用）");
            CollectionAssert.AreEqual(new[] { "Color", "Shape", "Material", "Texture", "Sound" }, data.FeatureDimKeys);
        }

        [Test]
        public void Parse_Synthetic_FeaturesAndTransformCorrect()
        {
            var data = ItemCsvParser.Parse(SyntheticCsv);
            var chair = data.Rows.Find(r => r.ItemId == "chair_x");
            Assert.IsNotNull(chair);
            Assert.AreEqual((int)FeatureColor.DarkBrown, chair.FeatureValues["Color"]);
            Assert.AreEqual((int)FeatureShape.Square, chair.FeatureValues["Shape"]);
            Assert.AreEqual((int)FeatureMaterial.Wood, chair.FeatureValues["Material"]);
            Assert.AreEqual((int)FeatureTexture.Rough, chair.FeatureValues["Texture"]);
            Assert.AreEqual((int)FeatureSound.WoodFriction, chair.FeatureValues["Sound"]);
            Assert.AreEqual(1f, chair.ScaleX);
            Assert.AreEqual(1.2f, chair.ScaleY);
            Assert.IsFalse(chair.ColliderCircle);
        }

        [Test]
        public void Parse_Synthetic_EmptySoundIsNone_And_CircleCollider()
        {
            var data = ItemCsvParser.Parse(SyntheticCsv);
            var lamp = data.Rows.Find(r => r.ItemId == "lamp_x");
            Assert.AreEqual(0, lamp.FeatureValues["Sound"]); // 空 → None(0)
            Assert.IsTrue(lamp.ColliderCircle);
        }

        [Test]
        public void Parse_DuplicateId_Throws()
        {
            Assert.Throws<ItemCsvException>(() => ItemCsvParser.Parse("itemId,displayName\na,甲\na,乙\n"));
        }

        [Test]
        public void Parse_InvalidFeatureMember_Throws()
        {
            Assert.Throws<ItemCsvException>(() => ItemCsvParser.Parse("itemId,displayName,Color\nx,X,NotAColor\n"));
        }

        [Test]
        public void Parse_UnknownColumn_Throws()
        {
            Assert.Throws<ItemCsvException>(() => ItemCsvParser.Parse("itemId,displayName,Bogus\nx,X,1\n"));
        }

        [Test]
        public void Parse_MissingItemIdColumn_Throws()
        {
            Assert.Throws<ItemCsvException>(() => ItemCsvParser.Parse("displayName,Color\n甲,Red\n"));
        }

        [Test]
        public void Parse_EmptyDisplayName_Throws()
        {
            Assert.Throws<ItemCsvException>(() => ItemCsvParser.Parse("itemId,displayName\nx,\n"));
        }

        /// <summary>端到端：运行导入器回填 ItemDatabase.asset——空目录 + 兜底图仍在（LevelSpawner 依赖 FallbackSprite）。</summary>
        [Test]
        public void ImportAll_ProducesEmptyItemDatabase_WithFallbackSprite()
        {
            ItemCatalogCodegen.ImportAll();
            AssetDatabase.SaveAssets();

            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemCatalogCodegen.DbPath);
            Assert.IsNotNull(db, "ItemDatabase.asset 应已生成");
            Assert.AreEqual(0, db.Items.Count, "目录应为空（正式物品自包含）");
            Assert.IsFalse(db.TryGetById("nonexistent_id", out _), "查不到的 id 返回 false");
            Assert.IsNotNull(db.FallbackSprite, "兜底图应回填 WhiteSquare");
        }
    }
}
