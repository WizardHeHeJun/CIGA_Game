// ------------------------------------------------------------
// SanityFeedback.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// San 分级 2D 表现：全屏黑 SpriteRenderer 压暗 + Edge/Distorted 闪烁 + Distorted/Critical 循环心跳音
    /// （缺省自动程序化生成）+ Critical 降玩家移速。只订阅 EventBus，不被逻辑依赖，随时可裁剪。
    /// </summary>
    public class SanityFeedback : MonoBehaviour
    {
        [Header("压暗（全屏黑 SpriteRenderer，可空）")]
        [SerializeField] private SpriteRenderer _darkOverlay;
        [SerializeField] private float _edgeAlpha = 0.25f;
        [SerializeField] private float _distortedAlpha = 0.5f;
        [SerializeField] private float _criticalAlpha = 0.75f;
        [SerializeField] private float _flickerAmount = 0.08f;
        [SerializeField] private float _flickerSpeed = 12f;

        [Header("音频（AudioSource 可空；无 clip 时自动生成程序化心跳）")]
        [SerializeField] private AudioSource _heartbeat;

        [Header("尖锐噪音（Distorted/Critical 随机触发，可空）")]
        [SerializeField] private AudioSource _noiseSource;
        [SerializeField] private float _noiseIntervalMin = 1.2f;
        [SerializeField] private float _noiseIntervalMax = 3.5f;

        [Header("移速惩罚")]
        [SerializeField] private PlayerController2D _player;
        [SerializeField] private GlobalConfig _config;

        private SanityState _state = SanityState.Normal;
        private float _baseAlpha;
        private AudioClip _noiseClip;
        private float _noiseTimer;

        private void Awake()
        {
            if (_heartbeat != null && _heartbeat.clip == null)
            {
                _heartbeat.clip = GenerateHeartbeat();
                _heartbeat.loop = true;
            }

            _noiseClip = GenerateNoise();
        }

        private void OnEnable()
        {
            EventBus.SanityStateChanged += OnSanityStateChanged;
        }

        private void OnDisable()
        {
            EventBus.SanityStateChanged -= OnSanityStateChanged;
        }

        private void OnSanityStateChanged(SanityState oldState, SanityState newState)
        {
            _state = newState;
            _baseAlpha = BaseAlpha(newState);
            SetOverlayAlpha(_baseAlpha); // 切档立即应用基础压暗，非闪烁档无需每帧写
            ApplyAudio(newState);
            ApplyMovePenalty(newState);
        }

        private void Update()
        {
            UpdateFlicker();
            UpdateNoise();
        }

        private void UpdateFlicker()
        {
            // 仅 Edge/Distorted 需要每帧闪烁；其余档位的 alpha 已在切档时写好，避免每帧无效写入。
            if (_darkOverlay == null || (_state != SanityState.Edge && _state != SanityState.Distorted))
            {
                return;
            }

            float flicker = (Mathf.PerlinNoise(Time.unscaledTime * _flickerSpeed, 0f) - 0.5f) * 2f * _flickerAmount;
            SetOverlayAlpha(_baseAlpha + flicker);
        }

        private void UpdateNoise()
        {
            if (_noiseSource == null || _noiseClip == null
                || (_state != SanityState.Distorted && _state != SanityState.Critical))
            {
                return;
            }

            _noiseTimer -= Time.deltaTime;
            if (_noiseTimer <= 0f)
            {
                _noiseSource.PlayOneShot(_noiseClip, Random.Range(0.15f, 0.4f));
                _noiseTimer = Random.Range(_noiseIntervalMin, _noiseIntervalMax);
            }
        }

        private void SetOverlayAlpha(float alpha)
        {
            if (_darkOverlay == null)
            {
                return;
            }

            var c = _darkOverlay.color;
            c.a = Mathf.Clamp01(alpha);
            _darkOverlay.color = c;
        }

        private float BaseAlpha(SanityState state)
        {
            switch (state)
            {
                case SanityState.Edge: return _edgeAlpha;
                case SanityState.Distorted: return _distortedAlpha;
                case SanityState.Critical: return _criticalAlpha;
                case SanityState.Dead: return 1f;
                default: return 0f;
            }
        }

        private void ApplyAudio(SanityState state)
        {
            if (_heartbeat == null)
            {
                return;
            }

            bool shouldPlay = state == SanityState.Distorted || state == SanityState.Critical;
            if (shouldPlay && !_heartbeat.isPlaying)
            {
                _heartbeat.loop = true;
                _heartbeat.Play();
            }
            else if (!shouldPlay && _heartbeat.isPlaying)
            {
                _heartbeat.Stop();
            }
        }

        private void ApplyMovePenalty(SanityState state)
        {
            if (_player == null)
            {
                return;
            }

            float penalty = _config != null ? _config.MoveSpeedPenalty : 0.2f;
            float multiplier = state == SanityState.Critical ? 1f - penalty : 1f;
            _player.SetSpeedMultiplier(multiplier);
        }

        /// <summary>程序化生成一个 1 秒循环的低频"扑通-扑通"心跳，避免依赖音频资产。</summary>
        private static AudioClip GenerateHeartbeat()
        {
            const int rate = 44100;
            const int length = rate; // 1s
            var data = new float[length];
            AddThump(data, rate, 0.00f, 90f, 0.12f);
            AddThump(data, rate, 0.18f, 70f, 0.10f);

            var clip = AudioClip.Create("Heartbeat", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void AddThump(float[] data, int rate, float start, float freq, float duration)
        {
            int s0 = (int)(start * rate);
            int n = (int)(duration * rate);
            for (int i = 0; i < n && s0 + i < data.Length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 18f);
                data[s0 + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.6f;
            }
        }

        /// <summary>程序化生成一段短促的白噪爆发（快速衰减），作尖锐噪音。</summary>
        private static AudioClip GenerateNoise()
        {
            const int rate = 44100;
            int length = (int)(rate * 0.15f);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 22f);
                data[i] = (Random.value * 2f - 1f) * env * 0.8f;
            }

            var clip = AudioClip.Create("Noise", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
