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

        /// <summary>是否响应输入（默认关，进关卡时由 GameManager 打开）。</summary>
        public bool InputEnabled { get; set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f; // 俯视 2D 无重力
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
            _rb.MovePosition(_rb.position + delta);
        }

        /// <summary>设置移速倍率（1 = 正常，0.8 = -20%）。</summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0f, multiplier);
        }
    }
}
