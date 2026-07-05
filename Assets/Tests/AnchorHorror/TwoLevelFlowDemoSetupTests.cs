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
    /// 触发 TwoLevelFlowDemoSetup.BuildAll()，断言 6 个 LevelData + LevelSequence 落盘，
    /// 且 LevelSequence 共 6 条 entry（entries[0..5]）。
    /// </summary>
    public class TwoLevelFlowDemoSetupTests
    {
        private const string LevelsDir = "Assets/Res/AnchorHorror/Levels";

        [Test]
        public void BuildDemoData_CreatesAllAssetsAndWiresSequence()
        {
            // 执行生成器（幂等，已存在则覆盖重建）
            TwoLevelFlowDemoSetup.BuildAll();

            // 6 个 LevelData：关卡1 + 走廊 + 四房间
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoTwoLevelFlow_Level1.asset"),
                "关卡1 LevelData 未生成");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoCorridor.asset"),
                "走廊 LevelData 未生成");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoRoom1.asset"),
                "房间1 LevelData 未生成");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoRoom2.asset"),
                "房间2 LevelData 未生成");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoRoom3.asset"),
                "房间3 LevelData 未生成");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + "/DemoRoom4.asset"),
                "房间4 LevelData 未生成");

            // LevelSequence
            var seq = AssetDatabase.LoadAssetAtPath<LevelSequence>(
                LevelsDir + "/DemoTwoLevelFlow_Sequence.asset");
            Assert.IsNotNull(seq, "LevelSequence 未生成");

            // 6 条 entry：entry[0]=Level1Select，entry[1]=走廊，entry[2..5]=四房间
            Assert.AreEqual(6, seq.Count, $"LevelSequence 应有 6 条 entry，实际 {seq.Count}");
            Assert.AreEqual(LevelKind.Level1Select, seq.GetKind(0), "entry[0] 应为 Level1Select");
            Assert.AreEqual(DoorKind.EnterLevel2,  seq.GetDoorKind(0), "entry[0] 门应为 EnterLevel2");
            Assert.AreEqual(LevelKind.Level2Sub, seq.GetKind(1), "entry[1] 应为第二关走廊");
            Assert.AreEqual(DoorKind.EnterRoom1, seq.GetDoorKind(1), "entry[1] 门类型应为走廊房间门占位");

            for (int i = 2; i <= 5; i++)
            {
                Assert.AreEqual(LevelKind.Level2Sub,      seq.GetKind(i),     $"entry[{i}] 应为 Level2Sub");
                Assert.AreEqual(DoorKind.ReturnToCorridor, seq.GetDoorKind(i), $"entry[{i}] 门应为 ReturnToCorridor");
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
