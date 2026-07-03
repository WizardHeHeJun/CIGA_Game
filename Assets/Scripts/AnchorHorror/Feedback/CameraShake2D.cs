// ------------------------------------------------------------
// CameraShake2D.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// San 越低相机抖得越猛（Perlin 偏移，LateUpdate 应用）。只订阅 SanityStateChanged，不被逻辑依赖。
    /// </summary>
    public class CameraShake2D : MonoBehaviour
    {
        [SerializeField] private Transform _camera;
        [SerializeField] private float _edgeAmp = 0.03f;
        [SerializeField] private float _distortedAmp = 0.08f;
        [SerializeField] private float _criticalAmp = 0.18f;
        [SerializeField] private float _frequency = 14f;

        private float _amp;
        private Vector3 _base;
        private bool _hasBase;

        private void OnEnable()
        {
            EventBus.SanityStateChanged += OnState;
        }

        private void OnDisable()
        {
            EventBus.SanityStateChanged -= OnState;
            RestoreBase();
        }

        private void Start()
        {
            if (_camera == null && Camera.main != null)
            {
                _camera = Camera.main.transform;
            }

            if (_camera != null)
            {
                _base = _camera.localPosition;
                _hasBase = true;
            }
        }

        private void OnState(SanityState oldState, SanityState newState)
        {
            switch (newState)
            {
                case SanityState.Edge: _amp = _edgeAmp; break;
                case SanityState.Distorted: _amp = _distortedAmp; break;
                case SanityState.Critical: _amp = _criticalAmp; break;
                case SanityState.Dead: _amp = _criticalAmp; break;
                default: _amp = 0f; break;
            }
        }

        private void LateUpdate()
        {
            if (_camera == null || !_hasBase)
            {
                return;
            }

            if (_amp <= 0f)
            {
                _camera.localPosition = _base;
                return;
            }

            float t = Time.unscaledTime * _frequency;
            float x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * _amp;
            float y = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * _amp;
            _camera.localPosition = _base + new Vector3(x, y, 0f);
        }

        private void RestoreBase()
        {
            if (_camera != null && _hasBase)
            {
                _camera.localPosition = _base;
            }
        }
    }
}
