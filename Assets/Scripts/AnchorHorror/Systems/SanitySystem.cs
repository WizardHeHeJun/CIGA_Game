// ------------------------------------------------------------
// SanitySystem.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// San 值系统：数值管理（决策 A：溢出截断到 Max）、环境衰减、带 hysteresis 的分级。
    /// 跨越阈值只广播一次 SanityStateChanged；归零由订阅方（GameManager）响应为 Fail。
    /// </summary>
    public class SanitySystem : MonoBehaviour
    {
        private GlobalConfig _config;
        private SanityState _state = SanityState.Normal;

        /// <summary>当前 San 值。</summary>
        public float Current { get; private set; }

        /// <summary>上限。</summary>
        public float Max { get; private set; }

        public SanityState State => _state;

        /// <summary>是否开启每秒衰减（仅 HorrorLevel 阶段为 true）。</summary>
        public bool DecayEnabled { get; set; }

        /// <summary>由 GameManager 注入配置并初始化数值。</summary>
        public void Init(GlobalConfig config)
        {
            _config = config;
            Max = config.SanityMax;
            Current = Mathf.Clamp(config.SanityInit, 0f, Max);
            _state = Evaluate(Current, SanityState.Normal);
            DecayEnabled = false;
            EventBus.RaiseSanityChanged(Current, Max);
        }

        private void Update()
        {
            if (!DecayEnabled || _config == null)
            {
                return;
            }

            if (Current <= 0f)
            {
                return;
            }

            Modify(-_config.DecayPerSec * Time.deltaTime);
        }

        /// <summary>统一的数值改动入口。决策 A：Clamp 到 [0, Max]，溢出直接截断。</summary>
        public void Modify(float delta)
        {
            if (_config == null)
            {
                return;
            }

            float prev = Current;
            Current = Mathf.Clamp(Current + delta, 0f, Max);
            if (!Mathf.Approximately(prev, Current))
            {
                EventBus.RaiseSanityChanged(Current, Max);
            }

            UpdateState();
        }

        private void UpdateState()
        {
            var next = Evaluate(Current, _state);
            if (next != _state)
            {
                var old = _state;
                _state = next;
                EventBus.RaiseSanityStateChanged(old, next);
            }
        }

        /// <summary>
        /// 依当前值与当前状态评估目标状态；用 hysteresis 缓冲避免临界反复抖动。
        /// 只有"越过阈值 ± 缓冲"才切换，否则维持 current。
        /// </summary>
        private SanityState Evaluate(float value, SanityState current)
        {
            float h = _config.Hysteresis;

            // Dead 是终态：归 0 立即死；且一旦 Dead 不再回滚（本游戏 San 归 0 = 永久失败）。
            // 这样 Critical→Dead 只走这条快速路径，不掺入下面的 hysteresis 结构，避免双规则歧义。
            if (value <= 0f)
            {
                return SanityState.Dead;
            }

            if (current == SanityState.Dead)
            {
                return SanityState.Dead;
            }

            // 基准阈值（无缓冲）判定一个"裸状态"
            SanityState bare = BareState(value);
            if (bare == current)
            {
                return current;
            }

            // 跨阈值时要求越过缓冲带，才真正切换，减少边界抖动
            if (bare > current) // 恶化方向（枚举值更大 = 更差）
            {
                float boundary = LowerBoundary(current); // 从 current 掉到更差状态的边界
                if (value <= boundary - h)
                {
                    return bare;
                }
            }
            else // 恢复方向
            {
                float boundary = UpperBoundary(current); // 从 current 回到更好状态的边界
                if (value >= boundary + h)
                {
                    return bare;
                }
            }

            return current;
        }

        private SanityState BareState(float value)
        {
            if (value <= 0f)
            {
                return SanityState.Dead;
            }

            if (value < _config.ThCritical)
            {
                return SanityState.Critical;
            }

            if (value < _config.ThDistorted)
            {
                return SanityState.Distorted;
            }

            if (value < _config.ThEdge)
            {
                return SanityState.Edge;
            }

            return SanityState.Normal;
        }

        // current 状态跌向更差状态时的临界值（value 低于它就该恶化）
        private float LowerBoundary(SanityState current)
        {
            switch (current)
            {
                case SanityState.Normal: return _config.ThEdge;       // <70 → Edge
                case SanityState.Edge: return _config.ThDistorted;    // <50 → Distorted
                case SanityState.Distorted: return _config.ThCritical;// <30 → Critical
                default: return 0f;                                   // Critical → Dead 由 value<=0 处理
            }
        }

        // current 状态回向更好状态时的临界值（value 高于它就该恢复）
        private float UpperBoundary(SanityState current)
        {
            switch (current)
            {
                case SanityState.Critical: return _config.ThCritical; // >=30 → Distorted
                case SanityState.Distorted: return _config.ThDistorted;// >=50 → Edge
                case SanityState.Edge: return _config.ThEdge;         // >=70 → Normal
                default: return _config.SanityMax;
            }
        }
    }
}
