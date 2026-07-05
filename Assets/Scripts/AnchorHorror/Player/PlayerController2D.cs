// ------------------------------------------------------------
// PlayerController2D.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 俯视 2D 玩家移动（旧版 Input Manager）。Update 读输入、FixedUpdate 移动刚体。
    /// 低 San（Critical）时通过 SetSpeedMultiplier 降低移速；过渡/暂停时 InputEnabled=false 冻结。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController2D : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 3f;

        private Rigidbody2D _rb;
        private Vector2 _input;
        private float _speedMultiplier = 1f;

        // 场景边界（世界坐标）：玩家不得走出背景范围（用户需求：边界=背景大小）。
        private bool _hasBounds;
        private Vector2 _minBounds;
        private Vector2 _maxBounds;

        /// <summary>是否响应输入（默认关，进关卡时由 GameManager 打开）。</summary>
        public bool InputEnabled { get; set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;       // 俯视 2D 无重力
            _rb.freezeRotation = true;   // 俯视 2D 碰撞不旋转（修：受碰撞角色会转）
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                _input = Vector2.zero;
                return;
            }

            _input.x = Input.GetAxisRaw("Horizontal");
            _input.y = Input.GetAxisRaw("Vertical");
            if (_input.sqrMagnitude > 1f)
            {
                _input = _input.normalized;
            }
        }

        private void FixedUpdate()
        {
            if (!InputEnabled)
            {
                return;
            }

            Vector2 delta = _input * (_moveSpeed * _speedMultiplier * Time.fixedDeltaTime);
            Vector2 target = _rb.position + delta;

            // 边界内 clamp：到背景边缘停下，不走进图外虚空。
            if (_hasBounds)
            {
                target.x = Mathf.Clamp(target.x, _minBounds.x, _maxBounds.x);
                target.y = Mathf.Clamp(target.y, _minBounds.y, _maxBounds.y);
            }

            _rb.MovePosition(target);
        }

        /// <summary>设置移速倍率（1 = 正常，0.8 = -20%）。</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// 设置移动边界（世界坐标，已含玩家半身留白）。GameManager 每次建关卡时按背景包围盒调用。
        /// max 各分量 &gt; min 时生效；否则关闭边界（自由移动）。
        /// </summary>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            _hasBounds = max.x > min.x && max.y > min.y;
            _minBounds = min;
            _maxBounds = max;
        }

        /// <summary>关闭移动边界（无背景的场景）。</summary>
        public void ClearBounds()
        {
            _hasBounds = false;
        }
    }
}
