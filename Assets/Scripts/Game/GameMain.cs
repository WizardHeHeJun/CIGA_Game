// ------------------------------------------------------------
// GameMain.cs
// Author : WizardHeHeJun
// Created: 2026-06-17
// ------------------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ciga.Game
{
    /// <summary>
    /// 游戏主入口 / 主菜单占位。挂在 GameMain 场景（构建列表第 0 个）的入口对象上：
    /// 运行时构建一个极简 uGUI 主菜单（标题 + 开始提示），按 空格/回车 加载首个玩法场景（Bootstrap），
    /// 其内 GameManager 自启接管。作为菜单场景的控制器，不跨场景常驻——LoadScene 时随场景卸载；
    /// GameManager.ReturnToMainMenu 重载本场景即可回到菜单。正式登录/资源加载管线待接入 <see cref="StartLoading"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public class GameMain : MonoBehaviour
    {
        private static readonly KeyCode[] StartKeys = { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter };

        [Header("启动配置")]
        [Tooltip("勾选则跳过主菜单，Start 时直接加载首个玩法场景（自动化 / 联调用）。")]
        [SerializeField] private bool _autoStart;

        [Tooltip("启动流程加载的首个场景（玩法入口 Bootstrap，需在 Build Settings 中）。")]
        [SerializeField] private string _firstScene = "Bootstrap";

        [Header("主菜单占位文案")]
        [SerializeField] private string _titleText = "锚点解谜";
        [SerializeField] private string _promptText = "按 空格 / 回车 开始";

        /// <summary>是否已触发加载，避免重复进入。</summary>
        public bool HasStarted { get; private set; }

        private bool _menuActive;

        private void Start()
        {
            if (_autoStart)
            {
                StartLoading();
                return;
            }

            BuildMenu();
        }

        private void Update()
        {
            if (HasStarted || !_menuActive)
            {
                return;
            }

            for (int i = 0; i < StartKeys.Length; i++)
            {
                if (Input.GetKeyDown(StartKeys[i]))
                {
                    StartLoading();
                    return;
                }
            }
        }

        /// <summary>
        /// 进入游戏：加载首个玩法场景（Bootstrap），由其内 GameManager 自启接管。
        /// 供主菜单按键、_autoStart 或外部入口调用（幂等）。
        /// TODO: 后续在此前接入正式登录 / 资源加载管线（登录 → 资源 → 进场景）。
        /// </summary>
        public void StartLoading()
        {
            if (HasStarted)
            {
                return;
            }

            HasStarted = true;
            _menuActive = false;
            SceneManager.LoadScene(_firstScene);
        }

        /// <summary>运行时构建极简 uGUI 主菜单（ScreenSpaceOverlay + TMP，无按钮，纯键盘触发）。</summary>
        private void BuildMenu()
        {
            var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CreateText(canvasGo.transform, "Title", _titleText, 96f, FontStyles.Bold,
                new Vector2(0f, 90f), new Color(0.95f, 0.9f, 0.8f));
            CreateText(canvasGo.transform, "Prompt", _promptText, 40f, FontStyles.Normal,
                new Vector2(0f, -70f), new Color(0.85f, 0.85f, 0.9f));

            _menuActive = true;
        }

        private static void CreateText(Transform parent, string name, string text, float fontSize,
            FontStyles style, Vector2 anchoredPos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;

            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1400f, 200f);
            rt.anchoredPosition = anchoredPos;
        }
    }
}
