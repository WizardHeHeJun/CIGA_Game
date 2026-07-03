// ------------------------------------------------------------
// PlayerJitter2D.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 低 San（Critical/Dead）时玩家精灵微颤（2D 读回"手抖"）。用缩放抖动，不与 Rigidbody2D 的位移冲突。
    /// 只订阅 SanityStateChanged，不被逻辑依赖。
    /// </summary>
    public class PlayerJitter2D : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _amplitude = 0.06f;
        [SerializeField] private float _frequency = 30f;

        private bool _active;
        private Vector3 _baseScale;
        private bool _hasBase;

        private void OnEnable()
        {
            EventBus.SanityStateChanged += OnState;
        }

        private void OnDisable()
        {
            EventBus.SanityStateChanged -= OnState;
            Restore();
        }

        private void Start()
        {
            if (_target == null)
            {
                _target = transform;
            }

            _baseScale = _target.localScale;
            _hasBase = true;
        }

        private void OnState(SanityState oldState, SanityState newState)
        {
            _active = newState == SanityState.Critical || newState == SanityState.Dead;
            if (!_active)
            {
                Restore();
            }
        }

        private void LateUpdate()
        {
            if (!_active || !_hasBase || _target == null)
            {
                return;
            }

            float w = (Mathf.PerlinNoise(Time.unscaledTime * _frequency, 0.37f) - 0.5f) * 2f * _amplitude;
            _target.localScale = _baseScale + new Vector3(w, -w, 0f);
        }

        private void Restore()
        {
            if (_hasBase && _target != null)
            {
                _target.localScale = _baseScale;
            }
        }
    }
}
