// ------------------------------------------------------------
// FloatingText.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 世界空间浮字：向上飘 + 淡出 + 自毁。由 MatchFeedback 在命中时生成。用 TextMeshPro(3D)，不需 Canvas。
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] private float _riseSpeed = 1.2f;
        [SerializeField] private float _lifetime = 1.2f;

        private TextMeshPro _tmp;
        private float _elapsed;

        /// <summary>初始化文本 / 颜色 / 字体（生成后立即调用）。</summary>
        public void Init(string text, Color color, TMP_FontAsset font)
        {
            _tmp = GetComponent<TextMeshPro>();
            if (_tmp == null)
            {
                _tmp = gameObject.AddComponent<TextMeshPro>();
            }

            if (font != null)
            {
                _tmp.font = font;
            }

            _tmp.text = text;
            _tmp.color = color;
            _tmp.fontSize = 4f;
            _tmp.alignment = TextAlignmentOptions.Center;

            var mr = _tmp.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = "Default";
                mr.sortingOrder = 1500; // 高于 San 压暗遮罩(1000)、低于过渡黑屏(2000)，低 San 时浮字仍可读
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);

            if (_tmp != null)
            {
                var c = _tmp.color;
                c.a = 1f - Mathf.Clamp01(_elapsed / _lifetime);
                _tmp.color = c;
            }

            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
