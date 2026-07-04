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

            // 背包重置为关卡1容量
            _backpack.Clear();
            _backpack.Capacity = _config.Level1SelectCap;

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
                LevelSpawner.Spawn(_levelData, _levelRoot.transform);
                SpawnLevelDoor(); // 建关卡1门（EnterLevel2，initially locked）
            }
            else
            {
                Debug.LogWarning("[AnchorHorror] EnterInitRoom：序列 entries[0] 数据为 null，无法生成关卡1。");
            }
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

            // 本物品能"新命中"哪些此前未覆盖的锚点（供拾取反馈，SC-B4）。入包前算，用未覆盖态判定。
            _pickupHitBuffer.Clear();
            var itemFeatures = item.GetFeatures();
            var targets = Anchor.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (_backpack.Covers(t))
                {
                    continue; // 已覆盖，拾同特征不再奖励反馈（陷阱：仅新命中触发）
                }

                for (int f = 0; f < itemFeatures.Count; f++)
                {
                    if (!itemFeatures[f].IsNone && itemFeatures[f] == t.Feature)
                    {
                        _pickupHitBuffer.Add(t.Feature);
                        break;
                    }
                }
            }

            if (!_backpack.TryAdd(item))
            {
                return; // 背包满，CanInteract 层已封
            }

            item.Consumed = true;

            // 拾取反馈：有新命中 → 暖音/浮字（复用 MatchFeedback，SC-B4）。隐藏物品前触发（浮字取物品位置）。
            if (_pickupHitBuffer.Count > 0)
            {
                EventBus.RaiseItemMatched(item, _pickupHitBuffer);
            }

            // 隐藏/销毁场景 GO（物品已入包，场景对象不再需要）
            item.gameObject.SetActive(false);

            // 满足判定：每个锚点有 ≥1 背包物品命中
            if (_backpack.Satisfies(Anchor.Targets))
            {
                EventBus.RaiseAllAnchorsActivated();
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
            _remainingTime = _config.Level2TimeLimit;

            // 销毁关卡1根（各自销毁，不复用 BeginTransition——陷阱 4）
            if (_levelRoot != null)
            {
                Destroy(_levelRoot);
                _levelRoot = null;
            }

            // 建关卡2 子场景1（entries[1]）
            _levelIndex = 1;
            _levelData = _sequence != null ? _sequence.GetLevel(_levelIndex) : null;

            if (_levelData != null)
            {
                _levelRoot = new GameObject("__LevelRoot");
                LevelSpawner.Spawn(_levelData, _levelRoot.transform);
                SpawnLevelDoor(); // 子场景门（SwitchSubScene）
                MovePlayerToSpawn(_levelData.PlayerSpawn);
            }
            else
            {
                Debug.LogWarning("[AnchorHorror] EnterLevel2Routine：序列 entries[1] 数据为 null，无法生成关卡2子场景1。");
            }

            _sanity.DecayEnabled = true;
            SetInputActive(true);
            SetPhase(GamePhase.HorrorLevel);

            yield return Fade(0f);
            _transitioning = false;
        }

        /// <summary>
        /// 子场景门触发：Fade → 销毁旧根 → 建环状 next 子场景 → 放玩家
        /// → 保留背包/锚点/倒计时/相位（SC-4，ADR-5，陷阱 3/4）。
        /// 绝不调 ExtractTargets（陷阱 3）；不清包、不重置计时（陷阱 6）。
        /// 各自销毁 _levelRoot，不复用 BeginTransition（陷阱 4）。
        /// </summary>
        public void SwitchSubScene()
        {
            if (_transitioning)
            {
                return;
            }

            _transitioning = true;
            StartCoroutine(SwitchSubSceneRoutine());
        }

        private IEnumerator SwitchSubSceneRoutine()
        {
            // 先校验子场景存在再动手：避免销毁旧根后无处可去、玩家沉默挂机（W-4）。
            int subStart = 1;
            int subCount = _sequence != null ? _sequence.Count - subStart : 0;
            if (subCount <= 0)
            {
                Debug.LogError(
                    $"[AnchorHorror] SwitchSubScene：序列无关卡2子场景（共 {(_sequence != null ? _sequence.Count : 0)} 条 entry），配置错误，取消切换。");
                _transitioning = false;
                yield break;
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

            // 环状 next：在 entries[1..Count-1] 内循环（SC-4）
            int subIndex = (_levelIndex - subStart + 1) % subCount;
            _levelIndex = subStart + subIndex;

            _levelData = _sequence.GetLevel(_levelIndex);

            if (_levelData != null)
            {
                _levelRoot = new GameObject("__LevelRoot");
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
            if (doorSetting == null)
            {
                return;
            }

            var doorKind = _sequence.GetDoorKind(_levelIndex);

            var doorGo = new GameObject("__LevelDoor");
            doorGo.transform.SetParent(_levelRoot.transform, false);
            doorGo.transform.position = doorSetting.Spawn;

            var sr = doorGo.AddComponent<SpriteRenderer>();
            var col = doorGo.AddComponent<BoxCollider2D>();

            if (doorSetting.Sprite != null)
            {
                sr.sprite = doorSetting.Sprite;
                col.size = doorSetting.Sprite.bounds.size;
            }
            else
            {
                col.size = new Vector2(1f, 2f);
            }

            var door = doorGo.AddComponent<LevelDoor>();
            door.Configure(doorKind, doorSetting.Sprite, doorSetting.Prompt);

            // 关卡1门引用（LockSelection 后门的 CanInteract 自动通过 SelectionLocked 判定）
            if (doorKind == DoorKind.EnterLevel2)
            {
                _level1Door = door;
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
                t += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(start, targetAlpha, duration > 0f ? t / duration : 1f);
                _transitionOverlay.color = color;
                yield return null;
            }

            color.a = targetAlpha;
            _transitionOverlay.color = color;
        }

        /// <summary>重开本局（重载起始场景，彻底重置一切）。供结算界面调用。</summary>
        public void RestartGame()
        {
            Time.timeScale = 1f;
            Instance = null;
            var scene = string.IsNullOrEmpty(_startSceneName) ? SceneManager.GetActiveScene().name : _startSceneName;
            Destroy(gameObject);
            SceneManager.LoadScene(scene);
        }

        /// <summary>返回主菜单。</summary>
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            Instance = null;
            Destroy(gameObject);
            SceneManager.LoadScene(SceneNames.GameMain);
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
