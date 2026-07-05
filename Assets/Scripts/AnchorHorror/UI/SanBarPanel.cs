// ------------------------------------------------------------
// SanBarPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡2 San 值条：san frame 外框 + san 填充。填充图裁剪到条形区域后用 Image.Type=Filled
    /// （水平、从左），fillAmount = current / max，San 减少时从右端线性缩减，直观反馈剩余理智。
    /// 订阅 EventBus.SanityChanged 更新填充；仅 HorrorLevel 相位显示（镜像 CountdownPanel）。
    /// San 无公开取值接口，值走事件；初始满，避免首个事件早于订阅时空白。
    /// </summary>
    public class SanBarPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;

        [Tooltip("San 填充图（Image.Type=Filled, Horizontal, 从左）。fillAmount = current/max。")]
        [SerializeField] private Image _fill;

        private void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
            EventBus.SanityChanged += OnSanityChanged;
        }

        private void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.SanityChanged -= OnSanityChanged;
        }

        private void Start()
        {
            if (_root == null)
            {
                Debug.LogWarning("[AnchorHorror] SanBarPanel 未接线（_root 为空）：San 条不可见。" +
                                 "请运行菜单 Ciga/AnchorHorror/生成可运行装配 重建场景。");
            }

            if (_fill != null)
            {
                _fill.fillAmount = 1f; // San 初始满
            }

            SetVisible(false);
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            SetVisible(newPhase == GamePhase.HorrorLevel);
        }

        private void OnSanityChanged(float current, float max)
        {
            if (_fill != null)
            {
                _fill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }
    }
}
