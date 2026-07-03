// ------------------------------------------------------------
// InteractionSystem.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 2D 交互系统：每帧取玩家半径内最近的未消耗 FeatureTag 高亮，按交互键触发。
    /// InitRoom 阶段 → 收集候选；HorrorLevel 阶段 → 匹配。用 OverlapCircleNonAlloc 避免热路径分配。
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [SerializeField] private Transform _player;
        [SerializeField] private LayerMask _interactMask = ~0;
        [SerializeField] private KeyCode _interactKey = KeyCode.E;
        [SerializeField] private float _fallbackRadius = 1.5f;

        private readonly Collider2D[] _hits = new Collider2D[16];
        private FeatureTag _current;
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
                Interact(_current);
            }
        }

        private void Interact(FeatureTag item)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Anchor == null)
            {
                return;
            }

            switch (gm.CurrentPhase)
            {
                case GamePhase.InitRoom:
                    gm.Anchor.CollectCandidate(item);
                    item.Consumed = true; // 初始房间：交互过的物品也不再重复计入
                    ClearHighlight();
                    break;

                case GamePhase.HorrorLevel:
                    gm.Anchor.TryMatch(item);
                    ClearHighlight();
                    break;
            }
        }

        private FeatureTag FindNearest()
        {
            float r = _radius > 0f ? _radius : _fallbackRadius;
            int count = Physics2D.OverlapCircleNonAlloc(_player.position, r, _hits, _interactMask);

            FeatureTag best = null;
            float bestSqr = float.MaxValue;
            Vector2 origin = _player.position;

            for (int i = 0; i < count; i++)
            {
                var tag = _hits[i].GetComponent<FeatureTag>();
                if (tag == null || tag.Consumed)
                {
                    continue;
                }

                float sqr = ((Vector2)tag.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = tag;
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
