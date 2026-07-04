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

        [Test]
        public void Parse_RealCsv_YieldsExpectedRowsAndDims()
        {
            var data = ItemCsvParser.Parse(ReadRealCsv());
            Assert.AreEqual(8, data.Rows.Count);
            CollectionAssert.AreEqual(new[] { "Color", "Shape", "Material", "Texture", "Sound" }, data.FeatureDimKeys);
        }

        [Test]
        public void Parse_ChairWood_FeaturesAndTransformCorrect()
        {
            var data = ItemCsvParser.Parse(ReadRealCsv());
            var chair = data.Rows.Find(r => r.ItemId == "chair_wood");
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
        public void Parse_EmptySoundIsNone_And_CircleCollider()
        {
            var data = ItemCsvParser.Parse(ReadRealCsv());
            var book = data.Rows.Find(r => r.ItemId == "book_paper");
            Assert.AreEqual(0, book.FeatureValues["Sound"]); // 空 → None(0)
            var lamp = data.Rows.Find(r => r.ItemId == "lamp_metal");
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

        /// <summary>端到端：运行导入器回填 ItemDatabase.asset 并校验（顺带产出该资产供关卡编辑器/示例关卡使用）。</summary>
        [Test]
        public void ImportAll_ProducesItemDatabase_WithAllItems()
        {
            ItemCatalogCodegen.ImportAll();
            AssetDatabase.SaveAssets();

            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemCatalogCodegen.DbPath);
            Assert.IsNotNull(db, "ItemDatabase.asset 应已生成");
            Assert.AreEqual(8, db.Items.Count);

            Assert.IsTrue(db.TryGetById("clock_metal", out var clock));
            Assert.AreEqual((int)FeatureSound.Ticking, (int)clock.Sound);
            Assert.AreEqual((int)FeatureMaterial.Metal, (int)clock.Material);
            Assert.AreEqual((int)FeatureColor.Gold, (int)clock.Color);

            Assert.IsFalse(db.TryGetById("nonexistent_id", out _), "查不到的 id 返回 false");
            Assert.IsNotNull(db.FallbackSprite, "兜底图应回填 WhiteSquare");
        }
    }
}
