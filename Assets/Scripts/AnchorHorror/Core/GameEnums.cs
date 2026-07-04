// ------------------------------------------------------------
// GameEnums.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------

namespace Ciga.AnchorHorror
{
    /// <summary>游戏阶段状态机。</summary>
    public enum GamePhase
    {
        Boot,         // 未开始 / 初始化中
        InitRoom,     // 初始房间：收集候选锚点
        Transition,   // 过渡：黑屏+低语，加载关卡→扫描→抽锚点
        HorrorLevel,  // 恐怖关卡：衰减+匹配
        SubClear,     // 子关通关：本关锚点全激活，等待玩家通过门进入下一关
        Victory,      // 最终胜利：末关通关
        Fail,         // 失败
    }

    /// <summary>San 值分级状态。数值区间见 anchor-horror-design.md §5。</summary>
    public enum SanityState
    {
        Normal,     // 100-70
        Edge,       // 70-50
        Distorted,  // 50-30
        Critical,   // 30-0
        Dead,       // 0
    }
}
