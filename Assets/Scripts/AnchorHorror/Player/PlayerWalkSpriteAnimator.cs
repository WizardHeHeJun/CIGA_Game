// ------------------------------------------------------------
// PlayerWalkSpriteAnimator.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 玩家行走 Sprite 换图表现：读取 PlayerController2D 的移动输入，按方向切换站立/行走帧。
    /// 只负责显示，不参与移动、碰撞或相机逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(PlayerController2D))]
    public class PlayerWalkSpriteAnimator : MonoBehaviour
    {
        private enum FacingDirection
        {
            Down,
            Up,
            Left,
            Right,
        }

        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private PlayerController2D _controller;

        [Header("向下")]
        [SerializeField] private Sprite _downIdle;
        [SerializeField] private Sprite _downWalk1;
        [SerializeField] private Sprite _downWalk2;

        [Header("向上")]
        [SerializeField] private Sprite _upIdle;
        [SerializeField] private Sprite _upWalk1;
        [SerializeField] private Sprite _upWalk2;

        [Header("向左")]
        [SerializeField] private Sprite _leftIdle;
        [SerializeField] private Sprite _leftWalk;

        [Header("向右")]
        [SerializeField] private Sprite _rightIdle;
        [SerializeField] private Sprite _rightWalk;

        [Header("受击反馈")]
        [SerializeField] private Sprite _hitDown;
        [SerializeField] private Sprite _hitUp;
        [SerializeField] private Sprite _hitLeft;
        [SerializeField] private Sprite _hitRight;
        [SerializeField] private float _hitFeedbackDuration = 1.5f;

        [SerializeField] private float _frameInterval = 0.16f;

        private FacingDirection _facing = FacingDirection.Down;
        private float _frameTimer;
        private int _frameIndex;
        private float _hitFeedbackRemaining;
        private Sprite _currentSprite;

        private void Awake()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_controller == null)
            {
                _controller = GetComponent<PlayerController2D>();
            }
        }

        private void OnEnable()
        {
            EventBus.ItemMismatched += OnItemMismatched;
            _frameTimer = 0f;
            _frameIndex = 0;
            _hitFeedbackRemaining = 0f;
            ApplySprite(GetIdleSprite(_facing));
        }

        private void OnDisable()
        {
            EventBus.ItemMismatched -= OnItemMismatched;
        }

        private void LateUpdate()
        {
            if (_spriteRenderer == null || _controller == null)
            {
                return;
            }

            if (_hitFeedbackRemaining > 0f)
            {
                if (_controller.IsMoving)
                {
                    UpdateFacing(_controller.MoveInput);
                }

                _hitFeedbackRemaining -= Time.deltaTime;
                ApplySprite(GetHitSprite(_facing));
                return;
            }

            if (!_controller.IsMoving)
            {
                _frameTimer = 0f;
                _frameIndex = 0;
                ApplySprite(GetIdleSprite(_facing));
                return;
            }

            UpdateFacing(_controller.MoveInput);
            AdvanceFrame();
            ApplySprite(GetWalkSprite(_facing));
        }

        private void OnItemMismatched(FeatureTag item)
        {
            if (_controller != null && _controller.MoveInput.sqrMagnitude > 0.0001f)
            {
                UpdateFacing(_controller.MoveInput);
            }

            _frameTimer = 0f;
            _frameIndex = 0;
            _hitFeedbackRemaining = Mathf.Max(0.01f, _hitFeedbackDuration);
        }

        private void UpdateFacing(Vector2 input)
        {
            FacingDirection next = _facing;
            float absX = Mathf.Abs(input.x);
            float absY = Mathf.Abs(input.y);
            if (absX > absY)
            {
                next = input.x < 0f ? FacingDirection.Left : FacingDirection.Right;
            }
            else if (absY > 0f)
            {
                next = input.y < 0f ? FacingDirection.Down : FacingDirection.Up;
            }

            if (next == _facing)
            {
                return;
            }

            _facing = next;
            _frameTimer = 0f;
            _frameIndex = 0;
        }

        private void AdvanceFrame()
        {
            float interval = Mathf.Max(0.01f, _frameInterval);
            _frameTimer += Time.deltaTime;
            if (_frameTimer < interval)
            {
                return;
            }

            _frameTimer -= interval;
            _frameIndex = 1 - _frameIndex;
        }

        private Sprite GetIdleSprite(FacingDirection direction)
        {
            switch (direction)
            {
                case FacingDirection.Up:
                    return FirstValid(_upIdle, _downIdle);
                case FacingDirection.Left:
                    return FirstValid(_leftIdle, _downIdle);
                case FacingDirection.Right:
                    return FirstValid(_rightIdle, _downIdle);
                default:
                    return _downIdle;
            }
        }

        private Sprite GetWalkSprite(FacingDirection direction)
        {
            switch (direction)
            {
                case FacingDirection.Up:
                    return _frameIndex == 0
                        ? FirstValid(_upWalk1, _upIdle, _downIdle)
                        : FirstValid(_upWalk2, _upWalk1, _upIdle, _downIdle);
                case FacingDirection.Left:
                    return _frameIndex == 0
                        ? FirstValid(_leftIdle, _downIdle)
                        : FirstValid(_leftWalk, _leftIdle, _downIdle);
                case FacingDirection.Right:
                    return _frameIndex == 0
                        ? FirstValid(_rightIdle, _downIdle)
                        : FirstValid(_rightWalk, _rightIdle, _downIdle);
                default:
                    return _frameIndex == 0
                        ? FirstValid(_downWalk1, _downIdle)
                        : FirstValid(_downWalk2, _downWalk1, _downIdle);
            }
        }

        private Sprite GetHitSprite(FacingDirection direction)
        {
            switch (direction)
            {
                case FacingDirection.Up:
                    return FirstValid(_hitUp, _upIdle, _downIdle);
                case FacingDirection.Left:
                    return FirstValid(_hitLeft, _leftIdle, _downIdle);
                case FacingDirection.Right:
                    return FirstValid(_hitRight, _rightIdle, _downIdle);
                default:
                    return FirstValid(_hitDown, _downIdle);
            }
        }

        private Sprite FirstValid(Sprite first, Sprite fallback)
        {
            return first != null ? first : fallback;
        }

        private Sprite FirstValid(Sprite first, Sprite second, Sprite fallback)
        {
            if (first != null)
            {
                return first;
            }

            return second != null ? second : fallback;
        }

        private Sprite FirstValid(Sprite first, Sprite second, Sprite third, Sprite fallback)
        {
            if (first != null)
            {
                return first;
            }

            if (second != null)
            {
                return second;
            }

            return third != null ? third : fallback;
        }

        private void ApplySprite(Sprite sprite)
        {
            if (sprite == null || sprite == _currentSprite)
            {
                return;
            }

            _currentSprite = sprite;
            _spriteRenderer.sprite = sprite;
        }
    }
}
