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
    /// 关卡门：由 GameManager 代码建于 _levelRoot 下。
    /// 三种类型（DoorKind）：
    ///   EnterLevel2        —— 关卡1门，InitRoom && SelectionLocked 后可交互，触发 EnterLevel2()
    ///   SwitchSubScenePrev —— 子场景左门，HorrorLevel 可交互，触发 SwitchSubScene(-1)
    ///   SwitchSubSceneNext —— 子场景右门，HorrorLevel 可交互，触发 SwitchSubScene(+1)
    /// 门上方世界空间显示提示文案（仅可交互时），引导玩家走向门（#2）。随 _levelRoot 销毁清除（ADR-1/5）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class LevelDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _normalColor = new Color(0.6f, 0.8f, 1f, 1f);
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0.6f, 1f);

        private DoorKind _doorKind = DoorKind.SwitchSubSceneNext;
        private bool _highlighted;
        private string _prompt;
        private TextMeshPro _promptText;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>由 GameManager.SpawnDoor 调用：设置门类型、精灵与提示文案（ADR-1/5）。</summary>
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
            EnsurePromptText();
        }

        private void Update()
        {
            // 门可交互时在上方显示提示，引导玩家走向门（#2）。不可交互（如关卡1未选满）时隐藏。
            if (_promptText == null)
            {
                return;
            }

            var gm = GameManager.Instance;
            bool show = gm != null && CanInteract(gm.CurrentPhase);
            if (_promptText.gameObject.activeSelf != show)
            {
                _promptText.gameObject.SetActive(show);
            }
        }

        // -------- IInteractable 实现 --------

        /// <summary>
        /// EnterLevel2：InitRoom 且 SelectionLocked（选满5并已抽锚点）时可交互（SC-1/3）。
        /// SwitchSubScenePrev/Next：HorrorLevel 时可交互（SC-4，#3）。
        /// </summary>
        public bool CanInteract(GamePhase phase)
        {
            switch (_doorKind)
            {
                case DoorKind.EnterLevel2:
                    return phase == GamePhase.InitRoom &&
                           GameManager.Instance != null &&
                           GameManager.Instance.SelectionLocked;

                case DoorKind.SwitchSubScenePrev:
                case DoorKind.SwitchSubSceneNext:
                    return phase == GamePhase.HorrorLevel;

                default:
                    return false;
            }
        }

        /// <summary>
        /// EnterLevel2 → gm.EnterLevel2()；Prev → gm.SwitchSubScene(-1)；Next → gm.SwitchSubScene(+1)（ADR-5，#3）。
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

                case DoorKind.SwitchSubScenePrev:
                    gm.SwitchSubScene(-1);
                    break;

                case DoorKind.SwitchSubSceneNext:
                    gm.SwitchSubScene(1);
                    break;
            }
        }

        /// <summary>门无可检视信息（IInteractable 接口要求的空实现）。</summary>
        public void Inspect()
        {
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

        /// <summary>在门上方建世界空间提示文本（初始隐藏，Update 在可交互时显示）。</summary>
        private void EnsurePromptText()
        {
            if (_promptText != null)
            {
                _promptText.text = _prompt;
                return;
            }

            var go = new GameObject("DoorPrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            _promptText = go.AddComponent<TextMeshPro>();
            _promptText.text = _prompt;
            _promptText.fontSize = 4f;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.color = new Color(1f, 1f, 0.7f, 1f);
            _promptText.sortingOrder = 2000;
            _promptText.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
            go.SetActive(false);
        }
    }
}
