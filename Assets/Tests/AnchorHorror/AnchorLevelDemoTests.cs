// ------------------------------------------------------------
// AnchorLevelDemoTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using Ciga.AnchorHorror.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>
    /// 示例关卡生成 + 端到端联调：BuildAll 产出 DemoLevel1/2，且 LevelSpawner 能从 CSV 生成的 ItemDatabase
    /// spawn 出带正确五维特征的物品（打通 物品配置表 → ItemDatabase → LevelData → LevelSpawner → FeatureTag）。
    /// </summary>
    public class AnchorLevelDemoTests
    {
        private const string Demo1 = "Assets/Res/AnchorHorror/Levels/DemoLevel1.asset";
        private const string Demo2 = "Assets/Res/AnchorHorror/Levels/DemoLevel2.asset";

        [Test]
        public void BuildAll_ProducesTwoDemoLevels_Wired()
        {
            AnchorLevelDemoSetup.BuildAll();
            AssetDatabase.SaveAssets();

            var l1 = AssetDatabase.LoadAssetAtPath<LevelData>(Demo1);
            Assert.IsNotNull(l1, "DemoLevel1 应生成");
            Assert.AreEqual(8, l1.Items.Count);
            Assert.IsNotNull(l1.ItemDatabase, "关卡应接好 ItemDatabase");
            Assert.IsNotNull(l1.LevelConfig, "关卡应接好 LevelConfig");
            Assert.AreEqual("chair_wood", l1.Items[0].ItemId);

            var l2 = AssetDatabase.LoadAssetAtPath<LevelData>(Demo2);
            Assert.IsNotNull(l2, "DemoLevel2 应生成");
            Assert.AreEqual(4, l2.Items.Count);
        }

        [Test]
        public void DemoLevel1_SpawnsAllItems_WithCorrectFeatures()
        {
            AnchorLevelDemoSetup.BuildAll();
            var l1 = AssetDatabase.LoadAssetAtPath<LevelData>(Demo1);
            Assert.IsNotNull(l1);

            var root = new GameObject("__demo_spawn_root");
            try
            {
                var tags = LevelSpawner.Spawn(l1, root.transform);
                Assert.AreEqual(8, tags.Count, "应 spawn 出 8 个物品");

                // 首个 = chair_wood：DarkBrown / Square / Wood / Rough / WoodFriction（取定义默认，无覆盖）
                var chair = tags[0];
                Assert.AreEqual(FeatureColor.DarkBrown, chair.Color);
                Assert.AreEqual(FeatureShape.Square, chair.Shape);
                Assert.AreEqual(FeatureMaterial.Wood, chair.Material);
                Assert.AreEqual(FeatureTexture.Rough, chair.Texture);
                Assert.AreEqual(FeatureSound.WoodFriction, chair.Sound);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
