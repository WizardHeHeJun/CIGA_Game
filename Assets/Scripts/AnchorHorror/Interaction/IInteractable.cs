// ------------------------------------------------------------
// IInteractable.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 可交互对象接口。实现类负责判断「当前相位下自身是否可交互」（CanInteract），
    /// 以及「触发交互后的业务行为」（Interact）与「视觉高亮切换」（SetHighlight）。
    /// InteractionSystem 通过此接口操作所有可交互对象，自身零相位知识。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>当前游戏阶段下，此对象是否允许交互。</summary>
        bool CanInteract(GamePhase phase);

        /// <summary>执行交互逻辑。调用前须保证 CanInteract 为 true。</summary>
        void Interact();

        /// <summary>切换高亮状态（靠近/离开时由 InteractionSystem 调用）。</summary>
        void SetHighlight(bool on);
    }
}
