// ------------------------------------------------------------
// GlobalConfig.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 全局数值配置（1 个实例）。所有魔法数字集中于此，联调改数值零重编译。
    /// SO 只读暴露，运行时状态不进 SO。
    /// </summary>
    [CreateAssetMenu(fileName = "GlobalConfig", menuName = "Ciga/AnchorHorror/GlobalConfig")]
    public class GlobalConfig : ScriptableObject
    {
        [Header("San 值")]
        [SerializeField] private float _sanityMax = 100f;
        [SerializeField] private float _sanityInit = 100f;
        [SerializeField] private float _decayPerSec = 0.5f;
        [SerializeField] private float _matchGain = 5f;
        [SerializeField] private float _mismatchLoss = 15f;

        [Header("San 分级阈值")]
        [SerializeField] private float _thEdge = 70f;      // <70 进入 Edge（>=70 为 Normal）
        [SerializeField] private float _thDistorted = 50f; // [30,50) Distorted
        [SerializeField] private float _thCritical = 30f;  // (0,30) Critical
        [SerializeField] private float _hysteresis = 2f;   // 临界防抖缓冲

        [Header("初始房间触发")]
        [SerializeField] private int _candidateThreshold = 8;   // 候选物品件数
        [SerializeField] private float _timeThreshold = 180f;   // 秒

        [Header("锚点抽取")]
        [SerializeField] private int _targetCount = 5;
        [SerializeField] private int _requiredCountMin = 1;
        [SerializeField] private int _requiredCountMax = 3;

        [Header("交互")]
        [SerializeField] private float _interactRadius = 1.5f;

        [Header("Critical 表现")]
        [SerializeField] private float _moveSpeedPenalty = 0.2f; // 30 以下移速 -20%

        [Header("两关卡流程")]
        [SerializeField] private int _level1SelectCap = 5;
        [SerializeField] private int _level2BackpackCap = 8;
        [SerializeField] private float _level2TimeLimit = 180f;

        [Header("过渡")]
        [SerializeField] private float _fadeDuration = 0.8f;
        [SerializeField] private float _fadeHold = 0.08f; // 切场景时全黑停顿时长（秒），让"切画面"读得清

        public int Level1SelectCap => _level1SelectCap;
        public int Level2BackpackCap => _level2BackpackCap;
        public float Level2TimeLimit => _level2TimeLimit;

        public float SanityMax => _sanityMax;
        public float SanityInit => _sanityInit;
        public float DecayPerSec => _decayPerSec;
        public float MatchGain => _matchGain;
        public float MismatchLoss => _mismatchLoss;

        public float ThEdge => _thEdge;
        public float ThDistorted => _thDistorted;
        public float ThCritical => _thCritical;
        public float Hysteresis => _hysteresis;

        public int CandidateThreshold => _candidateThreshold;
        public float TimeThreshold => _timeThreshold;

        public int TargetCount => _targetCount;
        public int RequiredCountMin => _requiredCountMin;
        public int RequiredCountMax => _requiredCountMax;

        public float InteractRadius => _interactRadius;
        public float MoveSpeedPenalty => _moveSpeedPenalty;
        public float FadeDuration => _fadeDuration;
        public float FadeHold => _fadeHold;
    }
}
