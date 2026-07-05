// ------------------------------------------------------------
// SceneLoader.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections;
using Ciga.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ciga.Startup
{
    /// <summary>
    /// 跨场景加载编排器，DontDestroyOnLoad 单例。
    /// 唯一加载入口：LoadScene(target)。
    /// 内部持有 LoadingOverlay Canvas（sortingOrder=1000）；首次 LoadScene 时懒创建。
    /// 假进度：displayed = MoveTowards(displayed, target01, unscaledDeltaTime/minDuration)。
    /// allowSceneActivation=false 保证 displayed>=1 后才切换，防止视觉卡顿。
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        private static SceneLoader _instance;

        /// <summary>全局唯一 SceneLoader 单例（DontDestroyOnLoad）。</summary>
        public static SceneLoader Instance => _instance;

        [Header("加载配置")]
        [SerializeField] private LoadingConfig _loadingConfig;

        [Header("音频")]
        [SerializeField] private AudioClip _bgmClip;
        [SerializeField] private AudioClip _uiClickClip;
        [Range(0f, 1f)]
        [SerializeField] private float _bgmVolume = 0.35f;

        private LoadingOverlay _overlay;
        private bool _isLoading;
        private Coroutine _loadCoroutine;
        private AudioSource _bgmSource;

        /// <summary>当前是否正在加载场景（幂等保护：加载中调 LoadScene 直接忽略）。</summary>
        public bool IsLoading => _isLoading;
        public AudioClip UiClickClip => _uiClickClip;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // 多余实例（如 GameMain 场景也放了 SceneLoader）自毁；其 LoadingConfig 不会生效，
                // 加载配置请在启动场景(Login)的 SceneLoader 上填。
                Debug.LogWarning("[SceneLoader] 已存在实例，销毁多余实例。LoadingConfig 请在启动场景的 SceneLoader 上配置。", this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureOverlay();
            EnsureBgmSource();
            PlayBgm();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 唯一场景加载入口。加载中调用直接忽略（幂等）。
        /// 目标场景不在 Build Settings 时打 Warning 并不切换。
        /// </summary>
        /// <param name="target">目标场景名称（使用 SceneNames 常量）</param>
        public void LoadScene(string target)
        {
            if (_isLoading)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogWarning($"[SceneLoader] 场景 '{target}' 不在 Build Settings 中，取消加载。请检查 Build Settings 配置。", this);
                return;
            }

            _loadCoroutine = StartCoroutine(LoadSceneRoutine(target));
        }

        private IEnumerator LoadSceneRoutine(string target)
        {
            _isLoading = true;

            EnsureOverlay();
            _overlay.SetProgress(0f);

            float fadeDuration = _loadingConfig != null ? _loadingConfig.FadeDuration : 0.3f;
            yield return _overlay.FadeIn(fadeDuration); // 遮罩淡入盖住当前场景（#2）

            float minDuration = _loadingConfig != null ? _loadingConfig.MinDuration : 1.5f;
            float elapsed = 0f;
            float displayed = 0f;

            var op = SceneManager.LoadSceneAsync(target);
            if (op == null)
            {
                Debug.LogWarning($"[SceneLoader] LoadSceneAsync('{target}') 返回 null，取消加载。", this);
                _overlay.SetVisible(false);
                _isLoading = false;
                yield break;
            }

            op.allowSceneActivation = false;

            while (!op.isDone)
            {
                elapsed += Time.unscaledDeltaTime;

                // target01：取 Unity 进度（卡 0.9 归一）与已耗时比例的较小值
                float progressRatio = Mathf.Clamp01(op.progress / 0.9f);
                float timeRatio = minDuration > 0f ? elapsed / minDuration : 1f;
                float target01 = Mathf.Min(progressRatio, timeRatio);

                // 假进度平滑插值
                float step = minDuration > 0f ? Time.unscaledDeltaTime / minDuration : 1f;
                displayed = Mathf.MoveTowards(displayed, target01, step);
                _overlay.SetProgress(displayed);

                // 两个条件同时满足才允许切换：视觉已满 & Unity 已就绪
                if (displayed >= 1f && op.progress >= 0.9f)
                {
                    op.allowSceneActivation = true;
                }

                yield return null;
            }

            yield return _overlay.FadeOut(fadeDuration); // 加载完成后遮罩淡出，露出新场景（#2）
            _isLoading = false;
            _loadCoroutine = null;
        }

        private void EnsureOverlay()
        {
            if (_overlay != null)
            {
                return;
            }

            var canvasGo = new GameObject("SceneLoader_OverlayCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster 不需要：Overlay 仅遮挡，无按钮交互
            var overlayGo = new GameObject("LoadingOverlay");
            overlayGo.transform.SetParent(canvasGo.transform, false);
            _overlay = overlayGo.AddComponent<LoadingOverlay>();
            _overlay.Setup(_loadingConfig);
            _overlay.SetVisible(false);
        }

        private void EnsureBgmSource()
        {
            if (_bgmSource != null)
            {
                _bgmSource.playOnAwake = false;
                _bgmSource.loop = true;
                _bgmSource.spatialBlend = 0f;
                _bgmSource.volume = _bgmVolume;
                return;
            }

            _bgmSource = GetComponent<AudioSource>();
            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
            }

            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.volume = _bgmVolume;
        }

        private void PlayBgm()
        {
            EnsureBgmSource();
            if (_bgmSource == null)
            {
                return;
            }

            _bgmSource.volume = _bgmVolume;
            if (_bgmClip == null)
            {
                if (_bgmSource.isPlaying)
                {
                    _bgmSource.Stop();
                }

                _bgmSource.clip = null;
                return;
            }

            if (_bgmSource.clip != _bgmClip)
            {
                _bgmSource.clip = _bgmClip;
            }

            if (!_bgmSource.isPlaying)
            {
                _bgmSource.Play();
            }
        }
    }
}
