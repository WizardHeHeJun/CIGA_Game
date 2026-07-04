// ------------------------------------------------------------
// AnchorHorrorLogicTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using System.Reflection;
using Ciga.AnchorHorror;
using NUnit.Framework;
using UnityEngine;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>
    /// EditMode 逻辑闭环回归（G1 网关）：三决策 + clamp 防死局 + 分级迟滞 + 端到端通关。
    /// 纯逻辑，不依赖场景/PlayMode。私有 [SerializeField] 字段用反射装配。
    /// </summary>
    public class AnchorHorrorLogicTests
    {
        private const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();
            Time.timeScale = 1f;
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
        }

        // ---------------------------------------------------------------- 决策 A：San 溢出截断

        [Test]
        public void SanityModify_ClampsToMax_NoOverflow()
        {
            var cfg = MakeConfig();
            var s = MakeSanity(cfg);

            s.Modify(50f);              // 100 + 50 → 截断 100
            Assert.AreEqual(100f, s.Current, 1e-4);

            s.Modify(-30f);             // 70
            s.Modify(50f);              // 70 + 50 → 截断 100
            Assert.AreEqual(100f, s.Current, 1e-4);

            s.Modify(-999f);            // 截断 0
            Assert.AreEqual(0f, s.Current, 1e-4);
        }

        // ---------------------------------------------------------------- San 分级迟滞

        [Test]
        public void SanityState_TransitionsThroughTiers_WithHysteresis()
        {
            var cfg = MakeConfig();     // 70/50/30, hysteresis 2
            var s = MakeSanity(cfg);
            Assert.AreEqual(SanityState.Normal, s.State);

            s.Modify(-31f);             // 69：在迟滞带内，仍 Normal
            Assert.AreEqual(SanityState.Normal, s.State);

            s.Modify(-2f);              // 67 <= 68 → Edge
            Assert.AreEqual(SanityState.Edge, s.State);

            s.Modify(-19f);             // 48 <= 48 → Distorted
            Assert.AreEqual(SanityState.Distorted, s.State);

            s.Modify(-20f);             // 28 <= 28 → Critical
            Assert.AreEqual(SanityState.Critical, s.State);

            s.Modify(-28f);             // 0 → Dead（终态）
            Assert.AreEqual(SanityState.Dead, s.State);
        }

        // ---------------------------------------------------------------- 决策 C：clamp 防死局

        [Test]
        public void ExtractTargets_ClampsRequiredToAvailable_NoDeadlock()
        {
            var cfg = MakeConfig(requiredMin: 3, requiredMax: 3);  // 强制想要 3
            var s = MakeSanity(cfg);
            var anchor = new AnchorSystem(cfg, null, s);

            // 候选物品：4 维都非 None → 4 个候选特征
            var candidate = MakeItem(FeatureColor.Red, FeatureShape.Round, FeatureMaterial.Wood, FeatureTexture.Smooth);
            anchor.CollectCandidate(candidate);

            // 关卡里每个特征只有 1 个物品（就是这件）
            var registry = new LevelFeatureRegistry();
            registry.Scan(new List<FeatureTag> { candidate });

            anchor.ExtractTargets(registry);

            Assert.IsTrue(anchor.Targets.Count >= 1);
            foreach (var t in anchor.Targets)
            {
                Assert.AreEqual(1, t.RequiredCount, "RequiredCount 必须被 clamp 到场景实际数量(1)，否则死局");
                Assert.LessOrEqual(t.RequiredCount, registry.CountOf(t.Feature));
            }
        }

        // ---------------------------------------------------------------- 背包：容量上限（SC-4）

        [Test]
        public void Inventory_TryAdd_RespectsCapacity()
        {
            var inv = new Inventory { Capacity = 2 };

            Assert.IsTrue(inv.TryAdd(MakeItem(FeatureColor.Red, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None)));
            Assert.IsTrue(inv.TryAdd(MakeItem(FeatureColor.Blue, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None)));
            Assert.IsFalse(
                inv.TryAdd(MakeItem(FeatureColor.None, FeatureShape.Round, FeatureMaterial.None, FeatureTexture.None)),
                "满则拒，不加");
            Assert.AreEqual(2, inv.Count);
        }

        // ---------------------------------------------------------------- 背包：覆盖 / 满足判定（SC-5）

        [Test]
        public void Inventory_CoversAndSatisfies_ByFeature()
        {
            var inv = new Inventory { Capacity = 8 };
            inv.TryAdd(MakeItem(FeatureColor.Red, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None));
            inv.TryAdd(MakeItem(FeatureColor.None, FeatureShape.None, FeatureMaterial.Wood, FeatureTexture.None));

            var red = new AnchorTarget(new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Red), 1);
            var wood = new AnchorTarget(new FeatureUnit(FeatureDimension.Material, (int)FeatureMaterial.Wood), 1);
            var blue = new AnchorTarget(new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Blue), 1);

            Assert.IsTrue(inv.Covers(red));
            Assert.IsTrue(inv.Covers(wood));
            Assert.IsFalse(inv.Covers(blue));

            Assert.IsTrue(inv.Satisfies(new List<AnchorTarget> { red, wood }), "全覆盖 → 满足");
            Assert.IsFalse(inv.Satisfies(new List<AnchorTarget> { red, blue }), "缺一 → 不满足");
        }

        // ---------------------------------------------------------------- 关卡1 抽锚点：去重 distinct + RequiredCount 恒 1（SC-2）

        [Test]
        public void ExtractTargetsFromSelection_DistinctFeatures_RequiredOne()
        {
            var cfg = MakeConfig(targetCount: 5);
            var s = MakeSanity(cfg);
            var anchor = new AnchorSystem(cfg, null, s);

            // 3 件已选，含重复特征（Red 出现 2 次）→ distinct = Red/Round/Wood/Blue/Smooth = 5
            var selected = new List<BackpackItem>
            {
                MakeBackpackItem(FeatureColor.Red, FeatureShape.Round, FeatureMaterial.None, FeatureTexture.None),
                MakeBackpackItem(FeatureColor.Red, FeatureShape.None, FeatureMaterial.Wood, FeatureTexture.None),
                MakeBackpackItem(FeatureColor.Blue, FeatureShape.None, FeatureMaterial.None, FeatureTexture.Smooth),
            };

            anchor.ExtractTargetsFromSelection(selected);

            Assert.AreEqual(5, anchor.Targets.Count, "distinct 特征恰好 5 个");
            var seen = new HashSet<FeatureUnit>();
            foreach (var t in anchor.Targets)
            {
                Assert.AreEqual(1, t.RequiredCount, "RequiredCount 恒为 1（新模型靠 Inventory 覆盖判定）");
                Assert.IsTrue(seen.Add(t.Feature), "锚点互不相同");
            }
        }

        // ---------------------------------------------------------------- 候选不足：fallback 补齐

        [Test]
        public void ExtractTargets_UsesFallback_WhenNoCandidates()
        {
            var cfg = MakeConfig(targetCount: 1);
            var s = MakeSanity(cfg);

            var level = ScriptableObject.CreateInstance<LevelConfig>();
            SetField(level, "_fallbackFeaturePool", new List<SerializableFeatureUnit>
            {
                MakeSfu(FeatureDimension.Color, (int)FeatureColor.Red),
            });

            var anchor = new AnchorSystem(cfg, level, s);   // 0 候选

            var levelItem = MakeItem(FeatureColor.Red, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None);
            var registry = new LevelFeatureRegistry();
            registry.Scan(new List<FeatureTag> { levelItem });

            anchor.ExtractTargets(registry);

            Assert.AreEqual(1, anchor.Targets.Count);
            Assert.AreEqual(new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Red), anchor.Targets[0].Feature);

            Object.DestroyImmediate(level);
        }

        // ---------------------------------------------------------------- 端到端：选物→抽锚→拾取入包→满足通关（SC-2/5）

        [Test]
        public void FullLoop_SelectExtractPickupSatisfy_Wins()
        {
            var cfg = MakeConfig(targetCount: 5);
            var s = MakeSanity(cfg);
            var anchor = new AnchorSystem(cfg, null, s);

            // 关卡1：选 5 件，每件一个非 None 特征 → distinct 5
            var selection = new List<BackpackItem>
            {
                MakeBackpackItem(FeatureColor.Red, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None),
                MakeBackpackItem(FeatureColor.Blue, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None),
                MakeBackpackItem(FeatureColor.None, FeatureShape.Round, FeatureMaterial.None, FeatureTexture.None),
                MakeBackpackItem(FeatureColor.None, FeatureShape.None, FeatureMaterial.Wood, FeatureTexture.None),
                MakeBackpackItem(FeatureColor.None, FeatureShape.None, FeatureMaterial.None, FeatureTexture.Smooth),
            };
            anchor.ExtractTargetsFromSelection(selection);
            Assert.AreEqual(5, anchor.Targets.Count);

            // 关卡2：往背包放恰好覆盖每个锚点的物品（cap 8）
            var backpack = new Inventory { Capacity = 8 };
            var snapshot = new List<AnchorTarget>(anchor.Targets);
            foreach (var target in snapshot)
            {
                Assert.IsTrue(backpack.TryAdd(MakeItemFromUnit(target.Feature)));
            }

            Assert.IsTrue(backpack.Satisfies(anchor.Targets), "背包覆盖全部 5 锚点 → 通关");
        }

        // ---------------------------------------------------------------- FeatureUnit 值语义

        [Test]
        public void FeatureUnit_ValueEquality_WorksInHashSet()
        {
            var a = new FeatureUnit(FeatureDimension.Color, 1);
            var b = new FeatureUnit(FeatureDimension.Color, 1);
            var c = new FeatureUnit(FeatureDimension.Shape, 1);

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);

            var set = new HashSet<FeatureUnit> { a, b, c };
            Assert.AreEqual(2, set.Count);
            Assert.IsTrue(set.Contains(new FeatureUnit(FeatureDimension.Color, 1)));
        }

        // ================================================================ helpers

        private GlobalConfig MakeConfig(int requiredMin = 1, int requiredMax = 3, int targetCount = 5)
        {
            var cfg = ScriptableObject.CreateInstance<GlobalConfig>();
            SetField(cfg, "_requiredCountMin", requiredMin);
            SetField(cfg, "_requiredCountMax", requiredMax);
            SetField(cfg, "_targetCount", targetCount);
            return cfg;
        }

        private SanitySystem MakeSanity(GlobalConfig cfg)
        {
            var go = new GameObject("Sanity");
            _spawned.Add(go);
            var s = go.AddComponent<SanitySystem>();
            s.Init(cfg);
            return s;
        }

        private FeatureTag MakeItem(FeatureColor c, FeatureShape sh, FeatureMaterial m, FeatureTexture t)
        {
            var go = new GameObject("Item");
            _spawned.Add(go);
            go.AddComponent<BoxCollider2D>();
            var tag = go.AddComponent<FeatureTag>();
            SetField(tag, "_color", c);
            SetField(tag, "_shape", sh);
            SetField(tag, "_material", m);
            SetField(tag, "_texture", t);
            // Awake/OnValidate 可能已用默认值建过缓存，强制按新值重建
            typeof(FeatureTag).GetMethod("RebuildFeatures", F).Invoke(tag, null);
            return tag;
        }

        private FeatureTag MakeItemFromUnit(FeatureUnit u)
        {
            var c = u.Dimension == FeatureDimension.Color ? (FeatureColor)u.Value : FeatureColor.None;
            var sh = u.Dimension == FeatureDimension.Shape ? (FeatureShape)u.Value : FeatureShape.None;
            var m = u.Dimension == FeatureDimension.Material ? (FeatureMaterial)u.Value : FeatureMaterial.None;
            var t = u.Dimension == FeatureDimension.Texture ? (FeatureTexture)u.Value : FeatureTexture.None;
            return MakeItem(c, sh, m, t);
        }

        private BackpackItem MakeBackpackItem(FeatureColor c, FeatureShape sh, FeatureMaterial m, FeatureTexture t)
        {
            var tag = MakeItem(c, sh, m, t);
            return new BackpackItem(tag.GetFeatures(), null, tag.name);
        }

        private static SerializableFeatureUnit MakeSfu(FeatureDimension dim, int value)
        {
            object boxed = new SerializableFeatureUnit();
            typeof(SerializableFeatureUnit).GetField("_dimension", F).SetValue(boxed, dim);
            typeof(SerializableFeatureUnit).GetField("_value", F).SetValue(boxed, value);
            return (SerializableFeatureUnit)boxed;
        }

        private static void SetField(object obj, string name, object value)
        {
            var f = obj.GetType().GetField(name, F);
            Assert.IsNotNull(f, $"字段 {name} 不存在于 {obj.GetType().Name}");
            f.SetValue(obj, value);
        }
    }
}
