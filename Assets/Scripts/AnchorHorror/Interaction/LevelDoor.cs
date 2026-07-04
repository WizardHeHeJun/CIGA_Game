// ------------------------------------------------------------
// LevelDoor.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡门：由 GameManager 代码建于 _levelRoot 下。
    /// 两种类型（DoorKind）：
    ///   EnterLevel2    —— 关卡1门，InitRoom && SelectionLocked 后可交互，触发 EnterLevel2()
    ///   SwitchSubScene —— 子场景门，HorrorLevel 中可交互，触发 SwitchSubScene()
    /// 随 _levelRoot 销毁一并清除（ADR-1/5）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class LevelDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _normalColor = new Color(0.6f, 0.8f, 1f, 1f);
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0.6f, 1f);

        private DoorKind _doorKind = DoorKind.SwitchSubScene;
        private bool _highlighted;
        private string _prompt;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>
        /// 由 GameManager.SpawnLevelDoor 调用：设置门类型、精灵与提示文案（ADR-1/5）。
        /// </summary>
        public void Configure(DoorKind kind, Sprite sprite, string prompt)
        {
            _doorKind = kind;

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

        /// <summary>
        /// EnterLevel2：InitRoom 且 SelectionLocked（选满5并已抽锚点）时可交互（SC-1/3）。
        /// SwitchSubScene：HorrorLevel 时可交互（SC-4）。
        /// </summary>
        public bool CanInteract(GamePhase phase)
        {
            switch (_doorKind)
            {
                case DoorKind.EnterLevel2:
                    return phase == GamePhase.InitRoom &&
                           GameManager.Instance != null &&
                           GameManager.Instance.SelectionLocked;

                case DoorKind.SwitchSubScene:
                    return phase == GamePhase.HorrorLevel;

                default:
                    return false;
            }
        }

        /// <summary>
        /// EnterLevel2 → gm.EnterLevel2()；SwitchSubScene → gm.SwitchSubScene()（ADR-5）。
        /// </summary>
        public void Interact()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            switch (_doorKind)
            {
                case DoorKind.EnterLevel2:
                    gm.EnterLevel2();
                    break;

                case DoorKind.SwitchSubScene:
                    gm.SwitchSubScene();
                    break;
            }
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
