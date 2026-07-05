// ------------------------------------------------------------
// FormalSceneArtSetupTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using Ciga.AnchorHorror.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>正式场景美术导入/关卡数据生成器的 EditMode 验证。</summary>
    public class FormalSceneArtSetupTests
    {
        private const string LevelsDir = "Assets/Res/AnchorHorror/Levels";
        private const string SequencePath = LevelsDir + "/Formal_Sequence.asset";

        [Test]
        public void BuildFormalSceneArt_CreatesAlignedSpritesAndCoveredSequence()
        {
            FormalSceneArtSetup.BuildAll();

            var seq = AssetDatabase.LoadAssetAtPath<LevelSequence>(SequencePath);
            Assert.IsNotNull(seq, "Formal_Sequence 未生成");
            Assert.AreEqual(6, seq.Count, "Formal_Sequence 应为 6 条 entry：关卡1 + 走廊 + 4 房间");
            Assert.AreEqual(LevelKind.Level1Select, seq.GetKind(0), "entry[0] 应为关卡1选择阶段");
            Assert.AreEqual(LevelKind.Level2Sub, seq.GetKind(1), "entry[1] 应为关卡2走廊");

            var level1 = seq.GetLevel(0);
            Assert.IsNotNull(level1, "entry[0].Level 为空");
            Assert.AreEqual(8, level1.Items.Count, "关卡1应有 8 个可选物件");

            for (int i = 0; i < level1.Items.Count; i++)
            {
                var item = level1.Items[i];
                Assert.IsTrue(item.OverrideSprite, $"关卡1物件[{i}] 应覆盖默认 Sprite");
                Assert.IsTrue(item.AlignWithBackground, $"关卡1物件[{i}] 应与背景同画布对齐");
                Assert.IsNotNull(item.Sprite, $"关卡1物件[{i}] Default Sprite 为空");
                Assert.Greater(item.ColliderSize.x, 0f, $"关卡1物件[{i}] ColliderSize.x 应 > 0");
                Assert.Greater(item.ColliderSize.y, 0f, $"关卡1物件[{i}] ColliderSize.y 应 > 0");
                Assert.IsFalse(item.VisualOnly, $"关卡1物件[{i}] 不应是纯视觉层");
            }

            Assert.IsTrue(LevelCoverageValidator.Validate(seq, out string report), report);
        }
    }
}
