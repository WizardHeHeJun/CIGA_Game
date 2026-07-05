// ------------------------------------------------------------
// UITextBreathe.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace Ciga.UI
{
    /// <summary>
    /// TMP 文本的「淡入 + 空闲呼吸」提示效果：Play() 后先在 _fadeInDuration 内从透明淡入，
    /// 之后按 _breathPeriod 在 [_minAlpha, _maxAlpha] 间正弦起伏（呼吸），峰值默认 0.3（30% 不透明）。
    /// 只改文本 alpha（TMP_Text.alpha），不动颜色；用 unscaledTime，暂停/慢放不受影响。
    /// 运行时挂到承载 TMP_Text 的节点即可（未指定 _target 时自动取同节点的 TMP_Text）。
    /// </summary>
    [DisallowMultipleComponent]
    public class UITextBreathe : MonoBehaviour
    {
        [SerializeField] private TMP_Text _target;              // 留空则取同节点 TMP_Text
        [SerializeField] private float _fadeInDuration = 0.7f;  // 淡入时长（秒）
        [SerializeField] private float _breathPeriod = 2f;      // 呼吸一个周期的时长（秒）
        [SerializeField, Range(0f, 1f)] private float _minAlpha = 0.1f;  // 呼吸最暗
        [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.3f;  // 呼吸最亮（30% 透明）

        private bool _running;
        private float _startTime;

        private void Awake()
        {
            if (_target == null)
            {
                _target = GetComponent<TMP_Text>();
            }
        }

        /// <summary>从透明开始淡入，随后进入呼吸。重复调用会重置节奏（重新淡入）。</summary>
        public void Play()
        {
            if (_target == null)
            {
                _target = GetComponent<TMP_Text>();
            }

            _running = true;
            _startTime = Time.unscaledTime;
            Apply(0f);
        }

        /// <summary>停止并置为全透明（隐藏）。</summary>
        public void Stop()
        {
            _running = false;
            Apply(0f);
        }

        private void Update()
        {
            if (!_running || _target == null)
            {
                return;
            }

            float elapsed = Time.unscaledTime - _startTime;
            float fade = _fadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / _fadeInDuration);

            // 相位偏 -π/2：淡入结束时正好落在 _minAlpha，再涨到 _maxAlpha，避免一进来就是最亮
            float phase = _breathPeriod <= 0f
                ? Mathf.PI * 0.5f
                : elapsed / _breathPeriod * Mathf.PI * 2f - Mathf.PI * 0.5f;
            float breathe = Mathf.Lerp(_minAlpha, _maxAlpha, 0.5f + 0.5f * Mathf.Sin(phase));

            Apply(fade * breathe);
        }

        private void Apply(float alpha)
        {
            if (_target == null)
            {
                _target = GetComponent<TMP_Text>();
            }

            if (_target != null)
            {
                _target.alpha = alpha;
            }
        }
    }
}
