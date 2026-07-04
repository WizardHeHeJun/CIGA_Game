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
    /// 匹配反馈：命中 → 物品微光 + 每个命中特征浮出关键词文本 + 程序化"暖音"确认音；不匹配 → 物品红闪。
    /// 只订阅 EventBus（ItemMatched / ItemMismatched），不被逻辑依赖。音源缺省自动自建，无需外部接线。
    /// </summary>
    public class MatchFeedback : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Color _glowColor = new Color(1f, 0.95f, 0.6f, 1f);
        [SerializeField] private Color _mismatchColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private float _glowTime = 0.5f;

        [Header("匹配暖音（AudioSource 可空，缺省自建；程序化生成柔和确认音）")]
        [SerializeField] private AudioSource _matchSource;
        [Range(0f, 1f)]
        [SerializeField] private float _matchVolume = 0.45f;
        [Tooltip("暖音基频（Hz）；越高越清脆、越低越沉。")]
        [SerializeField] private float _matchToneHz = 523f;

        [Header("特征音效（Sound 维度命中时播放，与暖音叠加；clip 空则不播）")]
        [SerializeField] private AudioSource _featureSoundSource;
        [Range(0f, 1f)]
        [SerializeField] private float _featureSoundVolume = 0.7f;

        private AudioClip _matchClip;

        private void Awake()
        {
            if (_matchSource == null)
            {
                _matchSource = gameObject.AddComponent<AudioSource>();
                _matchSource.playOnAwake = false;
            }

            if (_featureSoundSource == null)
            {
                _featureSoundSource = gameObject.AddComponent<AudioSource>();
                _featureSoundSource.playOnAwake = false;
            }

            _matchClip = GenerateWarmTone(_matchToneHz);
        }

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
            if (_matchSource != null && _matchClip != null)
            {
                _matchSource.PlayOneShot(_matchClip, _matchVolume);
            }

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

                // Sound 维度命中：播放该特征配置的 AudioClip（未配置则不播，暖音已作通用确认音）
                if (hits[i].Dimension == FeatureDimension.Sound && db != null && _featureSoundSource != null)
                {
                    var clip = db.GetAudioClip(hits[i]);
                    if (clip != null)
                    {
                        _featureSoundSource.PlayOneShot(clip, _featureSoundVolume);
                    }
                }
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

        /// <summary>程序化生成柔和"暖音"确认音：基频 + 柔化八度泛音，软起音 + 指数衰减，约 0.35s。</summary>
        private static AudioClip GenerateWarmTone(float hz)
        {
            const int rate = 44100;
            int length = (int)(rate * 0.35f);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float attack = Mathf.Clamp01(t / 0.012f);   // 12ms 软起音，去掉爆音
                float decay = Mathf.Exp(-t * 6.5f);
                float env = attack * decay;
                float wave = Mathf.Sin(2f * Mathf.PI * hz * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 2f * hz * t);
                data[i] = wave * env * 0.5f;
            }

            var clip = AudioClip.Create("MatchWarmTone", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
