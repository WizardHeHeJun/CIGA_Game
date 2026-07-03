// ------------------------------------------------------------
// MatchFeedback.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 匹配反馈：命中 → 物品微光 + 每个命中特征浮出关键词文本；不匹配 → 物品红闪。
    /// 只订阅 EventBus（ItemMatched / ItemMismatched），不被逻辑依赖。
    /// </summary>
    public class MatchFeedback : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Color _glowColor = new Color(1f, 0.95f, 0.6f, 1f);
        [SerializeField] private Color _mismatchColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private float _glowTime = 0.5f;

        private void OnEnable()
        {
            EventBus.ItemMatched += OnMatched;
            EventBus.ItemMismatched += OnMismatched;
        }

        private void OnDisable()
        {
            EventBus.ItemMatched -= OnMatched;
            EventBus.ItemMismatched -= OnMismatched;
            StopAllCoroutines();
        }

        private void OnMatched(FeatureTag item, IReadOnlyList<FeatureUnit> hits)
        {
            if (item != null)
            {
                var sr = item.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    StartCoroutine(Glow(sr, _glowColor));
                }
            }

            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            var origin = item != null ? item.transform.position : transform.position;
            float offset = 0.6f;
            for (int i = 0; i < hits.Count; i++)
            {
                string label = db != null ? db.GetDisplayName(hits[i]) : hits[i].ToString();
                Color color = db != null ? db.GetKeywordColor(hits[i]) : Color.white;
                SpawnText(label, color, origin + Vector3.up * offset);
                offset += 0.5f;
            }
        }

        private void OnMismatched(FeatureTag item)
        {
            if (item == null)
            {
                return;
            }

            var sr = item.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                StartCoroutine(Glow(sr, _mismatchColor));
            }
        }

        private void SpawnText(string text, Color color, Vector3 position)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = position;
            var ft = go.AddComponent<FloatingText>();
            ft.Init(text, color, _font);
        }

        private IEnumerator Glow(SpriteRenderer sr, Color glow)
        {
            Color baseColor = sr.color;
            Vector3 baseScale = sr.transform.localScale;
            float t = 0f;
            while (t < _glowTime)
            {
                if (sr == null)
                {
                    yield break; // 物品所在关卡场景可能已卸载/销毁，避免 MissingReferenceException
                }

                t += Time.deltaTime;
                float k = 1f - (t / _glowTime);
                sr.color = Color.Lerp(baseColor, glow, k);
                sr.transform.localScale = baseScale * (1f + 0.3f * k);
                yield return null;
            }

            if (sr == null)
            {
                yield break;
            }

            sr.color = baseColor;
            sr.transform.localScale = baseScale;
        }
    }
}
