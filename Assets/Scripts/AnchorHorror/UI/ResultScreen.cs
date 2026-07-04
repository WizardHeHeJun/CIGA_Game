// ------------------------------------------------------------
// ResultScreen.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 通关 / 失败结算界面（uGUI + TMP）：订阅 PhaseChanged，Clear/Fail 时激活盖屏面板，显示结果标题与操作提示。
    /// R 重开、Esc 返回主菜单。面板结构由 AnchorHorrorSetup 生成并接线；只订阅 EventBus，不被逻辑依赖。
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        private static readonly Color ClearColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color FailColor = new Color(1f, 0.4f, 0.4f);

        [Header("UI 引用（生成器接线）")]
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _hint;

        [Header("按键")]
        [SerializeField] private KeyCode _restartKey = KeyCode.R;
        [SerializeField] private KeyCode _menuKey = KeyCode.Escape;

        private GamePhase _phase = GamePhase.Boot;

        private void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
        }

        private void Start()
        {
            // 初始隐藏；即便生成器未接线（_root 为空）也安全。
            ApplyVisual();
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            _phase = newPhase;
            ApplyVisual();
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

        private void ApplyVisual()
        {
            bool show = IsResult();
            if (_root != null)
            {
                _root.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            bool clear = _phase == GamePhase.Clear;
            if (_title != null)
            {
                _title.text = clear ? "已 通 关" : "理 智 崩 溃";
                _title.color = clear ? ClearColor : FailColor;
            }

            if (_hint != null)
            {
                _hint.text = $"按 {_restartKey} 重新开始      按 {_menuKey} 返回主菜单";
            }
        }

        private bool IsResult()
        {
            return _phase == GamePhase.Clear || _phase == GamePhase.Fail;
        }
    }
}
