// ------------------------------------------------------------
// UIFadePanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.Collections;
using UnityEngine;

namespace Ciga.UI
{
    /// <summary>
    /// 面板整体淡入 / 淡出（改 CanvasGroup.alpha），手感对齐关卡切换过渡：
    /// SmoothStep 缓入缓出、默认 1s、走 unscaledTime（暂停/慢放不受影响）。
    /// FadeIn 先激活再淡入；FadeOut 淡出后可选失活。运行时挂载即可（自动补 CanvasGroup）。
    /// 只管 alpha 与激活，raycast 拦截由调用方按需自行管理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIFadePanel : MonoBehaviour
    {
        [SerializeField] private float _duration = 1f;   // 页面切换总耗时（比关卡切换稍缓，用户指定 1s）

        private CanvasGroup _cg;
        private Coroutine _routine;

        private void Awake()
        {
            EnsureCg();
        }

        /// <summary>设淡入淡出时长（对齐当前关卡切换配置时可传入）。</summary>
        public void SetDuration(float duration)
        {
            _duration = Mathf.Max(0f, duration);
        }

        /// <summary>立即置为指定 alpha（不动画，用于起始压到 0 再淡入）。</summary>
        public void SetInstant(float alpha)
        {
            EnsureCg();
            _cg.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>激活并从当前 alpha 淡入到 1。</summary>
        public void FadeIn()
        {
            gameObject.SetActive(true);
            EnsureCg();
            StartFade(1f, false);
        }

        /// <summary>淡出到 0，完成后按需失活；已隐藏则直接置 0。</summary>
        public void FadeOut(bool deactivate = true)
        {
            EnsureCg();
            if (!gameObject.activeInHierarchy)
            {
                _cg.alpha = 0f;
                if (deactivate)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            StartFade(0f, deactivate);
        }

        private void EnsureCg()
        {
            if (_cg == null)
            {
                _cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void StartFade(float target, bool deactivateAtEnd)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(FadeRoutine(target, deactivateAtEnd));
        }

        private IEnumerator FadeRoutine(float target, bool deactivateAtEnd)
        {
            float start = _cg.alpha;
            float t = 0f;
            while (t < _duration)
            {
                t += Time.unscaledDeltaTime;
                float k = _duration > 0f ? Mathf.Clamp01(t / _duration) : 1f;
                _cg.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, k)); // 缓入缓出，与关卡 Fade 一致
                yield return null;
            }

            _cg.alpha = target;
            _routine = null;

            if (deactivateAtEnd && target <= 0f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
