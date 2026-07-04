// ------------------------------------------------------------
// LevelDoor.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡门：子关通关（SubClear）后由 GameManager.EnterSubClear 代码生成于 _levelRoot 下。
    /// 仅 SubClear 相位可交互（ADR-2/3/6）；Interact → GameManager.AdvanceLevel() 触发换关。
    /// 随 _levelRoot 销毁一并清除，门控双保险：物理上不存在 + CanInteract 锁相位（陷阱 2）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class LevelDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _normalColor = new Color(0.6f, 0.8f, 1f, 1f);
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0.6f, 1f);

        private bool _highlighted;
        // 提示文案：当前由子关结算界面统一展示（ResultConfig.SubClear.Hint「走到门按 E」）；
        // 此字段为门自身未来的世界空间提示预留，暂不渲染。
        private string _prompt;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>
        /// 由 GameManager.EnterSubClear 调用：设置门的精灵与提示文案。
        /// </summary>
        public void Configure(Sprite sprite, string prompt)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (sprite != null)
            {
                _renderer.sprite = sprite;
            }

            _renderer.color = _normalColor;
            _prompt = prompt;
        }

        // -------- IInteractable 实现 --------

        /// <summary>仅 SubClear 相位可交互（ADR-6，陷阱 2）。</summary>
        public bool CanInteract(GamePhase phase)
        {
            return phase == GamePhase.SubClear;
        }

        /// <summary>触发换关（ADR-1/3）。</summary>
        public void Interact()
        {
            GameManager.Instance?.AdvanceLevel();
        }

        /// <summary>切换高亮：改 SpriteRenderer 颜色。</summary>
        public void SetHighlight(bool on)
        {
            if (_highlighted == on || _renderer == null)
            {
                return;
            }

            _highlighted = on;
            _renderer.color = on ? _highlightColor : _normalColor;
        }
    }
}
