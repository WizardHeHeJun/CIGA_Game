// ------------------------------------------------------------
// ResultScreen.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 通关 / 失败结算界面（IMGUI 占位）：订阅 PhaseChanged，Clear/Fail 时半透明盖屏显示结果与操作提示。
    /// R 重开、Esc 返回主菜单。只订阅 EventBus，不被逻辑依赖。
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        [SerializeField] private KeyCode _restartKey = KeyCode.R;
        [SerializeField] private KeyCode _menuKey = KeyCode.Escape;

        private GamePhase _phase = GamePhase.Boot;
        private GUIStyle _titleStyle;
        private GUIStyle _hintStyle;

        private void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            _phase = newPhase;
        }

        private void Update()
        {
            if (!IsResult())
            {
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            if (Input.GetKeyDown(_restartKey))
            {
                gm.RestartGame();
            }
            else if (Input.GetKeyDown(_menuKey))
            {
                gm.ReturnToMainMenu();
            }
        }

        private void OnGUI()
        {
            if (!IsResult())
            {
                return;
            }

            EnsureStyles();

            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;

            bool clear = _phase == GamePhase.Clear;
            _titleStyle.normal.textColor = clear ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.4f);
            GUI.Label(new Rect(0, Screen.height * 0.34f, Screen.width, 90), clear ? "已 通 关" : "理 智 崩 溃", _titleStyle);
            GUI.Label(new Rect(0, Screen.height * 0.5f, Screen.width, 40), "按 R 重新开始      按 Esc 返回主菜单", _hintStyle);
        }

        private bool IsResult()
        {
            return _phase == GamePhase.Clear || _phase == GamePhase.Fail;
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 48,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
            }

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter,
                };
                _hintStyle.normal.textColor = Color.white;
            }
        }
    }
}
