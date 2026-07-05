// ------------------------------------------------------------
// TwoLevelFlowPlayTests.cs
// Author : WizardHeHeJun (QA)
// Created: 2026-07-05
// ------------------------------------------------------------
// 驱动式 PlayMode 集成测试：覆盖 SC-1~SC-6（两关卡锚点解谜流程迭代A）。
// 通过反射直接注入数据 + 调 GameManager 公开 API 驱动，绕过键盘/碰撞交互限制。
// 不加载场景，代码构建 GameManager 及全部依赖 SO，与 AnchorHorrorPlayTests 范式一致。
// ------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Ciga.AnchorHorror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ciga.AnchorHorror.PlayTests
{
    /// <summary>
    /// 两关卡流程集成测试（SC-1~SC-6）。
    /// 每个 [UnityTest] 独立构建并清理 GameManager，互不污染。
    /// </summary>
    public class TwoLevelFlowPlayTests
    {
        // ── 反射常量 ──────────────────────────────────────────
        private const BindingFlags PF = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags SF = BindingFlags.NonPublic | BindingFlags.Instance;

        // ── 测试上下文（每个测试共享的根对象及资产） ─────────
        private GameObject _root;
        private GlobalConfig _cfg;
        private LevelSequence _sequence;
        private LevelData _level1Data;
        private LevelData _corridorData;
        private LevelData _room1Data;
        private LevelData _room2Data;
        private LevelData _room3Data;
        private LevelData _room4Data;
        private ItemDatabase _itemDb;
        private GameManager _gm;

        // ── 清理 ──────────────────────────────────────────────
        [TearDown]
        public void TearDown()
        {
            EventBus.ClearAll();

            // 重置单例（自动属性 backing field），防止跨测试污染
            var backingField = typeof(GameManager).GetField("<Instance>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (backingField != null) { backingField.SetValue(null, null); }

            if (_root != null) { Object.DestroyImmediate(_root); }

            // 销毁 ScriptableObject 测试资产
            if (_cfg != null) { Object.DestroyImmediate(_cfg); }
            if (_sequence != null) { Object.DestroyImmediate(_sequence); }
            if (_level1Data != null) { Object.DestroyImmediate(_level1Data); }
            if (_corridorData != null) { Object.DestroyImmediate(_corridorData); }
            if (_room1Data != null) { Object.DestroyImmediate(_room1Data); }
            if (_room2Data != null) { Object.DestroyImmediate(_room2Data); }
            if (_room3Data != null) { Object.DestroyImmediate(_room3Data); }
            if (_room4Data != null) { Object.DestroyImmediate(_room4Data); }
            if (_itemDb != null) { Object.DestroyImmediate(_itemDb); }
        }

        // ── 辅助：构造完整 GameManager 及测试数据 ────────────

        /// <summary>
        /// 构建可用的 GameManager，注入代码生成的 LevelSequence+数据：
        ///   entries[0] = Level1（8物品，8种不同 non-None 特征）
        ///   entries[1] = Corridor（走廊 Hub）
        ///   entries[2..5] = Room1..4（四房间）
        /// 返回 GameManager，已激活（Awake+Start 已跑）。
        /// </summary>
        private IEnumerator BuildGameManager()
        {
            // --- 1. GlobalConfig -----------------------------------------------
            _cfg = ScriptableObject.CreateInstance<GlobalConfig>();
            SetPrivate(_cfg, "_sanityMax", 100f);
            SetPrivate(_cfg, "_sanityInit", 100f);
            SetPrivate(_cfg, "_decayPerSec", 0.5f);
            SetPrivate(_cfg, "_matchGain", 5f);
            SetPrivate(_cfg, "_mismatchLoss", 15f);
            SetPrivate(_cfg, "_thEdge", 70f);
            SetPrivate(_cfg, "_thDistorted", 50f);
            SetPrivate(_cfg, "_thCritical", 30f);
            SetPrivate(_cfg, "_hysteresis", 2f);
            SetPrivate(_cfg, "_candidateThreshold", 8);
            SetPrivate(_cfg, "_timeThreshold", 180f);
            SetPrivate(_cfg, "_targetCount", 5);
            SetPrivate(_cfg, "_requiredCountMin", 1);
            SetPrivate(_cfg, "_requiredCountMax", 1);
            SetPrivate(_cfg, "_interactRadius", 1.5f);
            SetPrivate(_cfg, "_moveSpeedPenalty", 0.2f);
            SetPrivate(_cfg, "_level1SelectCap", 5);
            SetPrivate(_cfg, "_level2BackpackCap", 8);
            SetPrivate(_cfg, "_level2TimeLimit", 180f);
            SetPrivate(_cfg, "_fadeDuration", 0f); // 0 秒 Fade，加速测试

            // --- 2. ItemDatabase（测试用，空数据库——items 用代码建 FeatureTag 绕过 DB）-----
            _itemDb = ScriptableObject.CreateInstance<ItemDatabase>();
            SetPrivate(_itemDb, "_items", new List<ItemDefinition>());
            SetPrivate(_itemDb, "_fallbackSprite", null);

            // --- 3. LevelData（Level1：8物品，各有1个不同 non-None 特征值）---------
            //   我们不通过 LevelSpawner 生成，而是在 tests 里直接 new GameObject + AddComponent<FeatureTag>
            //   LevelData 仅用于 GameManager._levelData != null 判定，实际 items 列表留空
            //   改用 items=null 场景：GameManager 会调 LevelSpawner，需要真实 ItemDatabase；
            //   方案：向 LevelData 注入1个空占位 item 让 _items 非空但 count=1，
            //         或直接填 items 列表为空 → LevelSpawner 静默跳过，然后手动挂物品到 _levelRoot
            //   选方案：items 列表为空 → Spawner 不生成，测试里手动建 FeatureTag 并模拟 SelectInLevel1
            _level1Data = BuildLevelData("TestLevel1", _itemDb);
            _corridorData = BuildLevelData("TestCorridor", _itemDb);
            _room1Data = BuildLevelData("TestRoom1", _itemDb);
            _room2Data = BuildLevelData("TestRoom2", _itemDb);
            _room3Data = BuildLevelData("TestRoom3", _itemDb);
            _room4Data = BuildLevelData("TestRoom4", _itemDb);

            // --- 4. LevelSequence（6 entries：L1 + Corridor + Room1..4）-------------
            _sequence = BuildLevelSequence(
                _level1Data, _corridorData, _room1Data, _room2Data, _room3Data, _room4Data);

            // --- 5. 构建 GameManager GameObject（先 SetActive(false) 再接线）-------
            _root = new GameObject("FlowTest_Root");
            _root.SetActive(false);

            var sanity = _root.AddComponent<SanitySystem>();
            var interaction = _root.AddComponent<InteractionSystem>();
            _gm = _root.AddComponent<GameManager>();

            SetPrivate(_gm, "_config", _cfg);
            SetPrivate(_gm, "_sanity", sanity);
            SetPrivate(_gm, "_interaction", interaction);
            SetPrivate(_gm, "_sequence", _sequence);
            // _transitionOverlay 留 null → Fade() 立即 yield break，不拖时间

            _root.SetActive(true); // Awake 跑：设单例、Init sanity、new Backpack/AnchorSystem
            yield return null;     // Start 跑：EnterInitRoom → SetPhase(InitRoom)
            yield return null;
        }

        // ── SC-1 / SC-2：关卡1 选满5件、锁定、锚点去重5个 ──────────────────

        [UnityTest]
        public IEnumerator SC1_SC2_SelectFiveInLevel1_LockAndExtract5DistinctTargets()
        {
            yield return BuildGameManager();

            // ── 前置断言：InitRoom、未锁定 ──
            Assert.AreEqual(GamePhase.InitRoom, _gm.CurrentPhase,
                "SC-1: 启动后应进入 InitRoom");
            Assert.IsFalse(_gm.SelectionLocked,
                "SC-1: 启动时 SelectionLocked 应为 false");
            Assert.AreEqual(0, _gm.Backpack.Count,
                "SC-1: 初始背包应为空");

            Debug.Log("[FLOWTEST] SC-1 前置断言 PASS");

            // ── 构建8个有不同特征的测试物品 ──
            // Level1 data: 8种特征，每个物品只含1个 non-None 特征
            var level1Features = new[]
            {
                new FeatureUnit(FeatureDimension.Color,    (int)FeatureColor.Red),
                new FeatureUnit(FeatureDimension.Color,    (int)FeatureColor.Blue),
                new FeatureUnit(FeatureDimension.Color,    (int)FeatureColor.Green),
                new FeatureUnit(FeatureDimension.Shape,    (int)FeatureShape.Round),
                new FeatureUnit(FeatureDimension.Shape,    (int)FeatureShape.Square),
                new FeatureUnit(FeatureDimension.Material, (int)FeatureMaterial.Wood),
                new FeatureUnit(FeatureDimension.Material, (int)FeatureMaterial.Metal),
                new FeatureUnit(FeatureDimension.Texture,  (int)FeatureTexture.Smooth),
            };

            var items = new FeatureTag[8];
            for (int i = 0; i < 8; i++)
            {
                items[i] = BuildFeatureTagWithOneFeature(level1Features[i]);
            }

            // ── 选前4件：未锁定，背包持续增长 ──
            for (int i = 0; i < 4; i++)
            {
                Assert.IsFalse(_gm.SelectionLocked, $"SC-1: 选第{i+1}件前仍未锁定");
                _gm.SelectInLevel1(items[i]);
                Assert.AreEqual(i + 1, _gm.Backpack.Count, $"SC-1: 选第{i+1}件后背包应有{i+1}件");
                Assert.IsFalse(items[i].CanInteract(GamePhase.InitRoom),
                    $"SC-1: 选过的物品 Consumed=true 后不可再交互");
            }

            Assert.IsFalse(_gm.SelectionLocked, "SC-1: 选4件后仍未锁定");

            // ── 选第5件：触发 LockSelection ──
            _gm.SelectInLevel1(items[4]);
            Assert.IsTrue(_gm.SelectionLocked, "SC-1: 选满5件后 SelectionLocked 应为 true");
            Assert.AreEqual(5, _gm.Backpack.Count, "SC-1: 选满后背包应有5件");

            Debug.Log("[FLOWTEST] SC-1 选满5件锁定 PASS");

            // ── 尝试选第6件：背包已满，CanInteract 封锁 ──
            Assert.IsFalse(items[5].CanInteract(GamePhase.InitRoom),
                "SC-1: 背包满(count>=cap)后第6件物品 CanInteract 应为 false");
            _gm.SelectInLevel1(items[5]); // 即使强行调也不应入包
            Assert.AreEqual(5, _gm.Backpack.Count, "SC-1: 强行 SelectInLevel1 第6件后背包仍应为5");

            Debug.Log("[FLOWTEST] SC-1 第6件封锁 PASS");

            // ── SC-2：锚点验证 ──
            var targets = _gm.Anchor.Targets;
            Assert.AreEqual(5, targets.Count, "SC-2: 锁定后应有5个目标锚点");

            // 5个锚点特征互不相同（HashSet去重后仍为5）
            var featureSet = new HashSet<FeatureUnit>();
            for (int i = 0; i < targets.Count; i++)
            {
                featureSet.Add(targets[i].Feature);
                Assert.AreEqual(1, targets[i].RequiredCount, "SC-2: RequiredCount 恒为1");
            }
            Assert.AreEqual(5, featureSet.Count, "SC-2: 5个锚点特征必须互不相同");

            // 特征来自已选物品的特征集（前5件物品的特征）
            var selectionFeatures = new HashSet<FeatureUnit>();
            for (int i = 0; i < 5; i++)
            {
                var feats = _gm.Backpack.Items[i].Features;
                for (int f = 0; f < feats.Count; f++)
                {
                    if (!feats[f].IsNone) { selectionFeatures.Add(feats[f]); }
                }
            }
            foreach (var t in targets)
            {
                Assert.IsTrue(selectionFeatures.Contains(t.Feature),
                    $"SC-2: 锚点 {t.Feature} 必须来自已选物品的特征集");
            }

            Debug.Log("[FLOWTEST] SC-2 5个distinct锚点验证 PASS");

            // ── SC-1: 门可交互性（LevelDoor.CanInteract 依赖 SelectionLocked） ──
            // 建测试门验证逻辑
            var doorGo = new GameObject("TestDoor_L1");
            doorGo.AddComponent<BoxCollider2D>();
            doorGo.AddComponent<SpriteRenderer>();
            var door = doorGo.AddComponent<LevelDoor>();
            door.Configure(DoorKind.EnterLevel2, null, "test");

            Assert.IsTrue(door.CanInteract(GamePhase.InitRoom),
                "SC-1: SelectionLocked=true 后 EnterLevel2 门应可交互");

            Object.DestroyImmediate(doorGo);

            Debug.Log("[FLOWTEST] SC-1 关卡1门可交互 PASS");

            // 清理物品
            for (int i = 0; i < 8; i++)
            {
                if (items[i] != null) { Object.DestroyImmediate(items[i].gameObject); }
            }
        }

        // ── SC-3：进入关卡2：背包清空、倒计时启动、相位变 HorrorLevel ───────

        [UnityTest]
        public IEnumerator SC3_EnterLevel2_ClearsBackpackAndStartsTimer()
        {
            yield return BuildGameManager();

            // 先走完关卡1选择流程
            yield return SelectFiveItemsInLevel1();

            // 调 EnterLevel2（异步，含Fade协程；_transitionOverlay=null，Fade立即完成）
            _gm.EnterLevel2();

            // 等待相位变为 HorrorLevel，最多等2秒
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_gm.CurrentPhase != GamePhase.HorrorLevel && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(GamePhase.HorrorLevel, _gm.CurrentPhase,
                "SC-3: EnterLevel2 后应进入 HorrorLevel（2秒内）");
            Assert.AreEqual(0, _gm.Backpack.Count,
                "SC-3: 进关卡2后背包应清空（Count==0）");
            Assert.AreEqual(8, _gm.Backpack.Capacity,
                "SC-3: 进关卡2后背包容量应为8");
            Assert.Greater(_gm.RemainingTime, 170f,
                "SC-3: 倒计时应约等于180（>170）");

            Debug.Log($"[FLOWTEST] SC-3 EnterLevel2 PASS: Phase={_gm.CurrentPhase}, BackpackCount={_gm.Backpack.Count}, RemainingTime={_gm.RemainingTime:F1}");
        }

        // ── SC-4：走廊/房间切换保留背包/锚点/倒计时/相位 ───────────────────────

        [UnityTest]
        public IEnumerator SC4_SwitchSubScene_PreservesBackpackAnchorAndTimer()
        {
            yield return BuildGameManager();
            yield return SelectFiveItemsInLevel1();

            // 进关卡2
            _gm.EnterLevel2();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_gm.CurrentPhase != GamePhase.HorrorLevel && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(GamePhase.HorrorLevel, _gm.CurrentPhase, "SC-4 前置：应在 HorrorLevel");

            // 记录切换前状态
            var targetsBeforeSwitch = new List<FeatureUnit>();
            foreach (var t in _gm.Anchor.Targets) { targetsBeforeSwitch.Add(t.Feature); }
            float timeBeforeSwitch = _gm.RemainingTime;

            // 在关卡2拾取1件物品（让背包非空，验证切换后保留）
            var testItem = BuildFeatureTagWithOneFeature(
                new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Red));
            _gm.PickupInLevel2(testItem);
            int backpackCountBeforeSwitch = _gm.Backpack.Count;

            // 调 SwitchSubScene：从走廊 entry[1] 进入房间1 entry[2]
            _gm.SwitchSubScene(2);

            // 等切换完成，最多2秒
            deadline = Time.realtimeSinceStartup + 2f;
            while (IsTransitioning(_gm) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            // 多等1帧让_transitioning恢复
            yield return null;

            // 验证：相位不变
            Assert.AreEqual(GamePhase.HorrorLevel, _gm.CurrentPhase,
                "SC-4: 切换走廊/房间后相位仍应为 HorrorLevel");

            // 验证：锚点集合未变（同一批特征）
            Assert.AreEqual(targetsBeforeSwitch.Count, _gm.Anchor.Targets.Count,
                "SC-4: 切换走廊/房间后锚点数量不变");
            var targetsAfterSwitch = new HashSet<FeatureUnit>();
            foreach (var t in _gm.Anchor.Targets) { targetsAfterSwitch.Add(t.Feature); }
            foreach (var f in targetsBeforeSwitch)
            {
                Assert.IsTrue(targetsAfterSwitch.Contains(f),
                    $"SC-4: 锚点特征 {f} 应在切换后保留");
            }

            // 验证：背包未清（保留拾取的物品）
            Assert.AreEqual(backpackCountBeforeSwitch, _gm.Backpack.Count,
                "SC-4: 切换走廊/房间后背包内容应保留");

            // 验证：倒计时未重置（<=切换前时间，且不为180）
            Assert.LessOrEqual(_gm.RemainingTime, timeBeforeSwitch + 0.5f,
                "SC-4: 切换走廊/房间后倒计时应不增加（未重置）");
            Assert.Less(_gm.RemainingTime, 180f - 0.001f,
                "SC-4: 切换走廊/房间后倒计时不应回到180");

            Debug.Log($"[FLOWTEST] SC-4 SwitchSubScene保留状态 PASS: RemainingTime={_gm.RemainingTime:F1}, BackpackCount={_gm.Backpack.Count}, Targets={_gm.Anchor.Targets.Count}");

            // 再切一次：从房间返回走廊
            _gm.SwitchSubScene(1);
            deadline = Time.realtimeSinceStartup + 2f;
            while (IsTransitioning(_gm) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            yield return null;

            Assert.AreEqual(GamePhase.HorrorLevel, _gm.CurrentPhase,
                "SC-4: 第二次切换后相位仍为 HorrorLevel");

            Debug.Log("[FLOWTEST] SC-4 房间返回走廊 PASS");

            if (testItem != null) { Object.DestroyImmediate(testItem.gameObject); }
        }

        // ── SC-5：关卡2 满足所有锚点 → Victory ──────────────────────────────

        [UnityTest]
        public IEnumerator SC5_PickupAllTargetFeatures_TriggerVictory()
        {
            yield return BuildGameManager();
            yield return SelectFiveItemsInLevel1();

            _gm.EnterLevel2();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_gm.CurrentPhase != GamePhase.HorrorLevel && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(GamePhase.HorrorLevel, _gm.CurrentPhase, "SC-5 前置：应在 HorrorLevel");

            // 取所有5个目标特征，构造包含该特征的物品并拾取
            var targets = _gm.Anchor.Targets;
            Assert.AreEqual(5, targets.Count, "SC-5 前置：应有5个锚点");

            for (int i = 0; i < targets.Count; i++)
            {
                // 若上一个拾取触发了 Victory 则提前退出
                if (_gm.CurrentPhase == GamePhase.Victory)
                {
                    break;
                }

                var item = BuildFeatureTagWithOneFeature(targets[i].Feature);
                _gm.PickupInLevel2(item);
                yield return null; // 让 EventBus 传播

                Debug.Log($"[FLOWTEST] SC-5 拾取目标{i+1}/{targets.Count}: {targets[i].Feature}, 背包={_gm.Backpack.Count}");

                if (item != null) { Object.DestroyImmediate(item.gameObject); }
            }

            yield return null; // 等 AllAnchorsActivated 事件 → EnterVictory 处理

            Assert.AreEqual(GamePhase.Victory, _gm.CurrentPhase,
                "SC-5: 拾取满足所有5个锚点后应触发 Victory");

            Debug.Log($"[FLOWTEST] SC-5 通关Victory PASS: Phase={_gm.CurrentPhase}");
        }

        // ── SC-6a：倒计时到0 → Fail ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator SC6a_TimerExpiry_TriggersFail()
        {
            yield return BuildGameManager();
            yield return SelectFiveItemsInLevel1();

            _gm.EnterLevel2();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_gm.CurrentPhase != GamePhase.HorrorLevel && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(GamePhase.HorrorLevel, _gm.CurrentPhase, "SC-6a 前置：应在 HorrorLevel");

            // 反射设 _remainingTime = 0.05f，下一帧 Update 就会触发 Fail
            SetPrivate(_gm, "_remainingTime", 0.05f);

            // 等 Fail 状态，最多1秒（实际0.05s后下一帧即触发）
            deadline = Time.realtimeSinceStartup + 1f;
            while (_gm.CurrentPhase != GamePhase.Fail && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(GamePhase.Fail, _gm.CurrentPhase,
                "SC-6a: 倒计时到0后应进入 Fail 状态");
            Assert.AreEqual(0f, _gm.RemainingTime, 1e-3f,
                "SC-6a: Fail 时 RemainingTime 应为0");

            Debug.Log($"[FLOWTEST] SC-6a 倒计时Fail PASS: Phase={_gm.CurrentPhase}, RemainingTime={_gm.RemainingTime}");
        }

        // ── SC-6b：直接调 Fail（San归0模拟）→ Fail ───────────────────────────

        [UnityTest]
        public IEnumerator SC6b_DirectFail_EntersFailPhase()
        {
            yield return BuildGameManager();
            yield return SelectFiveItemsInLevel1();

            _gm.EnterLevel2();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_gm.CurrentPhase != GamePhase.HorrorLevel && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(GamePhase.HorrorLevel, _gm.CurrentPhase, "SC-6b 前置：应在 HorrorLevel");

            // 直接调 Fail（模拟 San 归0触发路径）
            _gm.Fail();
            yield return null;

            Assert.AreEqual(GamePhase.Fail, _gm.CurrentPhase,
                "SC-6b: 直接调 Fail() 应进入 Fail 状态");

            Debug.Log($"[FLOWTEST] SC-6b 直接Fail PASS: Phase={_gm.CurrentPhase}");

            // 验证 Fail 幂等（再调不崩、相位不变）
            _gm.Fail();
            Assert.AreEqual(GamePhase.Fail, _gm.CurrentPhase, "SC-6b: Fail 幂等");

            Debug.Log("[FLOWTEST] SC-6b Fail幂等 PASS");
        }

        // ── SC-6c：San归0事件链 → Fail ────────────────────────────────────────

        [UnityTest]
        public IEnumerator SC6c_SanityDead_TriggersFail()
        {
            yield return BuildGameManager();
            yield return SelectFiveItemsInLevel1();

            _gm.EnterLevel2();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_gm.CurrentPhase != GamePhase.HorrorLevel && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(GamePhase.HorrorLevel, _gm.CurrentPhase, "SC-6c 前置：应在 HorrorLevel");

            // 获取 SanitySystem 并直接降到0
            var sanity = _root.GetComponent<SanitySystem>();
            Assert.IsNotNull(sanity, "SC-6c 前置：SanitySystem 应存在");

            sanity.Modify(-200f); // 强制归0
            yield return null;    // SanityStateChanged 事件传播 → GameManager.OnSanityStateChanged → Fail

            Assert.AreEqual(GamePhase.Fail, _gm.CurrentPhase,
                "SC-6c: San 归0后 SanityStateChanged(Dead) 事件应触发 Fail");

            Debug.Log($"[FLOWTEST] SC-6c San归0Fail PASS: Phase={_gm.CurrentPhase}");
        }

        // ── 辅助方法 ──────────────────────────────────────────────────────────

        /// <summary>选满5个物品完成关卡1流程（用于其他测试的前置步骤）。</summary>
        private IEnumerator SelectFiveItemsInLevel1()
        {
            var level1Features = new[]
            {
                new FeatureUnit(FeatureDimension.Color,    (int)FeatureColor.Red),
                new FeatureUnit(FeatureDimension.Color,    (int)FeatureColor.Blue),
                new FeatureUnit(FeatureDimension.Color,    (int)FeatureColor.Green),
                new FeatureUnit(FeatureDimension.Shape,    (int)FeatureShape.Round),
                new FeatureUnit(FeatureDimension.Shape,    (int)FeatureShape.Square),
            };

            for (int i = 0; i < 5; i++)
            {
                var item = BuildFeatureTagWithOneFeature(level1Features[i]);
                _gm.SelectInLevel1(item);
                yield return null;
                Object.DestroyImmediate(item.gameObject);
            }

            Assert.IsTrue(_gm.SelectionLocked, "前置：SelectFiveItemsInLevel1 后应已锁定");
        }

        /// <summary>构造只含1个 non-None 特征的 FeatureTag（用于测试注入）。</summary>
        private static FeatureTag BuildFeatureTagWithOneFeature(FeatureUnit feature)
        {
            var go = new GameObject($"TestItem_{feature.Dimension}_{feature.Value}");
            go.AddComponent<BoxCollider2D>(); // FeatureTag RequireComponent(Collider2D)

            // 根据 FeatureDimension 设置对应枚举
            FeatureColor color = FeatureColor.None;
            FeatureShape shape = FeatureShape.None;
            FeatureMaterial material = FeatureMaterial.None;
            FeatureTexture texture = FeatureTexture.None;
            FeatureSound sound = FeatureSound.None;

            switch (feature.Dimension)
            {
                case FeatureDimension.Color:
                    color = (FeatureColor)feature.Value;
                    break;
                case FeatureDimension.Shape:
                    shape = (FeatureShape)feature.Value;
                    break;
                case FeatureDimension.Material:
                    material = (FeatureMaterial)feature.Value;
                    break;
                case FeatureDimension.Texture:
                    texture = (FeatureTexture)feature.Value;
                    break;
                case FeatureDimension.Sound:
                    sound = (FeatureSound)feature.Value;
                    break;
            }

            var tag = go.AddComponent<FeatureTag>();
            tag.Configure(color, shape, material, texture, sound);
            return tag;
        }

        /// <summary>构造测试用 LevelData（items 列表为空，LevelSpawner 静默跳过）。</summary>
        private static LevelData BuildLevelData(string name, ItemDatabase db)
        {
            var data = ScriptableObject.CreateInstance<LevelData>();
            SetPrivateStatic(data, "_levelName", name);
            SetPrivateStatic(data, "_items", new List<PlacedItem>());
            SetPrivateStatic(data, "_itemDatabase", db);
            SetPrivateStatic(data, "_levelConfig", null);
            SetPrivateStatic(data, "_playerSpawn", Vector2.zero);
            return data;
        }

        /// <summary>构造6-entry LevelSequence：entries[0]=L1，entries[1]=Corridor，entries[2..5]=Room1..4。</summary>
        private static LevelSequence BuildLevelSequence(
            LevelData l1,
            LevelData corridor,
            LevelData room1,
            LevelData room2,
            LevelData room3,
            LevelData room4)
        {
            var seq = ScriptableObject.CreateInstance<LevelSequence>();

            // 构造 LevelSequence.Entry，私有字段用反射
            var entryType = typeof(LevelSequence).GetNestedType("Entry",
                BindingFlags.Public | BindingFlags.NonPublic);
            var doorSettingType = typeof(LevelSequence).GetNestedType("DoorSetting",
                BindingFlags.Public | BindingFlags.NonPublic);

            var entries = new System.Collections.Generic.List<object>();

            // Entry[0]: L1（EnterLevel2 door）
            entries.Add(MakeEntry(entryType, doorSettingType, l1, LevelKind.Level1Select, DoorKind.EnterLevel2));
            // Entry[1]: Corridor（四扇房间门由 GameManager 程序化生成）
            entries.Add(MakeEntry(entryType, doorSettingType, corridor, LevelKind.Level2Sub, DoorKind.EnterRoom1));
            // Entry[2..5]: Rooms（ReturnToCorridor door）
            entries.Add(MakeEntry(entryType, doorSettingType, room1, LevelKind.Level2Sub, DoorKind.ReturnToCorridor));
            entries.Add(MakeEntry(entryType, doorSettingType, room2, LevelKind.Level2Sub, DoorKind.ReturnToCorridor));
            entries.Add(MakeEntry(entryType, doorSettingType, room3, LevelKind.Level2Sub, DoorKind.ReturnToCorridor));
            entries.Add(MakeEntry(entryType, doorSettingType, room4, LevelKind.Level2Sub, DoorKind.ReturnToCorridor));

            // _entries 是 List<LevelSequence.Entry>，需要把 object List 转成正确类型
            var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(entryType);
            var typedList = System.Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add");
            foreach (var e in entries)
            {
                addMethod.Invoke(typedList, new[] { e });
            }

            typeof(LevelSequence).GetField("_entries", SF).SetValue(seq, typedList);
            return seq;
        }

        private static object MakeEntry(
            System.Type entryType,
            System.Type doorSettingType,
            LevelData level,
            LevelKind kind,
            DoorKind doorKind)
        {
            var entry = System.Activator.CreateInstance(entryType);
            entryType.GetField("_level", PF).SetValue(entry, level);
            entryType.GetField("_kind", PF).SetValue(entry, kind);
            entryType.GetField("_doorKind", PF).SetValue(entry, doorKind);

            var doorSetting = System.Activator.CreateInstance(doorSettingType);
            doorSettingType.GetField("_spawn", PF).SetValue(doorSetting, Vector2.zero);
            doorSettingType.GetField("_sprite", PF).SetValue(doorSetting, null);
            doorSettingType.GetField("_prompt", PF).SetValue(doorSetting, "test door");
            entryType.GetField("_door", PF).SetValue(entry, doorSetting);

            return entry;
        }

        /// <summary>检查 GameManager 是否仍在过渡中（读私有 _transitioning 字段）。</summary>
        private static bool IsTransitioning(GameManager gm)
        {
            return (bool)typeof(GameManager).GetField("_transitioning", PF).GetValue(gm);
        }

        /// <summary>对任意 object 的私有实例字段赋值（反射辅助）。</summary>
        private static void SetPrivate(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, PF);
            if (field == null)
            {
                // 尝试父类
                var t = obj.GetType().BaseType;
                while (t != null && field == null)
                {
                    field = t.GetField(fieldName, PF);
                    t = t.BaseType;
                }
            }
            Assert.IsNotNull(field, $"反射找不到字段: {fieldName} on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        /// <summary>静态版本（用于 SO 实例且不在测试类 fixture 上下文内的情况）。</summary>
        private static void SetPrivateStatic(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) { return; } // 字段不存在则静默
            field.SetValue(obj, value);
        }
    }
}
