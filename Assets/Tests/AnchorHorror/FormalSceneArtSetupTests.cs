// ------------------------------------------------------------
// FormalSceneArtSetupTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using Ciga.AnchorHorror.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>正式场景美术导入/关卡数据生成器的 EditMode 验证。</summary>
    public class FormalSceneArtSetupTests
    {
        private const string LevelsDir = "Assets/Res/AnchorHorror/Levels";
        private const string SequencePath = LevelsDir + "/Formal_Sequence.asset";

        [Test]
        public void BuildFormalSceneArt_AppliesProvidedFiveDimensionalFeatures()
        {
            FormalSceneArtSetup.BuildAll();

            var seq = AssetDatabase.LoadAssetAtPath<LevelSequence>(SequencePath);
            Assert.IsNotNull(seq, "Formal_Sequence 未生成");

            var bedroom = seq.GetLevel(0);
            AssertFeature(bedroom, 0, FeatureColor.White, FeatureShape.Long, FeatureMaterial.Fabric, FeatureTexture.Soft, FeatureSound.ClothTouch, "床");
            AssertFeature(bedroom, 4, FeatureColor.Brown, FeatureShape.Square, FeatureMaterial.WoodGlass, FeatureTexture.Smooth, FeatureSound.GlassClink, "和母亲的合照");
            AssertFeature(bedroom, 7, FeatureColor.White, FeatureShape.Irregular, FeatureMaterial.Wood, FeatureTexture.Smooth, FeatureSound.WoodFriction, "椅子");

            var corridor = seq.GetLevel(1);
            Assert.AreEqual(6, corridor.Items.Count, "走廊应有 6 个配置项（含两扇视觉门）");
            Assert.IsTrue(corridor.Items[0].VisualOnly, "门A 应为纯视觉层");
            Assert.IsTrue(corridor.Items[1].VisualOnly, "门B 应为纯视觉层");
            AssertFeature(corridor, 2, FeatureColor.DarkRed, FeatureShape.Long, FeatureMaterial.Fabric, FeatureTexture.Fiber, FeatureSound.ClothTouch, "地毯");
            AssertFeature(corridor, 5, FeatureColor.Gray, FeatureShape.Long, FeatureMaterial.Stone, FeatureTexture.Rough, FeatureSound.WoodFriction, "楼梯");

            var living = seq.GetLevel(2);
            AssertFeature(living, 0, FeatureColor.DarkGray, FeatureShape.Long, FeatureMaterial.Fabric, FeatureTexture.Soft, FeatureSound.ClothTouch, "沙发");
            AssertFeature(living, 5, FeatureColor.Colorful, FeatureShape.Square, FeatureMaterial.Plastic, FeatureTexture.Rough, FeatureSound.PlasticClick, "玩具箱");

            var bathroom = seq.GetLevel(3);
            AssertFeature(bathroom, 2, FeatureColor.Silver, FeatureShape.Square, FeatureMaterial.Glass, FeatureTexture.Reflective, FeatureSound.GlassClink, "镜子");
            AssertFeature(bathroom, 5, FeatureColor.Black, FeatureShape.Round, FeatureMaterial.Metal, FeatureTexture.Wet, FeatureSound.Ticking, "排水口");

            var kitchen = seq.GetLevel(4);
            AssertFeature(kitchen, 0, FeatureColor.White, FeatureShape.Square, FeatureMaterial.MetalPlastic, FeatureTexture.Smooth, FeatureSound.MetalMechanical, "电饭煲");
            AssertFeature(kitchen, 1, FeatureColor.Silver, FeatureShape.Curved, FeatureMaterial.Metal, FeatureTexture.Reflective, FeatureSound.Ticking, "水龙头");
            AssertFeature(kitchen, 6, FeatureColor.Gold, FeatureShape.Round, FeatureMaterial.Metal, FeatureTexture.Scaled, FeatureSound.Ticking, "钟表");

            var utility = seq.GetLevel(5);
            AssertFeature(utility, 0, FeatureColor.Beige, FeatureShape.Square, FeatureMaterial.Metal, FeatureTexture.PaintPeeled, FeatureSound.MetalMechanical, "旧电饭煲");
            AssertFeature(utility, 3, FeatureColor.Silver, FeatureShape.Irregular, FeatureMaterial.Glass, FeatureTexture.Fissure, FeatureSound.GlassClink, "镜子碎片");
            AssertFeature(utility, 5, FeatureColor.Black, FeatureShape.Irregular, FeatureMaterial.Metal, FeatureTexture.Scratched, FeatureSound.MetalMechanical, "折叠椅");
        }

        private static void AssertFeature(
            LevelData level,
            int index,
            FeatureColor color,
            FeatureShape shape,
            FeatureMaterial material,
            FeatureTexture texture,
            FeatureSound sound,
            string label)
        {
            Assert.IsNotNull(level, $"{label} 所在关卡为空");
            Assert.Greater(level.Items.Count, index, $"{label} 索引越界");

            var item = level.Items[index];
            Assert.IsTrue(item.OverrideFeatures, $"{label} 应覆盖五维特征");
            Assert.AreEqual(color, item.Color, $"{label} 颜色不匹配");
            Assert.AreEqual(shape, item.Shape, $"{label} 形状不匹配");
            Assert.AreEqual(material, item.Material, $"{label} 材质不匹配");
            Assert.AreEqual(texture, item.Texture, $"{label} 触感/纹理不匹配");
            Assert.AreEqual(sound, item.Sound, $"{label} 声音不匹配");
        }
    }
}
