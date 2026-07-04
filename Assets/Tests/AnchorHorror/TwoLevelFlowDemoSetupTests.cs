// ------------------------------------------------------------
// TwoLevelFlowDemoSetupTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// Desc   : 触发两关卡流程 Demo 数据生成，并断言关键资产落盘。
//          借 EditMode 测试跑生成器（验证桥无法直接执行菜单项）。
// ------------------------------------------------------------
using Ciga.AnchorHorror.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>
    /// 触发 TwoLevelFlowDemoSetup.BuildAll()，断言 4 个 LevelData + LevelSequence 落盘，
    /// 且 LevelSequence 共 4 条 entry（entries[0..3]）。
    /// </summary>
    public class TwoLevelFlowDemoSetupTests
    {
        private const string LevelsDir = "Assets/Res/AnchorHorror/Levels";

        [Test]
        public void BuildDemoData_CreatesAllAssetsAndWiresSequence()
        {
            // 执行生成器（幂等，已存在则覆盖重建）
            TwoLevelFlowDemoSetup.BuildAll();

            // 4 个 LevelData
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoTwoLevelFlow_Level1.asset"),
                "关卡1 LevelData 未生成");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoSub1.asset"),
                "子场景1 LevelData 未生成");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoSub2.asset"),
                "子场景2 LevelData 未生成");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoSub3.asset"),
                "子场景3 LevelData 未生成");

            // LevelSequence
            var seq = AssetDatabase.LoadAssetAtPath<LevelSequence>(
                LevelsDir + "/DemoTwoLevelFlow_Sequence.asset");
            Assert.IsNotNull(seq, "LevelSequence 未生成");

            // 4 条 entry：entry[0]=Level1Select, entry[1..3]=Level2Sub
            Assert.AreEqual(4, seq.Count, $"LevelSequence 应有 4 条 entry，实际 {seq.Count}");
            Assert.AreEqual(LevelKind.Level1Select, seq.GetKind(0), "entry[0] 应为 Level1Select");
            Assert.AreEqual(DoorKind.EnterLevel2,  seq.GetDoorKind(0), "entry[0] 门应为 EnterLevel2");

            for (int i = 1; i <= 3; i++)
            {
                Assert.AreEqual(LevelKind.Level2Sub,     seq.GetKind(i),     $"entry[{i}] 应为 Level2Sub");
                Assert.AreEqual(DoorKind.SwitchSubScene, seq.GetDoorKind(i), $"entry[{i}] 门应为 SwitchSubScene");
            }

            // 关卡1 有 8 个物品
            var level1 = seq.GetLevel(0);
            Assert.IsNotNull(level1, "序列 entries[0].Level 为 null");
            Assert.AreEqual(8, level1.Items.Count, $"关卡1 应有 8 物品，实际 {level1.Items.Count}");

            // 关卡1物品均设 OverrideFeatures=true（保证特征精确可控）
            for (int i = 0; i < level1.Items.Count; i++)
            {
                Assert.IsTrue(level1.Items[i].OverrideFeatures,
                    $"关卡1 物品[{i}] OverrideFeatures 未设为 true");
            }

            // Bootstrap 已接线（_sequence 非 null）——通过 GameManager 组件验证
            var gos = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Res/AnchorHorror/Bootstrap.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Additive);
            bool found = false;
            foreach (var root in gos.GetRootGameObjects())
            {
                var gm = root.GetComponent<GameManager>();
                if (gm == null)
                {
                    continue;
                }

                var so = new SerializedObject(gm);
                var prop = so.FindProperty("_sequence");
                if (prop != null && prop.objectReferenceValue != null)
                {
                    found = true;
                    break;
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(gos, true);
            Assert.IsTrue(found, "Bootstrap.unity 里 GameManager._sequence 未接线");
        }
    }
}
