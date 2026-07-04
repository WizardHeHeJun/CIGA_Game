// ------------------------------------------------------------
// GameManager.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections;
using Ciga.Startup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 游戏总控：轻量单例 + 组合根 + 阶段状态机。
    /// 编排 InitRoom → Transition → HorrorLevel → SubClear/Victory/Fail，并注入各 System 的依赖。
    /// 过渡严格按设计决策 C：先异步加载关卡 → registry 扫描 → 抽锚点 clamp → 放玩家进场。
    /// 多关循环：_sequence 持关卡序列，_levelIndex 追踪当前关（ADR-1）。
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
        [SerializeField] private string _horrorLevelScene = "HorrorLevel";
        [SerializeField] private SpriteRenderer _transitionOverlay;
        [SerializeField] private AudioSource _whisperSource;

        private LevelFeatureRegistry _registry;
        private float _initRoomTimer;
        private bool _transitioning;
        private bool _initialized; // Awake 完整跑完才为 true；失败者单例/缺依赖时保持 false，阻止 Start/Update
        private string _startSceneName; // 起始（Bootstrap）场景名，供重开局重载
        private GameObject _levelRoot; // data 分支关卡根；_sequence==null 时恒为 null

        // 关卡序列推进（实例字段无 static，重开局场景重载后天然归零——陷阱 6）
        private int _levelIndex;
        // 当前关卡数据（由 _sequence.GetLevel(_levelIndex) 取；_sequence==null 时兜底为 null→原 additive 路径）
        private LevelData _levelData;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;
        public AnchorSystem Anchor { get; private set; }
        public GlobalConfig Config => _config;
        public FeatureDatabase Database => _database;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _startSceneName = gameObject.scene.name; // DontDestroyOnLoad 前记录，否则会变成 "DontDestroyOnLoad"
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
                Debug.LogWarning("[AnchorHorror] GameManager 未挂 LevelSequence，将走 additive 场景路径（legacy）。");
            }

            _registry = new LevelFeatureRegistry();
            _sanity.Init(_config);

            // 从序列取第 0 关数据（_sequence 为 null 时走 additive 路径）
            _levelData = _sequence != null ? _sequence.GetLevel(0) : null;
            _levelIndex = 0;

            // 用当前关的 LevelConfig 建 AnchorSystem（_levelData 非空则取其配置，否则用旧字段兜底）
            var levelCfg = _levelData != null ? _levelData.LevelConfig : _levelConfig;
            Anchor = new AnchorSystem(_config, levelCfg, _sanity);

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
            // 失败者单例（Awake 里 Destroy 自身）或缺依赖被禁用的实例不进入 InitRoom。
            if (_initialized)
            {
                EnterInitRoom();
            }
        }

        private void Update()
        {
            if (!_initialized || CurrentPhase != GamePhase.InitRoom)
            {
                return;
            }

            _initRoomTimer += Time.deltaTime;
            bool byCount = Anchor.CandidateItemCount >= _config.CandidateThreshold;
            bool byTime = _initRoomTimer >= _config.TimeThreshold;
            if (byCount || byTime)
            {
                BeginTransition();
            }
        }

        /// <summary>进入/重开初始房间。</summary>
        public void EnterInitRoom()
        {
            if (!_initialized)
            {
                return;
            }

            _initRoomTimer = 0f;
            _transitioning = false;
            Anchor.Reset();

            SetInputActive(true);
            _sanity.DecayEnabled = false;
            SetPhase(GamePhase.InitRoom);
        }

        /// <summary>手动/自动触发过渡（幂等）。</summary>
        public void BeginTransition()
        {
            if (_transitioning || CurrentPhase != GamePhase.InitRoom)
            {
                return;
            }

            // 幂等清理：防重进过渡时残留关卡根叠加
            if (_levelRoot != null)
            {
                Destroy(_levelRoot);
                _levelRoot = null;
            }

            _transitioning = true;
            StartCoroutine(TransitionRoutine());
        }

        /// <summary>
        /// 门触发换关（ADR-1/3/4，陷阱 1）。
        /// 注意：不复用 BeginTransition——它有 CurrentPhase==InitRoom 守卫，SubClear 态会被拦截。
        /// 此处自行销毁 _levelRoot、推进索引、重建 AnchorSystem，再直接起 TransitionRoutine。
        /// </summary>
        public void AdvanceLevel()
        {
            if (_transitioning)
            {
                return;
            }

            // 推进关卡索引
            _levelIndex++;

            // 取下一关数据
            _levelData = _sequence != null ? _sequence.GetLevel(_levelIndex) : null;

            // 手动销毁旧关卡根（门随之一并清除；绕开 BeginTransition 的 InitRoom 守卫——陷阱 1）
            if (_levelRoot != null)
            {
                Destroy(_levelRoot);
                _levelRoot = null;
            }

            // 重建 AnchorSystem（_levelConfig 是 readonly，重建比加 setter 干净，且天然清跨关状态——ADR-4/陷阱 3/4）
            var levelCfg = _levelData != null ? _levelData.LevelConfig : _levelConfig;
            Anchor = new AnchorSystem(_config, levelCfg, _sanity); // 新实例天然清零，无需再 Reset

            _transitioning = true;
            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            SetPhase(GamePhase.Transition);
            SetInputActive(false);

            // 1. 黑屏渐入 + 低语
            if (_whisperSource != null)
            {
                _whisperSource.Play();
            }
            yield return Fade(1f);

            if (_levelData != null)
            {
                // ——— data 分支：不 LoadSceneAsync，不 SetActiveScene ———
                // 2a. 建关卡根（挂在当前活动场景下）
                _levelRoot = new GameObject("__LevelRoot");

                // 3a. LevelSpawner 生成物品并返回 FeatureTag 列表；registry 只扫关卡根（隔离 InitRoom 候选，防死局）
                var tags = LevelSpawner.Spawn(_levelData, _levelRoot.transform);
                _registry.Scan(tags);

                // 4. 抽锚点（RequiredCount clamp 到场景实际数量，防死局）
                Anchor.ExtractTargets(_registry);

                // 5a. 放玩家进场：取 LevelData 出生点
                MovePlayerToSpawn(_levelData.PlayerSpawn);
            }
            else
            {
                // ——— 原 additive 路径逐字节不变 ———
                // 2. 异步加载关卡场景（Additive）；玩家仍冻结
                var op = SceneManager.LoadSceneAsync(_horrorLevelScene, LoadSceneMode.Additive);
                if (op != null)
                {
                    while (!op.isDone)
                    {
                        yield return null;
                    }
                }
                else
                {
                    Debug.LogWarning($"[AnchorHorror] 关卡场景 '{_horrorLevelScene}' 未加载（未加入 Build Settings？），沿用当前场景扫描。");
                }

                var levelScene = SceneManager.GetSceneByName(_horrorLevelScene);
                if (levelScene.IsValid() && levelScene.isLoaded)
                {
                    SceneManager.SetActiveScene(levelScene);
                }

                // 3. registry 扫描关卡特征数量
                if (levelScene.IsValid() && levelScene.isLoaded)
                {
                    _registry.Scan(levelScene);
                }
                else
                {
                    _registry.Scan(SceneManager.GetActiveScene());
                }

                // 4. 抽锚点（RequiredCount clamp 到场景实际数量，防死局）
                Anchor.ExtractTargets(_registry);

                // 5. 放玩家进场：定位出生点、开启衰减与交互
                MovePlayerToSpawn(levelScene);
            }

            _sanity.DecayEnabled = true;
            SetInputActive(true);
            SetPhase(GamePhase.HorrorLevel);

            // 6. 黑屏渐出
            yield return Fade(0f);
            _transitioning = false;
        }

        private void OnAllAnchorsActivated()
        {
            // legacy additive 路径（未挂序列）不走多关推进——否则会进 SubClear 却无门可推进、
            // R/Esc 又被结算配置锁死而永久卡死（code-review WARN）。
            if (_sequence == null)
            {
                return;
            }

            // 末关：胜利；否则：子关通关生成门（ADR-4）
            if (_levelIndex + 1 >= _sequence.Count)
            {
                EnterVictory();
            }
            else
            {
                EnterSubClear();
            }
        }

        private void OnSanityStateChanged(SanityState oldState, SanityState newState)
        {
            if (newState == SanityState.Dead)
            {
                Fail();
            }
        }

        /// <summary>
        /// 子关通关：进入 SubClear 相位，在 _levelRoot 下代码建门（ADR-3）。
        /// 门随 _levelRoot 销毁一并清除（换关天然清理），物理上杜绝 HorrorLevel 阶段被误触（陷阱 2）。
        /// </summary>
        private void EnterSubClear()
        {
            if (CurrentPhase == GamePhase.SubClear || CurrentPhase == GamePhase.Victory || CurrentPhase == GamePhase.Fail)
            {
                return;
            }

            _sanity.DecayEnabled = false;
            SetInputActive(true); // 保持输入启用，玩家需要走向门
            SetPhase(GamePhase.SubClear);

            // 在关卡根下代码建门（ADR-3，与 ItemFactory 风格一致，无 prefab 依赖）
            if (_levelRoot == null || _sequence == null)
            {
                return;
            }

            var doorSetting = _sequence.GetDoor(_levelIndex);
            if (doorSetting == null)
            {
                return;
            }

            var doorGo = new GameObject("__LevelDoor");
            doorGo.transform.SetParent(_levelRoot.transform, false);
            doorGo.transform.position = doorSetting.Spawn;

            var sr = doorGo.AddComponent<SpriteRenderer>();
            var col = doorGo.AddComponent<BoxCollider2D>();

            // 根据 Sprite 尺寸自动设置碰撞体大小（无 Sprite 时给默认尺寸）
            if (doorSetting.Sprite != null)
            {
                sr.sprite = doorSetting.Sprite;
                var bounds = doorSetting.Sprite.bounds;
                col.size = bounds.size;
            }
            else
            {
                col.size = new Vector2(1f, 2f);
            }

            var door = doorGo.AddComponent<LevelDoor>();
            door.Configure(doorSetting.Sprite, doorSetting.Prompt);
        }

        /// <summary>最终胜利（末关锚点全激活）。</summary>
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

        /// <summary>失败（San 归 0）。</summary>
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

        /// <summary>data 分支：直接使用 LevelData 中序列化的出生点坐标。</summary>
        private void MovePlayerToSpawn(Vector2 spawnPosition)
        {
            if (_player == null)
            {
                return;
            }

            _player.transform.position = spawnPosition;
        }

        /// <summary>additive 路径：在关卡场景根下查找名为 PlayerSpawn 的对象。</summary>
        private void MovePlayerToSpawn(Scene levelScene)
        {
            if (_player == null || !levelScene.IsValid() || !levelScene.isLoaded)
            {
                return;
            }

            var roots = levelScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var spawn = roots[i].transform.Find("PlayerSpawn");
                if (spawn == null && roots[i].name == "PlayerSpawn")
                {
                    spawn = roots[i].transform;
                }

                if (spawn != null)
                {
                    _player.transform.position = spawn.position;
                    return;
                }
            }
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
            // 不调 EventBus.ClearAll()：Destroy(gameObject) 会触发各订阅者 OnDisable 正常反订阅，
            // 新场景实例在 OnEnable 重新订阅，流程自洽（ClearAll 仅供测试彻底重置）。
            // _levelIndex 为实例字段，Destroy(gameObject) + 场景重载后新 Awake 从 0 开始——陷阱 6 自动满足。
            Instance = null; // 让重载后的新实例接管，避免旧单例阻挡
            var scene = string.IsNullOrEmpty(_startSceneName) ? SceneManager.GetActiveScene().name : _startSceneName;
            Destroy(gameObject);
            SceneManager.LoadScene(scene);
        }

        /// <summary>返回主菜单（按场景名加载，Build Settings 顺序变动不影响逻辑）。</summary>
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
                lp = Mathf.Lerp(lp, noise, 0.2f);              // 低通，柔化成"气声"
                float env = Mathf.Sin(Mathf.PI * (t / 2f));    // 0→1→0 的缓入缓出
                data[i] = lp * env * 0.25f;
            }

            var clip = AudioClip.Create("Whisper", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
