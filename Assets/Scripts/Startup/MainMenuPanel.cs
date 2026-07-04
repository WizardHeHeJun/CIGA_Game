// ------------------------------------------------------------
// MainMenuPanel.cs
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
    /// 主菜单面板：「开始」→ SceneLoader.LoadScene(Bootstrap)；「退出」→ Application.Quit。
    /// Config 字段为空时调 PlaceholderWarnOnce 占位，不崩溃。
    /// OnEnable/OnDisable 成对管理监听。
    /// </summary>
    public class MainMenuPanel : UIPanel
    {
        [Header("配置（Inspector 拖入）")]
        [SerializeField] private MainMenuConfig _config;

        [Header("UI 引用（生成器接线）")]
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _logoImage;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _startButtonLabel;
        [SerializeField] private Button _quitButton;
        [SerializeField] private TMP_Text _quitButtonLabel;

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (_startButton != null)
            {
                _startButton.onClick.AddListener(OnStartClicked);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.AddListener(OnQuitClicked);
            }
        }

        private void OnDisable()
        {
            if (_startButton != null)
            {
                _startButton.onClick.RemoveListener(OnStartClicked);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.RemoveListener(OnQuitClicked);
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
                    // 无背景图 → 置透明，露出面板深色占位底（否则默认白底会盖住深色、令白色文字隐形）
                    _backgroundImage.sprite = null;
                    _backgroundImage.color = new Color(1f, 1f, 1f, 0f);
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

            if (_titleLabel != null)
            {
                _titleLabel.text = !string.IsNullOrEmpty(_config.TitleText) ? _config.TitleText : "锚点解谜";
            }
            else
            {
                PlaceholderWarnOnce("_titleLabel");
            }

            ApplyButtonConfig(_startButton, _startButtonLabel, _config.StartButtonSprite, _config.StartButtonText, "开始游戏", "_startButton");
            ApplyButtonConfig(_quitButton, _quitButtonLabel, _config.QuitButtonSprite, _config.QuitButtonText, "退出", "_quitButton");
        }

        private void ApplyButtonConfig(Button btn, TMP_Text label, Sprite sprite, string text, string fallback, string fieldKey)
        {
            if (btn == null)
            {
                PlaceholderWarnOnce(fieldKey);
                return;
            }

            if (sprite != null)
            {
                var targetGraphic = btn.targetGraphic as Image;
                if (targetGraphic != null)
                {
                    targetGraphic.sprite = sprite;
                }
            }

            if (label != null)
            {
                label.text = !string.IsNullOrEmpty(text) ? text : fallback;
            }
        }

        private void OnStartClicked()
        {
            var loader = SceneLoader.Instance;
            if (loader == null || loader.IsLoading)
            {
                return;
            }

            if (_startButton != null)
            {
                _startButton.interactable = false;
            }

            loader.LoadScene(SceneNames.Bootstrap);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
