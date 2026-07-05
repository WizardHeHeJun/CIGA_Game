// ------------------------------------------------------------
// GameManager.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using Ciga.Startup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 游戏总控：轻量单例 + 组合根 + 阶段状态机。
    /// 两关卡流程（ADR-1/2/3/4/5/6）：
    ///   关卡1（InitRoom）：从 8 物品选 5 → LockSelection 抽 5 锚点 → 关卡1门可交互
    ///   关卡2（HorrorLevel）：EnterLevel2 进入 → 180s 倒计时 + San 衰减双失败线 → 拾取满足 5 锚点 → Victory
    ///   子场景切换（SwitchSubScene）：换物品摆放、背包/锚点/倒计时/相位全保留
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("配置")]
        [SerializeField] private GlobalConfig _config;
        [SerializeField] private FeatureDatabase _database;
        [SerializeField] private LevelConfig _levelConfig;

        [Header("关卡序列（ADR-1）")]
        [SerializeField] private LevelSequence _sequence;

        [Header("系统 / 角色（挂在常驻层级上）")]
        [SerializeField] private SanitySystem _sanity;
        [SerializeField] private InteractionSystem _interaction;
        [SerializeField] private PlayerController2D _player;

        [Tooltip("镜头跟随组件（CameraRig 上）。留空则 Awake 自动 FindObjectOfType 兜底。")]
        [SerializeField] private CameraFollow2D _cameraFollow;

        [Tooltip("玩家精灵半身留白：边界内缩这么多，防止玩家贴边时半个身子露出背景外。")]
        [SerializeField] private float _boundsPadding = 0.5f;

        [Header("过渡")]
        [SerializeField] private SpriteRenderer _transitionOverlay;
        [SerializeField] private AudioSource _whisperSource;

        [Header("教程（迭代B，可选）")]
        [SerializeField] private TutorialPanel _tutorial;

        private bool _transitioning;
        private bool _initialized;
        private string _startSceneName;
        private GameObject _levelRoot;

        // 关卡序列推进（实例字段，重开局场景重载后天然归零——陷阱 9）
        private int _levelIndex;
        private LevelData _levelData;

        // 背包（普通类实例，GameManager 持有，ADR-2）
        private Inventory _backpack;

        // 关卡2 倒计时（仅 HorrorLevel 递减，独立字段不与旧 InitRoom 兜底计时混用——陷阱 6）
        private float _remainingTime;

        // 拾取反馈"新命中"缓冲（复用避免每次拾取分配，SC-B4）
        private readonly List<FeatureUnit> _pickupHitBuffer = new List<FeatureUnit>();

        // 关卡1 门引用（LockSelection 后开启可交互）
        private LevelDoor _level1Door;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;
        public AnchorSystem Anchor { get; private set; }
        public GlobalConfig Config => _config;
        public FeatureDatabase Database => _database;

        /// <summary>关卡1 已选满 5 件且已抽锚点（关卡1门可交互条件之一，SC-1）。</summary>
        public bool SelectionLocked { get; private set; }

        /// <summary>关卡2 倒计时剩余秒数（供 CountdownPanel，SC-6）。</summary>
        public float RemainingTime => _remainingTime;

        /// <summary>背包只读引用（供 MemoryPanel / FeatureTag.CanInteract，ADR-2）。</summary>
        public Inventory Backpack => _backpack;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _startSceneName = gameObject.scene.name;
            DontDestroyOnLoad(gameObject);

            if (_config == null)
            {
                Debug.LogError("[AnchorHorror] GameManager 缺少 GlobalConfig，禁用。请在 Inspector 挂上配置。");
                enabled = false;
                return;
            }

            if (_sanity == null)
            {
                _sanity = GetComponentInChildren<SanitySystem>();
            }

            if (_sanity == null)
            {
                Debug.LogError("[AnchorHorror] GameManager 找不到 SanitySystem，禁用。");
                enabled = false;
                return;
            }

            if (_player == null || _interaction == null)
            {
                Debug.LogWarning("[AnchorHorror] GameManager 未接 PlayerController2D / InteractionSystem，相关功能将静默失效。");
            }

            // 镜头跟随：未在 Inspector 接线时初始化期兜底查一次（CameraRig 是场景常驻对象，只 Awake 用一次）。
            if (_cameraFollow == null)
            {
                _cameraFollow = FindObjectOfType<CameraFollow2D>();
            }

            if (_sequence == null)
            {
                Debug.LogWarning("[AnchorHorror] GameManager 未挂 LevelSequence，两关卡流程无法运行。");
            }

            // 背包实例（ADR-2，普通类，不进 SO）
            _backpack = new Inventory();

            var levelCfg = _levelConfig;
            if (_sequence != null && _sequence.Count > 0)
            {
                var firstLevel = _sequence.GetLevel(0);
                if (firstLevel != null && firstLevel.LevelConfig != null)
                {
                    levelCfg = firstLevel.LevelConfig;
                }
            }
            Anchor = new AnchorSystem(_config, levelCfg, _sanity);
            _sanity.Init(_config);

            if (_interaction != null)
            {
                _interaction.Init(_player != null ? _player.transform : null, _config);
            }

            if (_whisperSource != null && _whisperSource.clip == null)
            {
                _whisperSource.clip = GenerateWhisper();
            }

            _initialized = true;
        }

        private void OnEnable()
        {
            EventBus.AllAnchorsActivated += OnAllAnchorsActivated;
            EventBus.SanityStateChanged += OnSanityStateChanged;
        }

        private void OnDisable()
        {
            EventBus.AllAnchorsActivated -= OnAllAnchorsActivated;
            EventBus.SanityStateChanged -= OnSanityStateChanged;
        }

        private void Start()
        {
            if (!_initialized)
            {
                return;
            }

            // 迭代B：挂了教程图则先展示，任意键后经 BeginAfterTutorial 进关卡1；未挂则直接进（向后兼容）。
            if (_tutorial != null)
            {
                _tutorial.Show(this);
            }
            else
            {
                EnterInitRoom();
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            // 关卡2 倒计时（仅 HorrorLevel，不复用 _initRoomTimer——陷阱 6）
            if (CurrentPhase == GamePhase.HorrorLevel && !_transitioning)
            {
                _remainingTime -= Time.deltaTime;
                if (_remainingTime <= 0f)
                {
                    _remainingTime = 0f;
                    Fail();
                }
            }
        }

        // ──────────────────────────────────────────────────────
        //  关卡1 相关
        // ──────────────────────────────────────────────────────

        /// <summary>
        /// 进入/重开初始房间（关卡1，复用 InitRoom 相位）。
        /// 建关卡1根（entries[0]），生成 8 物品 + 关卡1门（initially locked）（SC-1，ADR-1/5，陷阱 8）。
        /// </summary>
        public void EnterInitRoom()
        {
            if (!_initialized)
            {
                return;
            }

            _transitioning = false;
            SelectionLocked = false;
            _level1Door = null;
            Anchor.Reset();

            // 首屏起始压黑，建完房间后淡入（#1）——避免淡入前先闪一帧空场景/未布置场景。
            SetOverlayAlpha(1f);

            // 背包重置为关卡1容量
            _backpack.Clear();
            _backpack.Capacity = _config.Level1SelectCap;
            EventBus.RaiseBackpackChanged(_backpack);

            _levelIndex = 0;
            _levelData = _sequence != null ? _sequence.GetLevel(0) : null;

            SetInputActive(true);
            _sanity.DecayEnabled = false;
            SetPhase(GamePhase.InitRoom);

            // 建关卡1根（data 驱动，entries[0]）
            if (_levelData != null)
            {
                if (_levelRoot != null)
                {
                    Destroy(_levelRoot);
                    _levelRoot = null;
                }

                _levelRoot = new GameObject("__LevelRoot");
                ApplyBackgroundAndBounds();  // 铺背景 + 设镜头/玩家边界（关卡1=卧室）
                LevelSpawner.Spawn(_levelData, _levelRoot.transform);
                SpawnLevelDoor(); // 建关卡1门（EnterLevel2，initially locked）
            }
            else
            {
                Debug.LogWarning("[AnchorHorror] EnterInitRoom：序列 entries[0] 数据为 null，无法生成关卡1。");
            }

            // 从黑淡入到关卡1首屏（#1）。允许淡入期间移动，纯视觉过渡不锁输入。
            StartCoroutine(Fade(0f));
        }

        /// <summary>教程图结束回调（迭代B，SC-B1）：任意键关掉教程后进入关卡1。</summary>
        public void BeginAfterTutorial()
        {
            EnterInitRoom();
        }

        /// <summary>
        /// 关卡1 拾取物品入背包（cap 5）。
        /// 满 5 件自动调 LockSelection 抽锚点 + 开关卡1门（SC-1/2，陷阱 5）。
        /// </summary>
        public void SelectInLevel1(FeatureTag item)
        {
            if (item == null || SelectionLocked)
            {
                return;
            }

            if (!_backpack.TryAdd(item))
            {
                return; // 满了（CanInteract 层已封，此处兜底）
            }

            item.Consumed = true;
            EventBus.RaiseBackpackChanged(_backpack);

            if (_backpack.Count >= _config.Level1SelectCap)
            {
                LockSelection();
            }
        }

        /// <summary>
        /// 关卡1 选满：抽锚点 + 开启关卡1门（SC-1/2，ADR-4）。
        /// ExtractTargetsFromSelection 从已选物品特征去重抽取，不碰 registry（陷阱 2）。
        /// </summary>
        private void LockSelection()
        {
            if (SelectionLocked)
            {
                return;
            }

            Anchor.ExtractTargetsFromSelection(_backpack.Items);
            SelectionLocked = true;

            // 开启关卡1门可交互（门 CanInteract 判 SelectionLocked，此处无需额外调用——状态已更新）
            // _level1Door 引用保留供将来需要显式刷新表现时用
        }

        // ──────────────────────────────────────────────────────
        //  关卡2 拾取
        // ──────────────────────────────────────────────────────

        /// <summary>
        /// 关卡2 拾取物品入背包（cap 8）。
        /// 成功入包 → Consumed + 隐藏物品 → 检查 Inventory.Satisfies → 满足则通关（SC-5，ADR-6）。
        /// 拾取不改 San（San 仅随时间衰减——陷阱 7）。
        /// </summary>
        public void PickupInLevel2(FeatureTag item)
        {
            if (item == null)
            {
                return;
            }

            // 分析本物品与 5 个目标锚点的关系（入包前算）：
            //   新命中(_pickupHitBuffer) = 命中某个"尚未覆盖"的锚点 → 暖音/浮字奖励（SC-B4）；
            //   matchesAnyTarget = 命中任一目标锚点（哪怕已覆盖）；
            //   都不命中 = 完全无关的"错误物品" → 扣 San + 红闪（用户实测反馈：恢复拾错惩罚）。
            _pickupHitBuffer.Clear();
            bool matchesAnyTarget = false;
            var itemFeatures = item.GetFeatures();
            var targets = Anchor.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                bool itemHasFeature = false;
                for (int f = 0; f < itemFeatures.Count; f++)
                {
                    if (!itemFeatures[f].IsNone && itemFeatures[f] == t.Feature)
                    {
                        itemHasFeature = true;
                        break;
                    }
                }

                if (!itemHasFeature)
                {
                    continue;
                }

                matchesAnyTarget = true;
                if (!_backpack.Covers(t))
                {
                    _pickupHitBuffer.Add(t.Feature); // 新命中（尚未覆盖的锚点）
                }
            }

            if (!_backpack.TryAdd(item))
            {
                return; // 背包满，CanInteract 层已封（错误物品也照样占格）
            }

            item.Consumed = true;
            EventBus.RaiseBackpackChanged(_backpack);

            // 反馈（隐藏物品前触发，浮字/红闪取物品位置）：
            if (_pickupHitBuffer.Count > 0)
            {
                EventBus.RaiseItemMatched(item, _pickupHitBuffer); // 新命中 → 暖音/浮字
            }
            else if (!matchesAnyTarget)
            {
                // 完全无关的错误物品：扣 San + 红闪（双失败线之一由 San 承接）。
                if (_sanity != null)
                {
                    _sanity.Modify(-_config.MismatchLoss);
                }

                EventBus.RaiseItemMismatched(item);
            }

            // 隐藏/销毁场景 GO（物品已入包，场景对象不再需要）
            item.gameObject.SetActive(false);

            // 满足判定：每个锚点有 ≥1 背包物品命中 → 通关；否则背包满(8)仍未满足 → 直接失败（用户要求）。
            if (_backpack.Satisfies(Anchor.Targets))
            {
                EventBus.RaiseAllAnchorsActivated();
            }
            else if (_backpack.Count >= _backpack.Capacity)
            {
                Fail();
            }
        }

        // ──────────────────────────────────────────────────────
        //  关卡1→关卡2 过渡 / 子场景切换
        // ──────────────────────────────────────────────────────

        /// <summary>
        /// 关卡1门触发：Fade → 清背包 → 启 180s 倒计时 → 销毁关卡1根 → 建子场景1
        /// → 放玩家 → DecayEnabled=true → SetPhase(HorrorLevel)（SC-3，ADR-5，陷阱 3/4）。
        /// 各自销毁 _levelRoot，不复用 BeginTransition（陷阱 4）。
        /// </summary>
        public void EnterLevel2()
        {
            if (_transitioning)
            {
                return;
            }

            _transitioning = true;
            StartCoroutine(EnterLevel2Routine());
        }

        private IEnumerator EnterLevel2Routine()
        {
            SetPhase(GamePhase.Transition);
            SetInputActive(false);

            if (_whisperSource != null)
            {
                _whisperSource.Play();
            }
            yield return Fade(1f);

            // 清背包，设关卡2容量，启倒计时（SC-3，陷阱 6）
            _backpack.Clear();
            _backpack.Capacity = _config.Level2BackpackCap;
            EventBus.RaiseBackpackChanged(_backpack);
            _remainingTime = _config.Level2TimeLimit;

            // 销毁关卡1根（各自销毁，不复用 BeginTransition——陷阱 4）
            if (_levelRoot != null)
            {
                Destroy(_levelRoot);
                _levelRoot = null;
            }

            // 建关卡2：从**中间**的子场景进入（用户要求，便于左右两侧都能立即走）。
            // 5 个子场景 entries[1..5] → 取 entries[3]（中间）。subCount/2 整除天然取中。
            int subStart = 1;
            int subCount = _sequence != null ? _sequence.Count - subStart : 0;
            _levelIndex = subCount > 0 ? subStart + subCount / 2 : subStart;
            _levelData = _sequence != null ? _sequence.GetLevel(_levelIndex) : null;

            if (_levelData != null)
            {
                _levelRoot = new GameObject("__LevelRoot");
                ApplyBackgroundAndBounds();  // 铺背景 + 设镜头/玩家边界（关卡2起始=走廊）
                LevelSpawner.Spawn(_levelData, _levelRoot.transform);
                SpawnLevelDoor(); // 子场景门（左右门线性来回）
                MovePlayerToSpawn(_levelData.PlayerSpawn);
            }
            else
            {
                Debug.LogWarning($"[AnchorHorror] EnterLevel2Routine：序列 entries[{_levelIndex}] 数据为 null，无法生成关卡2起始子场景。");
            }

            _sanity.DecayEnabled = true;
            SetInputActive(true);
            SetPhase(GamePhase.HorrorLevel);

            yield return HoldBlack();
            yield return Fade(0f);
            _transitioning = false;
        }

        /// <summary>
        /// 子场景门触发：Fade → 销毁旧根 → 建环状 next 子场景 → 放玩家
        /// → 保留背包/锚点/倒计时/相位（SC-4，ADR-5，陷阱 3/4）。
        /// 绝不调 ExtractTargets（陷阱 3）；不清包、不重置计时（陷阱 6）。
        /// 各自销毁 _levelRoot，不复用 BeginTransition（陷阱 4）。
        /// </summary>
        public void SwitchSubScene(int direction)
        {
            if (_transitioning)
            {
                return;
            }

            _transitioning = true;
            StartCoroutine(SwitchSubSceneRoutine(direction));
        }

        private IEnumerator SwitchSubSceneRoutine(int direction)
        {
            // 先校验子场景存在再动手：避免销毁旧根后无处可去、玩家沉默挂机（W-4）。
            int subStart = 1;
            int last = _sequence != null ? _sequence.Count - 1 : 0;
            if (_sequence == null || last < subStart)
            {
                Debug.LogError(
                    $"[AnchorHorror] SwitchSubScene：序列无关卡2子场景（共 {(_sequence != null ? _sequence.Count : 0)} 条 entry），配置错误，取消切换。");
                _transitioning = false;
                yield break;
            }

            // 线性来回（#3）：clamp 到 [subStart, last]，到边界不动（左右门按边界只生成一侧，通常到不了此分支）。
            int target = Mathf.Clamp(_levelIndex + direction, subStart, last);
            if (target == _levelIndex)
            {
                _transitioning = false;
                yield break; // 已在边界
            }

            SetInputActive(false);

            if (_whisperSource != null)
            {
                _whisperSource.Play();
            }
            yield return Fade(1f);

            // 销毁旧根
            if (_levelRoot != null)
            {
                Destroy(_levelRoot);
                _levelRoot = null;
            }

            _levelIndex = target;
            _levelData = _sequence.GetLevel(_levelIndex);

            if (_levelData != null)
            {
                _levelRoot = new GameObject("__LevelRoot");
                ApplyBackgroundAndBounds();  // 铺背景 + 设镜头/玩家边界（切到的子场景）
                LevelSpawner.Spawn(_levelData, _levelRoot.transform);
                SpawnLevelDoor();
                MovePlayerToSpawn(_levelData.PlayerSpawn);
            }
            else
            {
                Debug.LogWarning($"[AnchorHorror] SwitchSubSceneRoutine：entries[{_levelIndex}] 数据为 null。");
            }

            // 相位不变（保留 HorrorLevel），背包/锚点/倒计时全保留（SC-4，陷阱 3/6）
            SetInputActive(true);

            yield return HoldBlack();
            yield return Fade(0f);
            _transitioning = false;
        }

        // ──────────────────────────────────────────────────────
        //  胜负
        // ──────────────────────────────────────────────────────

        private void OnAllAnchorsActivated()
        {
            EnterVictory();
        }

        private void OnSanityStateChanged(SanityState oldState, SanityState newState)
        {
            if (newState == SanityState.Dead)
            {
                Fail();
            }
        }

        /// <summary>最终胜利（背包满足全部锚点或 AllAnchorsActivated 事件）。</summary>
        private void EnterVictory()
        {
            if (CurrentPhase == GamePhase.Victory || CurrentPhase == GamePhase.Fail)
            {
                return;
            }

            _sanity.DecayEnabled = false;
            SetInputActive(false);
            SetPhase(GamePhase.Victory);
        }

        /// <summary>失败（San 归 0 或倒计时到 0——双失败线，SC-6，陷阱 7）。</summary>
        public void Fail()
        {
            if (CurrentPhase == GamePhase.Fail || CurrentPhase == GamePhase.Victory)
            {
                return;
            }

            _sanity.DecayEnabled = false;
            SetInputActive(false);
            SetPhase(GamePhase.Fail);
        }

        // ──────────────────────────────────────────────────────
        //  辅助方法
        // ──────────────────────────────────────────────────────

        /// <summary>
        /// 在 _levelRoot 下代码建门。
        /// 关卡1（entries[0]）建 EnterLevel2 门；关卡2 子场景建 SwitchSubScene 门。
        /// 末关（序列最后一条 entry）不建门。
        /// </summary>
        private void SpawnLevelDoor()
        {
            if (_levelRoot == null || _sequence == null)
            {
                return;
            }

            var doorSetting = _sequence.GetDoor(_levelIndex);
            var sprite = doorSetting != null ? doorSetting.Sprite : null;

            if (_sequence.GetKind(_levelIndex) == LevelKind.Level1Select)
            {
                // 关卡1门：进入关卡2（右侧）。CanInteract 仅 SelectionLocked 后为 true。
                Vector2 pos = doorSetting != null ? doorSetting.Spawn : new Vector2(4f, -4f);
                string prompt = doorSetting != null && !string.IsNullOrEmpty(doorSetting.Prompt)
                    ? doorSetting.Prompt : "按 E 进入第二关";
                _level1Door = SpawnDoor(DoorKind.EnterLevel2, pos, sprite, prompt);
                return;
            }

            // 关卡2 子场景：左右门线性来回（#3）。非首场景建左门（上一个），非末场景建右门（下一个）。
            int subStart = 1;
            int last = _sequence.Count - 1;
            if (_levelIndex < last)
            {
                SpawnDoor(DoorKind.SwitchSubSceneNext, new Vector2(6f, -3.5f), sprite, "按 E 前往下一场景 →");
            }

            if (_levelIndex > subStart)
            {
                SpawnDoor(DoorKind.SwitchSubScenePrev, new Vector2(-6f, -3.5f), sprite, "← 按 E 返回上一场景");
            }
        }

        /// <summary>在 _levelRoot 下代码建一扇门（碰撞体 + 精灵 + LevelDoor），返回该门。</summary>
        private LevelDoor SpawnDoor(DoorKind kind, Vector2 pos, Sprite sprite, string prompt)
        {
            var doorGo = new GameObject("__LevelDoor");
            doorGo.transform.SetParent(_levelRoot.transform, false);
            doorGo.transform.position = pos;

            var sr = doorGo.AddComponent<SpriteRenderer>();
            var col = doorGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true; // 门靠 OverlapCircle 交互，不物理阻挡玩家

            if (sprite != null)
            {
                sr.sprite = sprite;
                col.size = sprite.bounds.size;
            }
            else
            {
                col.size = new Vector2(1f, 2f);
            }

            var door = doorGo.AddComponent<LevelDoor>();
            door.Configure(kind, sprite, prompt);
            return door;
        }

        /// <summary>
        /// 按当前场景（entries[_levelIndex]）背景图，在 _levelRoot 下铺全屏背景（压最底层，中心对齐原点），
        /// 并把其世界包围盒推给镜头跟随与玩家做边界 clamp（用户需求：场景比窗口大、镜头跟随、边界=背景大小）。
        /// 无背景图时不铺背景，并关闭镜头/玩家边界（自由跟随，防沿用上一场景 clamp）。
        /// 随 _levelRoot 销毁自动清除，切场景重建。
        /// </summary>
        private void ApplyBackgroundAndBounds()
        {
            if (_levelRoot == null)
            {
                return;
            }

            var bg = _sequence != null ? _sequence.GetBackground(_levelIndex) : null;
            if (bg == null)
            {
                if (_cameraFollow != null)
                {
                    _cameraFollow.SetBounds(Vector2.zero, Vector2.zero); // 相等 → 关闭 clamp
                }

                if (_player != null)
                {
                    _player.ClearBounds();
                }

                return;
            }

            var bgGo = new GameObject("__Background");
            bgGo.transform.SetParent(_levelRoot.transform, false);
            bgGo.transform.localPosition = Vector3.zero;
            var sr = bgGo.AddComponent<SpriteRenderer>();
            sr.sprite = bg;
            sr.sortingOrder = -100; // 物品(0)/玩家(10) 之下，铺最底层

            // 缩放到目标世界高度（比窗口大一些）：房间大小由配置控制，跟图片导入 PPU/尺寸解耦。
            float rawHeight = bg.bounds.size.y;
            float scale = rawHeight > 0.0001f ? _config.SceneWorldHeight / rawHeight : 1f;
            bgGo.transform.localScale = new Vector3(scale, scale, 1f);

            // 世界包围盒（sprite pivot 居中 → 以原点为中心；乘缩放）：extents 为半尺寸。
            Vector3 ext = bg.bounds.extents * scale;
            var min = new Vector2(-ext.x, -ext.y);
            var max = new Vector2(ext.x, ext.y);

            if (_cameraFollow != null)
            {
                _cameraFollow.SetBounds(min, max);
            }

            if (_player != null)
            {
                float pad = Mathf.Max(0f, _boundsPadding); // 玩家半身留白，防贴边露出图外
                _player.SetBounds(min + new Vector2(pad, pad), max - new Vector2(pad, pad));
            }
        }

        private void SetPhase(GamePhase phase)
        {
            if (CurrentPhase == phase)
            {
                return;
            }

            var old = CurrentPhase;
            CurrentPhase = phase;
            EventBus.RaisePhaseChanged(old, phase);
        }

        private void SetInputActive(bool active)
        {
            if (_player != null)
            {
                _player.InputEnabled = active;
            }

            if (_interaction != null)
            {
                _interaction.Interactable = active;
            }
        }

        private void MovePlayerToSpawn(Vector2 spawnPosition)
        {
            if (_player == null)
            {
                return;
            }

            _player.transform.position = spawnPosition;
        }

        private IEnumerator Fade(float targetAlpha)
        {
            if (_transitionOverlay == null)
            {
                yield break;
            }

            float duration = _config != null ? _config.FadeDuration : 0.8f;
            var color = _transitionOverlay.color;
            float start = color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // 用 unscaled：胜负/暂停可能改 timeScale，过渡不受影响
                float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                color.a = Mathf.Lerp(start, targetAlpha, Mathf.SmoothStep(0f, 1f, k)); // 缓入缓出，比线性更顺
                _transitionOverlay.color = color;
                yield return null;
            }

            color.a = targetAlpha;
            _transitionOverlay.color = color;
        }

        /// <summary>立即把过渡遮罩设为指定 alpha（首屏起始压黑、避免淡入前先闪一帧场景）。</summary>
        private void SetOverlayAlpha(float alpha)
        {
            if (_transitionOverlay == null)
            {
                return;
            }

            var color = _transitionOverlay.color;
            color.a = alpha;
            _transitionOverlay.color = color;
        }

        /// <summary>切场景全黑停顿（unscaled），让"切画面"读得清；FadeHold&lt;=0 时不停顿。</summary>
        private IEnumerator HoldBlack()
        {
            float hold = _config != null ? _config.FadeHold : 0f;
            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }
        }

        /// <summary>重开本局（重载起始场景，彻底重置一切）。供结算界面调用。先淡出黑屏再加载，消除硬切（#3）。</summary>
        public void RestartGame()
        {
            var scene = string.IsNullOrEmpty(_startSceneName) ? SceneManager.GetActiveScene().name : _startSceneName;
            LoadSceneWithFade(scene);
        }

        /// <summary>返回主菜单。先淡出黑屏再加载，消除硬切（#3）。</summary>
        public void ReturnToMainMenu()
        {
            LoadSceneWithFade(SceneNames.GameMain);
        }

        /// <summary>退出游戏（编辑器停止播放 / 打包 Application.Quit）。供结算界面「退出游戏」按钮调用。</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// 带过渡地切换 Unity 场景（重开 / 返回菜单，#3）。
        ///   有 SceneLoader → 委托它：其 ScreenSpaceOverlay 加载遮罩(sortingOrder 1000)能盖住结算 UI(100)，
        ///     淡入淡出即过渡。GameManager 根是 DontDestroyOnLoad，用 MoveGameObjectToScene 移回当前场景，
        ///     使其随场景卸载被自动销毁——时机正好被不透明遮罩盖住，无闪烁、无僵尸单例、无需魔法等待。
        ///   无 SceneLoader（编辑器直连 Bootstrap 联调）→ 本地世界遮罩淡出后直接加载兜底。
        /// _transitioning 复用为幂等门闩：过渡中重复触发（连按 R/Esc）直接忽略。
        /// </summary>
        private void LoadSceneWithFade(string scene)
        {
            if (_transitioning)
            {
                return;
            }

            _transitioning = true;
            StartCoroutine(LoadSceneWithFadeRoutine(scene));
        }

        private IEnumerator LoadSceneWithFadeRoutine(string scene)
        {
            SetInputActive(false);
            Time.timeScale = 1f; // 记忆面板暂停或结算残留的 timeScale 复位，保证过渡与加载正常推进

            var loader = SceneLoader.Instance;
            if (loader != null && !loader.IsLoading && Application.CanStreamedLevelBeLoaded(scene))
            {
                Instance = null;
                loader.LoadScene(scene); // 遮罩淡入(盖住结算UI+冻结玩法) → 加载 → 淡出到目标场景
                // 脱离 DontDestroyOnLoad：移回当前场景 → 随 Single 加载卸载自动销毁（被不透明遮罩盖住，无闪烁）
                SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
                yield break;
            }

            // 无 SceneLoader：本地淡出黑屏后直接加载；DontDestroyOnLoad 根同样移回当前场景随卸载销毁。
            yield return Fade(1f);
            Instance = null;
            SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
            SceneManager.LoadScene(scene);
        }

        /// <summary>程序化生成 2 秒缓入缓出的低语噪声，避免依赖音频资产。</summary>
        private static AudioClip GenerateWhisper()
        {
            const int rate = 44100;
            const int length = rate * 2;
            var data = new float[length];
            float lp = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float noise = (Mathf.PerlinNoise(i * 0.6f, t * 3f) - 0.5f) * 2f;
                lp = Mathf.Lerp(lp, noise, 0.2f);
                float env = Mathf.Sin(Mathf.PI * (t / 2f));
                data[i] = lp * env * 0.25f;
            }

            var clip = AudioClip.Create("Whisper", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
