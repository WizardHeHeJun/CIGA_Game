// ------------------------------------------------------------
// InteractionSystem.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 2D 交互系统：每帧取玩家半径内最近的 IInteractable（且 CanInteract 为 true）高亮，
    /// 按交互键触发 Interact()。相位判断已下沉到各 IInteractable 实现，本类零相位知识（ADR-2/6）。
    /// 用 OverlapCircleNonAlloc 避免热路径分配。
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [SerializeField] private Transform _player;
        [SerializeField] private LayerMask _interactMask = ~0;
        [SerializeField] private KeyCode _interactKey = KeyCode.E;   // 拾取/选择
        [SerializeField] private KeyCode _inspectKey = KeyCode.R;    // 检视（听声音/看信息）
        [SerializeField] private float _fallbackRadius = 1.5f;

        private readonly Collider2D[] _hits = new Collider2D[16];
        private IInteractable _current;
        private float _radius;

        /// <summary>是否允许交互（过渡/暂停时由 GameManager 关闭）。</summary>
        public bool Interactable { get; set; }

        public void Init(Transform player, GlobalConfig config)
        {
            if (player != null)
            {
                _player = player;
            }

            _radius = config != null ? config.InteractRadius : _fallbackRadius;
        }

        private void Update()
        {
            if (!Interactable || _player == null)
            {
                ClearHighlight();
                return;
            }

            var nearest = FindNearest();
            if (nearest != _current)
            {
                ClearHighlight();
                _current = nearest;
                if (_current != null)
                {
                    _current.SetHighlight(true);
                }
            }

            if (_current != null && Input.GetKeyDown(_interactKey))
            {
                _current.Interact();
            }

            if (_current != null && Input.GetKeyDown(_inspectKey))
            {
                _current.Inspect();
            }
        }

        private IInteractable FindNearest()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                return null;
            }

            var phase = gm.CurrentPhase;
            float r = _radius > 0f ? _radius : _fallbackRadius;
            int count = Physics2D.OverlapCircleNonAlloc(_player.position, r, _hits, _interactMask);

            IInteractable best = null;
            float bestSqr = float.MaxValue;
            Vector2 origin = _player.position;

            for (int i = 0; i < count; i++)
            {
                // TryGetComponent：非分配的组件查找，避免 GetComponent<接口> 的托管分配（热路径 GC 规范）。
                // 完整的 IInteractable 注册表池化留作后续优化（见 project-plan 已记的交互池化项）。
                if (!_hits[i].TryGetComponent<IInteractable>(out var interactable) || !interactable.CanInteract(phase))
                {
                    continue;
                }

                // 取 Collider 所属 Transform 来计算距离
                float sqr = ((Vector2)_hits[i].transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = interactable;
                }
            }

            return best;
        }

        private void ClearHighlight()
        {
            if (_current != null)
            {
                _current.SetHighlight(false);
                _current = null;
            }
        }
    }
}
