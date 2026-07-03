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

        // ---------------------------------------------------------------- 决策 A/匹配：命中回 San + 消耗 + 激活

        [Test]
        public void TryMatch_Hit_GainsSanity_Consumes_Activates()
        {
            var cfg = MakeConfig();
            var s = MakeSanity(cfg);
            s.Modify(-20f);             // 80，便于观察 +5
            var anchor = new AnchorSystem(cfg, null, s);
            InjectTarget(anchor, new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Red), 1);

            bool activated = false;
            bool allActivated = false;
            EventBus.AnchorActivated += _ => activated = true;
            EventBus.AllAnchorsActivated += () => allActivated = true;

            var item = MakeItem(FeatureColor.Red, FeatureShape.Round, FeatureMaterial.Wood, FeatureTexture.Smooth);
            anchor.TryMatch(item);

            Assert.AreEqual(85f, s.Current, 1e-4);
            Assert.IsTrue(item.Consumed);
            Assert.IsTrue(activated);
            Assert.IsTrue(allActivated, "唯一锚点激活即全激活");
        }

        // ---------------------------------------------------------------- 决策 B：不匹配只扣一次

        [Test]
        public void TryMatch_Mismatch_LosesSanityOnce_ThenConsumed()
        {
            var cfg = MakeConfig();
            var s = MakeSanity(cfg);    // 100
            var anchor = new AnchorSystem(cfg, null, s);
            InjectTarget(anchor, new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Blue), 1);

            int mismatchCount = 0;
            EventBus.ItemMismatched += _ => mismatchCount++;

            var item = MakeItem(FeatureColor.Red, FeatureShape.Round, FeatureMaterial.Wood, FeatureTexture.Smooth);

            anchor.TryMatch(item);      // 不匹配 → -15 → 85，消耗
            Assert.AreEqual(85f, s.Current, 1e-4);
            Assert.IsTrue(item.Consumed);
            Assert.AreEqual(1, mismatchCount);

            anchor.TryMatch(item);      // 已消耗 → 无操作，不再扣分
            Assert.AreEqual(85f, s.Current, 1e-4);
            Assert.AreEqual(1, mismatchCount);
        }

        // ---------------------------------------------------------------- 一物命中多锚点叠加回 San

        [Test]
        public void TryMatch_OneItemHitsMultipleAnchors_StacksGain()
        {
            var cfg = MakeConfig();
            var s = MakeSanity(cfg);
            s.Modify(-20f);             // 80
            var anchor = new AnchorSystem(cfg, null, s);
            InjectTarget(anchor, new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Red), 1);
            InjectTarget(anchor, new FeatureUnit(FeatureDimension.Material, (int)FeatureMaterial.Wood), 1);

            List<FeatureUnit> hits = null;
            bool allActivated = false;
            EventBus.ItemMatched += (_, h) => hits = new List<FeatureUnit>(h);
            EventBus.AllAnchorsActivated += () => allActivated = true;

            var item = MakeItem(FeatureColor.Red, FeatureShape.Round, FeatureMaterial.Wood, FeatureTexture.Smooth);
            anchor.TryMatch(item);

            Assert.AreEqual(90f, s.Current, 1e-4);   // 80 + 5 + 5
            Assert.AreEqual(2, hits.Count);
            Assert.IsTrue(allActivated);
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

        // ---------------------------------------------------------------- 端到端：收集→抽锚→逐一匹配→全激活

        [Test]
        public void FullLoop_CollectExtractMatch_AllActivated()
        {
            var cfg = MakeConfig(requiredMin: 1, requiredMax: 1, targetCount: 5);
            var s = MakeSanity(cfg);
            var anchor = new AnchorSystem(cfg, null, s);

            // 5 件候选，每件只有一个非 None 特征 → 恰好 5 个候选特征
            var candidates = new List<FeatureTag>
            {
                MakeItem(FeatureColor.Red, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None),
                MakeItem(FeatureColor.Blue, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None),
                MakeItem(FeatureColor.None, FeatureShape.Round, FeatureMaterial.None, FeatureTexture.None),
                MakeItem(FeatureColor.None, FeatureShape.None, FeatureMaterial.Wood, FeatureTexture.None),
                MakeItem(FeatureColor.None, FeatureShape.None, FeatureMaterial.None, FeatureTexture.Smooth),
            };
            foreach (var c in candidates)
            {
                anchor.CollectCandidate(c);
            }

            var registry = new LevelFeatureRegistry();
            registry.Scan(candidates);

            anchor.ExtractTargets(registry);
            Assert.AreEqual(5, anchor.Targets.Count);

            bool allActivated = false;
            EventBus.AllAnchorsActivated += () => allActivated = true;

            // 为每个锚点造一个恰好匹配它的物品并匹配
            var snapshot = new List<AnchorTarget>(anchor.Targets);
            foreach (var target in snapshot)
            {
                var item = MakeItemFromUnit(target.Feature);
                anchor.TryMatch(item);
            }

            Assert.IsTrue(allActivated, "逐一匹配全部锚点后应触发通关");
            foreach (var t in anchor.Targets)
            {
                Assert.IsTrue(t.IsActivated);
            }
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

        private static void InjectTarget(AnchorSystem anchor, FeatureUnit feature, int required)
        {
            var field = typeof(AnchorSystem).GetField("_targets", F);
            var list = (System.Collections.IList)field.GetValue(anchor);
            list.Add(new AnchorTarget(feature, required));
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
