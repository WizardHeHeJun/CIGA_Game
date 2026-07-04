// ------------------------------------------------------------
// CountdownPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.Text;
using TMPro;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡2 倒计时面板：仅在 HorrorLevel 相位下显示，每帧从 GameManager.RemainingTime 读秒数，
    /// 以 MM:SS 格式渲染到 TextMeshProUGUI。其余相位自动隐藏。
    /// 基础版（迭代 A）：判空降级（GameManager 或 RemainingTime 源缺失时静默不报错）。
    /// 打磨（数字动画、颜色变化）留迭代 B。
    /// </summary>
    public class CountdownPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _timeText;

        [Header("紧迫强调（迭代B，SC-B3）")]
        [SerializeField] private float _warnThreshold = 30f;      // 剩余 ≤ 此秒数时变红
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _warnColor = new Color(1f, 0.25f, 0.2f, 1f);

        private readonly StringBuilder _sb = new StringBuilder(8); // "MM:SS\0" 最多 6 字符，留余量

        private int _lastDisplayedSeconds = -1; // 仅秒数变化才刷新文本，避免热路径字符串 GC

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
            if (_root == null)
            {
                Debug.LogWarning("[AnchorHorror] CountdownPanel 未接线（_root 为空）：倒计时面板不可见。" +
                                 "请运行菜单 Ciga/AnchorHorror/生成可运行装配 重建场景以接上面板。");
            }

            // 初始隐藏：Boot/InitRoom 时不显示
            SetVisible(false);
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf)
            {
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            // 仅秒数有变化时才重建文本，避免每帧 StringBuilder+TextMeshPro 触发字符串分配
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(gm.RemainingTime));
            if (totalSeconds == _lastDisplayedSeconds)
            {
                return;
            }

            _lastDisplayedSeconds = totalSeconds;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            _sb.Clear();
            // 格式化 MM:SS（分/秒各两位，不足补 0）
            if (minutes < 10)
            {
                _sb.Append('0');
            }

            _sb.Append(minutes);
            _sb.Append(':');
            if (seconds < 10)
            {
                _sb.Append('0');
            }

            _sb.Append(seconds);

            if (_timeText != null)
            {
                _timeText.color = totalSeconds <= _warnThreshold ? _warnColor : _normalColor; // <阈值变红（SC-B3）
                _timeText.SetText(_sb);
            }
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            SetVisible(newPhase == GamePhase.HorrorLevel);
            if (newPhase == GamePhase.HorrorLevel)
            {
                // 相位切入时强制重置缓存，确保立刻刷新文本
                _lastDisplayedSeconds = -1;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_root == null)
            {
                return;
            }

            _root.SetActive(visible);
            if (!visible)
            {
                _lastDisplayedSeconds = -1;
            }
        }
    }
}
