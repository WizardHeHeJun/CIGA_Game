// ------------------------------------------------------------
// ResultScreen.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections;
using TMPro;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 三态结算界面（uGUI + TMP）：订阅 PhaseChanged，SubClear/Victory/Fail 时激活盖屏面板。
    /// 视觉与按键响应由挂载的 ResultConfig SO 数据驱动（ADR-5）：
    ///   - SubClear：显示过关提示，R/Esc 均关闭（只等玩家走门），防止误按重开（陷阱 5）；
    ///   - Victory ：显示胜利结算，Esc→返回主菜单；
    ///   - Fail    ：显示失败结算，R→重开局，Esc→返回主菜单。
    /// 面板结构由 AnchorHorrorSetup 生成并接线；只订阅 EventBus，不被逻辑依赖。
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        [Header("UI 引用（生成器接线）")]
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _hint;

        [Header("结算配置（ADR-5）")]
        [SerializeField] private ResultConfig _resultConfig;

        [Header("按键")]
        [SerializeField] private KeyCode _restartKey = KeyCode.R;
        [SerializeField] private KeyCode _menuKey = KeyCode.Escape;

        [Header("过渡")]
        [SerializeField] private float _fadeDuration = 0.4f; // 结算面板淡入时长（#1）

        private GamePhase _phase = GamePhase.Boot;
        private CanvasGroup _canvasGroup;
        private Coroutine _fadeRoutine;

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

            var entry = _resultConfig != null ? _resultConfig.Get(_phase) : null;
            var gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            // 按 ResultConfig 开关门控 R/Esc（陷阱 5：SubClear 两者 false，防止玩家误按 R 直接重开）
            if (entry != null && entry.RespondsRestart && Input.GetKeyDown(_restartKey))
            {
                gm.RestartGame();
            }
            else if (entry != null && entry.RespondsMenu && Input.GetKeyDown(_menuKey))
            {
                gm.ReturnToMainMenu();
            }
        }

        private void ApplyVisual()
        {
            bool show = IsResult();

            if (!show)
            {
                if (_fadeRoutine != null)
                {
                    StopCoroutine(_fadeRoutine);
                    _fadeRoutine = null;
                }

                if (_root != null)
                {
                    _root.SetActive(false);
                }

                return;
            }

            if (_root != null)
            {
                _root.SetActive(true);
            }

            // 结算面板淡入（#1）：CanvasGroup alpha 0→1，比瞬间弹出更柔和。
            var cg = EnsureCanvasGroup();
            if (cg != null)
            {
                cg.alpha = 0f;
                if (_fadeRoutine != null)
                {
                    StopCoroutine(_fadeRoutine);
                }

                _fadeRoutine = StartCoroutine(FadeInRoutine(cg));
            }

            // 有 ResultConfig 时走数据化路径；无配置兜底显示空内容（避免空引用崩溃）
            if (_resultConfig != null)
            {
                var entry = _resultConfig.Get(_phase);
                if (entry != null)
                {
                    if (_title != null)
                    {
                        _title.text = entry.Title;
                        _title.color = entry.Color;
                    }

                    if (_hint != null)
                    {
                        _hint.text = entry.Hint;
                    }
                }
            }
        }

        // _root 上懒挂 CanvasGroup（生成器未加也能自愈，无需重跑装配）。
        private CanvasGroup EnsureCanvasGroup()
        {
            if (_root == null)
            {
                return null;
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = _root.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = _root.AddComponent<CanvasGroup>();
                }
            }

            return _canvasGroup;
        }

        private IEnumerator FadeInRoutine(CanvasGroup cg)
        {
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime; // 结算可能处于暂停/timeScale 异常，用 unscaled 保证淡入照常
                cg.alpha = _fadeDuration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _fadeDuration)) : 1f;
                yield return null;
            }

            cg.alpha = 1f;
            _fadeRoutine = null;
        }

        private bool IsResult()
        {
            return _phase == GamePhase.SubClear || _phase == GamePhase.Victory || _phase == GamePhase.Fail;
        }
    }
}
