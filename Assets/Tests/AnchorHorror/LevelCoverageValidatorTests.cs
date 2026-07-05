// ------------------------------------------------------------
// LevelCoverageValidatorTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
// 迭代B SC-B2：死局检测校验器 EditMode 测试。
//   正例：加载可解的 demo LevelSequence → Validate 通过。
//   负例：合成「关卡1有 Red、关卡2只有 Blue」的序列 → Validate 报死局、报告含缺失特征。
// ------------------------------------------------------------
using System.Collections.Generic;
using System.Reflection;
using Ciga.AnchorHorror;
using Ciga.AnchorHorror.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.Tests
{
    public class LevelCoverageValidatorTests
    {
        private const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _assets.Count; i++)
            {
                if (_assets[i] != null)
                {
                    Object.DestroyImmediate(_assets[i]);
                }
            }

            _assets.Clear();
        }

        // ── 正例：可解 demo 应通过 ──────────────────────────────
        [Test]
        public void DemoSequence_PassesCoverage()
        {
            var seq = AssetDatabase.LoadAssetAtPath<LevelSequence>(
                "Assets/Res/AnchorHorror/Levels/DemoTwoLevelFlow_Sequence.asset");
            Assert.IsNotNull(seq, "找不到 demo LevelSequence 资产（迭代A demo 数据应已在盘）");

            bool ok = LevelCoverageValidator.Validate(seq, out string report);
            Assert.IsTrue(ok, $"demo 数据构造上可解，应通过。报告：{report}");
        }

        // ── 负例：关卡2 缺关卡1 的特征 → 报死局 ─────────────────
        [Test]
        public void MissingFeatureInLevel2_ReportsDeadlock()
        {
            // 关卡1：一个 Red 物品；关卡2：一个 Blue 物品（缺 Red）
            var l1 = MakeLevelData("L1", MakePlaced(FeatureColor.Red));
            var sub = MakeLevelData("Sub", MakePlaced(FeatureColor.Blue));
            var seq = MakeSequence(
                (l1, LevelKind.Level1Select, DoorKind.EnterLevel2),
                (sub, LevelKind.Level2Sub, DoorKind.ReturnToCorridor));

            bool ok = LevelCoverageValidator.Validate(seq, out string report);
            Assert.IsFalse(ok, "关卡2 缺 Red，应报死局");
            StringAssert.Contains("Red", report, "报告应指出缺失的 Red 特征");
        }

        // ── 正例（合成）：关卡2 覆盖关卡1 → 通过 ────────────────
        [Test]
        public void SyntheticCovered_Passes()
        {
            var l1 = MakeLevelData("L1", MakePlaced(FeatureColor.Red), MakePlaced(FeatureShape.Round));
            var sub = MakeLevelData("Sub", MakePlaced(FeatureColor.Red), MakePlaced(FeatureShape.Round));
            var seq = MakeSequence(
                (l1, LevelKind.Level1Select, DoorKind.EnterLevel2),
                (sub, LevelKind.Level2Sub, DoorKind.ReturnToCorridor));

            bool ok = LevelCoverageValidator.Validate(seq, out string report);
            Assert.IsTrue(ok, $"关卡2 覆盖了关卡1 全部特征，应通过。报告：{report}");
        }

        // ── 合成辅助（反射构造私有序列化字段） ──────────────────

        private PlacedItem MakePlaced(FeatureColor color)
        {
            return MakePlaced(color, FeatureShape.None);
        }

        private PlacedItem MakePlaced(FeatureShape shape)
        {
            return MakePlaced(FeatureColor.None, shape);
        }

        private PlacedItem MakePlaced(FeatureColor color, FeatureShape shape)
        {
            var placed = new PlacedItem();
            SetField(placed, "_overrideFeatures", true);
            SetField(placed, "_color", color);
            SetField(placed, "_shape", shape);
            SetField(placed, "_material", FeatureMaterial.None);
            SetField(placed, "_texture", FeatureTexture.None);
            SetField(placed, "_sound", FeatureSound.None);
            return placed;
        }

        private LevelData MakeLevelData(string name, params PlacedItem[] items)
        {
            var data = ScriptableObject.CreateInstance<LevelData>();
            _assets.Add(data);
            SetField(data, "_levelName", name);
            SetField(data, "_items", new List<PlacedItem>(items));
            SetField(data, "_itemDatabase", null);
            return data;
        }

        private LevelSequence MakeSequence(params (LevelData data, LevelKind kind, DoorKind doorKind)[] entries)
        {
            var seq = ScriptableObject.CreateInstance<LevelSequence>();
            _assets.Add(seq);

            var entryType = typeof(LevelSequence).GetNestedType("Entry", BindingFlags.Public | BindingFlags.NonPublic);
            var doorType = typeof(LevelSequence).GetNestedType("DoorSetting", BindingFlags.Public | BindingFlags.NonPublic);
            var listType = typeof(List<>).MakeGenericType(entryType);
            var typedList = System.Activator.CreateInstance(listType);
            var add = listType.GetMethod("Add");

            foreach (var e in entries)
            {
                var entry = System.Activator.CreateInstance(entryType);
                entryType.GetField("_level", F).SetValue(entry, e.data);
                entryType.GetField("_kind", F).SetValue(entry, e.kind);
                entryType.GetField("_doorKind", F).SetValue(entry, e.doorKind);

                var door = System.Activator.CreateInstance(doorType);
                doorType.GetField("_spawn", F).SetValue(door, Vector2.zero);
                doorType.GetField("_prompt", F).SetValue(door, "test");
                entryType.GetField("_door", F).SetValue(entry, door);

                add.Invoke(typedList, new[] { entry });
            }

            typeof(LevelSequence).GetField("_entries", F).SetValue(seq, typedList);
            return seq;
        }

        private static void SetField(object obj, string name, object value)
        {
            var f = obj.GetType().GetField(name, F);
            Assert.IsNotNull(f, $"字段 {name} 不存在于 {obj.GetType().Name}");
            f.SetValue(obj, value);
        }
    }
}
