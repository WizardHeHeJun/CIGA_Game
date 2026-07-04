// ------------------------------------------------------------
// FeatureCodegenTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using Ciga.AnchorHorror.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>
    /// 特征配置表 codegen 回归：ID 兼容 / 复合材质独立 / 5 维 / Sound / 往返确定性 / CSV 坏数据校验 / DB 回填中文名。
    /// </summary>
    public class FeatureCodegenTests
    {
        // ---------------------------------------------------------------- SC-2 ID 兼容（旧枚举值一字不变）

        [Test]
        public void OldEnumIds_Unchanged_ForMigrationCompat()
        {
            Assert.AreEqual(1, (int)FeatureColor.Red);
            Assert.AreEqual(2, (int)FeatureColor.Blue);
            Assert.AreEqual(5, (int)FeatureColor.White);
            Assert.AreEqual(7, (int)FeatureColor.Brown);
            Assert.AreEqual(1, (int)FeatureShape.Round);
            Assert.AreEqual(5, (int)FeatureShape.Irregular);
            Assert.AreEqual(1, (int)FeatureMaterial.Wood);
            Assert.AreEqual(6, (int)FeatureMaterial.Ceramic);
            Assert.AreEqual(1, (int)FeatureTexture.Smooth);
            Assert.AreEqual(5, (int)FeatureTexture.Patterned);
            Assert.AreEqual(0, (int)FeatureColor.None);
        }

        // ---------------------------------------------------------------- SC-4 复合材质各独立单值，不互匹

        [Test]
        public void CompositeMaterials_AreIndependentValues()
        {
            Assert.AreEqual(9, (int)FeatureMaterial.WoodGlass);
            Assert.AreEqual(10, (int)FeatureMaterial.GlassMetal);
            Assert.AreEqual(11, (int)FeatureMaterial.MetalPlastic);

            var woodGlass = new FeatureUnit(FeatureDimension.Material, (int)FeatureMaterial.WoodGlass);
            var wood = new FeatureUnit(FeatureDimension.Material, (int)FeatureMaterial.Wood);
            var glass = new FeatureUnit(FeatureDimension.Material, (int)FeatureMaterial.Glass);
            Assert.AreNotEqual(woodGlass, wood, "木质玻璃 ≠ 木质");
            Assert.AreNotEqual(woodGlass, glass, "木质玻璃 ≠ 玻璃");
        }

        // ---------------------------------------------------------------- SC-5 新增 Sound 维度 + 5 维 GetFeatures

        [Test]
        public void SoundDimension_AndValues_Exist()
        {
            Assert.AreEqual(4, (int)FeatureDimension.Sound);
            Assert.AreEqual(5, (int)FeatureSound.Ticking);
            Assert.AreEqual(0, (int)FeatureSound.None);
        }

        [Test]
        public void FeatureTag_GetFeatures_ProducesFiveDimensions()
        {
            var go = new GameObject("Item");
            go.AddComponent<BoxCollider2D>();
            var tag = go.AddComponent<FeatureTag>();
            tag.Configure(FeatureColor.Red, FeatureShape.Round, FeatureMaterial.WoodGlass, FeatureTexture.Smooth, FeatureSound.Ticking);

            var features = tag.GetFeatures();
            Assert.AreEqual(5, features.Count, "5 维应产 5 个 FeatureUnit");
            CollectionAssert.Contains(features, new FeatureUnit(FeatureDimension.Sound, (int)FeatureSound.Ticking));
            CollectionAssert.Contains(features, new FeatureUnit(FeatureDimension.Material, (int)FeatureMaterial.WoodGlass));

            Object.DestroyImmediate(go);
        }

        // ---------------------------------------------------------------- SC-6 生成往返确定性

        [Test]
        public void Codegen_IsDeterministic_SameCsvSameOutput()
        {
            var data = ParseCsv();
            Assert.AreEqual(FeatureCodegen.GenerateEnumsText(data), FeatureCodegen.GenerateEnumsText(data));
            Assert.AreEqual(FeatureCodegen.GenerateFeatureTagText(data), FeatureCodegen.GenerateFeatureTagText(data));
        }

        // ---------------------------------------------------------------- SC-7 CSV 坏数据 → 校验中止

        [Test]
        public void CsvParser_RejectsBadData()
        {
            const string head = "@dim,0,Color,,,,,,\n";
            Assert.Throws<FeatureCsvException>(() => FeatureCsvParser.Parse(head + "@val,0,Color,0,Zero,零,#FFFFFF,,"), "禁显式 valueId=0");
            Assert.Throws<FeatureCsvException>(() => FeatureCsvParser.Parse(head + "@val,0,Color,256,Big,大,#FFFFFF,,"), "valueId 越界 255");
            Assert.Throws<FeatureCsvException>(() => FeatureCsvParser.Parse(head + "@val,0,Color,1,A,甲,#FFFFFF,,\n@val,0,Color,1,B,乙,#FFFFFF,,"), "valueId 重复");
            Assert.Throws<FeatureCsvException>(() => FeatureCsvParser.Parse(head + "@val,9,Ghost,1,X,鬼,,,"), "维度未声明");
            Assert.Throws<FeatureCsvException>(() => FeatureCsvParser.Parse(head + "@val,0,Color,1,1bad,坏,#FFFFFF,,"), "非法标识符");
            Assert.Throws<FeatureCsvException>(() => FeatureCsvParser.Parse(head + "@val,0,Color,1,X,坏,notacolor,,"), "colorHex 非法");
            Assert.DoesNotThrow(() => FeatureCsvParser.Parse(head + "@val,0,Color,1,Red,红色,#FF6B6B,,"), "合法行应通过");
        }

        // ---------------------------------------------------------------- SC-8/SC-9 RegenerateAll 回填 DB 中文名（并触发生成器落盘）

        [Test]
        public void RegenerateAll_FillsDatabase_WithChineseDisplayNames()
        {
            FeatureCodegen.RegenerateAll();

            var db = AssetDatabase.LoadAssetAtPath<FeatureDatabase>(FeatureCodegen.DbPath);
            Assert.IsNotNull(db, "FeatureDatabase.asset 应存在");

            Assert.AreEqual("米黄", db.GetDisplayName(new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Beige)));
            Assert.AreEqual("木质玻璃", db.GetDisplayName(new FeatureUnit(FeatureDimension.Material, (int)FeatureMaterial.WoodGlass)));
            Assert.AreEqual("滴答声", db.GetDisplayName(new FeatureUnit(FeatureDimension.Sound, (int)FeatureSound.Ticking)));
            Assert.AreEqual("红色", db.GetDisplayName(new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Red)));

            // 磁盘产物与当前 CSV 生成结果一致（往返 vs 盘）
            var data = ParseCsv();
            string diskEnums = System.IO.File.ReadAllText(
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), FeatureCodegen.EnumsPath))
                .Replace("\r\n", "\n").Replace("\r", "\n");
            Assert.AreEqual(FeatureCodegen.GenerateEnumsText(data), diskEnums, "磁盘 FeatureEnums.Generated.cs 应等于当前 CSV 的生成结果");
        }

        private static FeatureCsvData ParseCsv()
        {
            string full = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), FeatureCodegen.CsvPath);
            return FeatureCsvParser.Parse(System.IO.File.ReadAllText(full));
        }
    }
}
