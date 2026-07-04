// ------------------------------------------------------------
// EventBus.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 轻量静态事件中心，只承载"发生了什么"的通知（表现 / UI 层订阅）。
    /// 核心逻辑协作（如 Anchor→Sanity 的数值改动）走 GameManager 注入的直接引用，不走本总线。
    /// 订阅方务必成对 OnEnable/OnDisable。ClearAll() 仅用于测试 / 彻底重置（会连常驻 UI 订阅一起清），常规重开局勿调。
    /// </summary>
    public static class EventBus
    {
        // ---- 候选 / 抽取 ----
        public static event Action<int> CandidateCollected;                        // (当前候选物品数)
        public static event Action<IReadOnlyList<AnchorTarget>> TargetsExtracted;  // 抽取完成的 5 个目标锚点

        // ---- 匹配 ----
        public static event Action<FeatureTag, IReadOnlyList<FeatureUnit>> ItemMatched; // (物品, 本次命中的特征)
        public static event Action<FeatureTag> ItemMismatched;                          // 不匹配（已扣分并消耗）
        public static event Action<FeatureTag> ItemInspected;                           // 检视物品（R 键，听声音/看信息）

        // ---- 锚点 ----
        public static event Action<AnchorTarget> AnchorActivated;  // 某锚点刚被激活
        public static event Action AllAnchorsActivated;            // 5 个全激活 → 通关信号

        // ---- San ----
        public static event Action<float, float> SanityChanged;                    // (current, max)
        public static event Action<SanityState, SanityState> SanityStateChanged;   // (old, new)

        // ---- 阶段 ----
        public static event Action<GamePhase, GamePhase> PhaseChanged;             // (old, new)

        public static void RaiseCandidateCollected(int count)
        {
            CandidateCollected?.Invoke(count);
        }

        public static void RaiseTargetsExtracted(IReadOnlyList<AnchorTarget> targets)
        {
            TargetsExtracted?.Invoke(targets);
        }

        public static void RaiseItemMatched(FeatureTag item, IReadOnlyList<FeatureUnit> hits)
        {
            ItemMatched?.Invoke(item, hits);
        }

        public static void RaiseItemMismatched(FeatureTag item)
        {
            ItemMismatched?.Invoke(item);
        }

        public static void RaiseItemInspected(FeatureTag item)
        {
            ItemInspected?.Invoke(item);
        }

        public static void RaiseAnchorActivated(AnchorTarget anchor)
        {
            AnchorActivated?.Invoke(anchor);
        }

        public static void RaiseAllAnchorsActivated()
        {
            AllAnchorsActivated?.Invoke();
        }

        public static void RaiseSanityChanged(float current, float max)
        {
            SanityChanged?.Invoke(current, max);
        }

        public static void RaiseSanityStateChanged(SanityState oldState, SanityState newState)
        {
            SanityStateChanged?.Invoke(oldState, newState);
        }

        public static void RaisePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            PhaseChanged?.Invoke(oldPhase, newPhase);
        }

        /// <summary>
        /// 清空所有订阅。仅供测试 / 彻底重置：会移除包括常驻 UI 在内的全部订阅方，
        /// 常规重开局不要调用（否则 MemoryPanel/SanityFeedback 等需重新 OnEnable 才恢复）。
        /// </summary>
        public static void ClearAll()
        {
            CandidateCollected = null;
            TargetsExtracted = null;
            ItemMatched = null;
            ItemMismatched = null;
            ItemInspected = null;
            AnchorActivated = null;
            AllAnchorsActivated = null;
            SanityChanged = null;
            SanityStateChanged = null;
            PhaseChanged = null;
        }
    }
}
