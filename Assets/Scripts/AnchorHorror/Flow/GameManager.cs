// ------------------------------------------------------------
// GameManager.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 游戏总控：轻量单例 + 组合根 + 阶段状态机。
    /// 编排 InitRoom → Transition → HorrorLevel → Clear/Fail，并注入各 System 的依赖。
    /// 过渡严格按设计决策 C：先异步加载关卡 → registry 扫描 → 抽锚点 clamp → 放玩家进场。
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("配置")]
        [SerializeField] private GlobalConfig _config;
        [SerializeField] private FeatureDatabase _database;
        [SerializeField] private LevelConfig _levelConfig;

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

            _registry = new LevelFeatureRegistry();
            _sanity.Init(_config);
            Anchor = new AnchorSystem(_config, _levelConfig, _sanity);

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
            _sanity.DecayEnabled = true;
            SetInputActive(true);
            SetPhase(GamePhase.HorrorLevel);

            // 6. 黑屏渐出
            yield return Fade(0f);
            _transitioning = false;
        }

        private void OnAllAnchorsActivated()
        {
            Clear();
        }

        private void OnSanityStateChanged(SanityState oldState, SanityState newState)
        {
            if (newState == SanityState.Dead)
            {
                Fail();
            }
        }

        /// <summary>通关。</summary>
        public void Clear()
        {
            if (CurrentPhase == GamePhase.Clear || CurrentPhase == GamePhase.Fail)
            {
                return;
            }

            _sanity.DecayEnabled = false;
            SetInputActive(false);
            SetPhase(GamePhase.Clear);
        }

        /// <summary>失败（San 归 0）。</summary>
        public void Fail()
        {
            if (CurrentPhase == GamePhase.Fail || CurrentPhase == GamePhase.Clear)
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
            Instance = null; // 让重载后的新实例接管，避免旧单例阻挡
            var scene = string.IsNullOrEmpty(_startSceneName) ? SceneManager.GetActiveScene().name : _startSceneName;
            Destroy(gameObject);
            SceneManager.LoadScene(scene);
        }

        /// <summary>返回主菜单（构建列表第 0 个场景，通常是 GameMain）。</summary>
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            Instance = null;
            Destroy(gameObject);
            SceneManager.LoadScene(0);
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
