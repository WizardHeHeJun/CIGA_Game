// ------------------------------------------------------------
// LoginPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using Ciga.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.Startup
{
    /// <summary>
    /// Login 场景面板：渲染背景/Logo/文案，「进入游戏」按钮触发场景加载。
    /// Config 字段为空时调 PlaceholderWarnOnce 占位，不崩溃。
    /// IsLoading 时禁用按钮防重复点击；OnEnable/OnDisable 成对管理监听。
    /// </summary>
    public class LoginPanel : UIPanel
    {
        [Header("配置（Inspector 拖入）")]
        [SerializeField] private LoginPanelConfig _config;

        [Header("UI 引用（生成器接线）")]
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _logoImage;
        [SerializeField] private Button _enterButton;
        [SerializeField] private TMP_Text _enterButtonLabel;
        [SerializeField] private TMP_Text _subtitleLabel;

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (_enterButton != null)
            {
                _enterButton.onClick.AddListener(OnEnterClicked);
            }
        }

        private void OnDisable()
        {
            if (_enterButton != null)
            {
                _enterButton.onClick.RemoveListener(OnEnterClicked);
            }
        }

        protected override void ApplyConfig()
        {
            if (_config == null)
            {
                PlaceholderWarnOnce("_config");
                return;
            }

            if (_backgroundImage != null)
            {
                if (_config.Background != null)
                {
                    _backgroundImage.sprite = _config.Background;
                    _backgroundImage.color = Color.white;
                }
                else
                {
                    PlaceholderWarnOnce("Background");
                }
            }

            if (_logoImage != null)
            {
                if (_config.Logo != null)
                {
                    _logoImage.sprite = _config.Logo;
                    _logoImage.color = Color.white;
                    _logoImage.gameObject.SetActive(true);
                }
                else
                {
                    _logoImage.gameObject.SetActive(false);
                }
            }

            if (_enterButton != null && _config.EnterButtonSprite != null)
            {
                var targetGraphic = _enterButton.targetGraphic as Image;
                if (targetGraphic != null)
                {
                    targetGraphic.sprite = _config.EnterButtonSprite;
                }
            }

            if (_enterButtonLabel != null)
            {
                _enterButtonLabel.text = !string.IsNullOrEmpty(_config.EnterButtonText)
                    ? _config.EnterButtonText
                    : "进入游戏";
            }
            else
            {
                PlaceholderWarnOnce("_enterButtonLabel");
            }

            if (_subtitleLabel != null)
            {
                bool hasSubtitle = !string.IsNullOrEmpty(_config.Subtitle);
                _subtitleLabel.text = hasSubtitle ? _config.Subtitle : string.Empty;
                _subtitleLabel.gameObject.SetActive(hasSubtitle);
            }
        }

        private void OnEnterClicked()
        {
            var loader = SceneLoader.Instance;
            if (loader == null || loader.IsLoading)
            {
                return;
            }

            // 禁用按钮防重复点击
            if (_enterButton != null)
            {
                _enterButton.interactable = false;
            }

            loader.LoadScene(SceneNames.GameMain);
        }
    }
}
