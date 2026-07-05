// ------------------------------------------------------------
// UIPressScaleFeedback.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ciga.UI
{
    /// <summary>
    /// UI 按钮悬浮放大 / 按下缩小 的缩放反馈：靠指针事件平滑改变 transform.localScale。
    /// 全屏 alpha 命中按钮请把 RectTransform.pivot 设到笔触中心（见 OpaqueCenterNormalized），
    /// 缩放才会「就地」而非绕屏幕中心漂移。Button.transition 建议设 None，由本组件接管反馈。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPressScaleFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float _hoverScale = 1.07f;   // 悬浮放大倍率
        [SerializeField] private float _pressScale = 0.93f;   // 按下缩小倍率
        [SerializeField] private float _speed = 14f;          // 平滑速度（越大越快贴合目标）

        private Vector3 _baseScale = Vector3.one;
        private float _target = 1f;
        private bool _hovering;
        private bool _pressing;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            _hovering = false;
            _pressing = false;
            _target = 1f;
            transform.localScale = _baseScale;
        }

        private void OnDisable()
        {
            // 复位，避免残留缩放（面板隐藏/重开时）
            transform.localScale = _baseScale;
        }

        private void Update()
        {
            var goal = _baseScale * _target;
            if ((transform.localScale - goal).sqrMagnitude < 1e-8f)
            {
                if (transform.localScale != goal)
                {
                    transform.localScale = goal;
                }

                return;
            }

            // 帧率无关的指数平滑；用 unscaledDeltaTime 避免受 timeScale 影响
            float k = 1f - Mathf.Exp(-_speed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, goal, k);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovering = true;
            UpdateTarget();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            UpdateTarget();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressing = true;
            UpdateTarget();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressing = false;
            UpdateTarget();
        }

        private void UpdateTarget()
        {
            // 按下优先于悬浮：按住时缩小，仅悬浮时放大，都没有则复原
            _target = _pressing ? _pressScale : (_hovering ? _hoverScale : 1f);
        }

        /// <summary>
        /// 扫描 sprite 纹理非透明像素包围盒中心（归一化 0..1，可作 RectTransform.pivot）。
        /// 纹理不可读则回退 (0.5,0.5)。GetPixels32 行 0 = 底部，与 pivot 的 y 向上一致。
        /// </summary>
        public static Vector2 OpaqueCenterNormalized(Sprite sprite, float alphaThreshold = 0.1f)
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable)
            {
                return new Vector2(0.5f, 0.5f);
            }

            Color32[] px;
            try
            {
                px = sprite.texture.GetPixels32();
            }
            catch (UnityException)
            {
                return new Vector2(0.5f, 0.5f);
            }

            int w = sprite.texture.width;
            int h = sprite.texture.height;
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alphaThreshold * 255f), 0, 255);
            int minX = w, minY = h, maxX = -1, maxY = -1;
            const int step = 2; // 隔像素采样，够算中心又省一半扫描

            for (int y = 0; y < h; y += step)
            {
                int row = y * w;
                for (int x = 0; x < w; x += step)
                {
                    if (px[row + x].a > a)
                    {
                        if (x < minX) { minX = x; }
                        if (x > maxX) { maxX = x; }
                        if (y < minY) { minY = y; }
                        if (y > maxY) { maxY = y; }
                    }
                }
            }

            if (maxX < 0)
            {
                return new Vector2(0.5f, 0.5f);
            }

            return new Vector2((minX + maxX) * 0.5f / w, (minY + maxY) * 0.5f / h);
        }
    }
}
