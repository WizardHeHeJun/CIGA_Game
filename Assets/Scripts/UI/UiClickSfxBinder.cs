// ------------------------------------------------------------
// UiClickSfxBinder.cs
// Author : Claude
// Created: 2026-07-05
// ------------------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.UI
{
    /// <summary>
    /// 轻量 UI 点击音效绑定器：仅绑定 Unity UI Button.onClick，播放统一点击音效。
    /// 不处理键盘输入，不处理世界交互；OnEnable/OnDisable 成对注册，避免重复订阅。
    /// </summary>
    [DisallowMultipleComponent]
    public class UiClickSfxBinder : MonoBehaviour
    {
        [SerializeField] private Button[] _buttons;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _clickClip;
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.8f;
        [SerializeField] private bool _includeInactive = true;

        private bool _bound;

        public AudioClip ClickClip
        {
            get => _clickClip;
            set => _clickClip = value;
        }

        private void Awake()
        {
            EnsureAudioSource();
            EnsureButtons();
        }

        private void OnEnable()
        {
            EnsureAudioSource();
            EnsureButtons();
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void SetClickClip(AudioClip clickClip)
        {
            _clickClip = clickClip;
        }

        public void RefreshButtons()
        {
            Unbind();
            EnsureButtons();
            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        private void EnsureAudioSource()
        {
            if (_audioSource != null)
            {
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
                return;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
        }

        private void EnsureButtons()
        {
            if (_buttons != null && _buttons.Length > 0)
            {
                return;
            }

            _buttons = GetComponentsInChildren<Button>(_includeInactive);
        }

        private void Bind()
        {
            if (_bound || _buttons == null)
            {
                return;
            }

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null)
                {
                    _buttons[i].onClick.AddListener(PlayClick);
                }
            }

            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _buttons == null)
            {
                return;
            }

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null)
                {
                    _buttons[i].onClick.RemoveListener(PlayClick);
                }
            }

            _bound = false;
        }

        private void PlayClick()
        {
            if (_audioSource == null || _clickClip == null)
            {
                return;
            }

            _audioSource.PlayOneShot(_clickClip, _volume);
        }
    }
}
