// ------------------------------------------------------------
// TutorialPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using Ciga.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 教程图盖屏（迭代B，SC-B1）：主菜单进入 Bootstrap 后由 GameManager.Start 调 Show() 展示，
    /// 任意键 → 隐藏并回调 GameManager.BeginAfterTutorial() 进关卡1。
    /// 缺美术资源用占位灰图（生成器接线）。判空降级：未接线时不阻塞、直接进关卡1。
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        private const float TutorialLockSeconds = 3f;   // 进教程后忽略按键的秒数（给时间看图、防误触）

        [SerializeField] private GameObject _root;
        [SerializeField] private Image _image;   // 教程图（占位，可替换真美术）
        [SerializeField] private TMP_Text _prompt;

        private GameManager _gm;
        private bool _showing;
        private float _unlockTime;
        private bool _promptShown;
        private UITextBreathe _promptBreathe;
        private UIFadePanel _fade;

        /// <summary>展示教程图并接管「任意键继续」。由 GameManager.Start 调用。</summary>
        public void Show(GameManager gm)
        {
            _gm = gm;

            if (_root == null)
            {
                // 未接线降级：不阻塞流程，直接进关卡1。
                Debug.LogWarning("[AnchorHorror] TutorialPanel 未接线（_root 为空）：跳过教程直接进关卡1。");
                _showing = false;
                if (gm != null)
                {
                    gm.BeginAfterTutorial();
                }

                return;
            }

            _showing = true;

            // 页面整体淡入（手感对齐关卡切换，不再硬切）
            if (_fade == null)
            {
                _fade = _root.GetComponent<UIFadePanel>() ?? _root.AddComponent<UIFadePanel>();
            }

            _fade.SetInstant(0f);
            _fade.FadeIn();

            // 前 TutorialLockSeconds 秒忽略按键；期间提示隐藏，解锁再淡入 + 呼吸（避免一进来太直接）
            _unlockTime = Time.unscaledTime + TutorialLockSeconds;
            _promptShown = false;
            if (_prompt != null)
            {
                _prompt.text = "按任意键继续";
                if (_promptBreathe == null)
                {
                    _promptBreathe = _prompt.GetComponent<UITextBreathe>()
                                     ?? _prompt.gameObject.AddComponent<UITextBreathe>();
                }

                _promptBreathe.Stop(); // 锁定期先隐藏
            }
        }

        private void Start()
        {
            // 未被 Show 接管时默认隐藏（避免占位盖屏常驻）。
            if (!_showing && _root != null)
            {
                _root.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_showing)
            {
                return;
            }

            // 锁定期：忽略一切按键
            if (Time.unscaledTime < _unlockTime)
            {
                return;
            }

            // 解锁瞬间：提示淡入 + 呼吸
            if (!_promptShown)
            {
                _promptShown = true;
                if (_promptBreathe != null)
                {
                    _promptBreathe.Play();
                }
            }

            if (Input.anyKeyDown)
            {
                _showing = false;

                // 先进关卡1：EnterInitRoom 会立即压黑并从黑淡入，教程页背后不会露白
                if (_gm != null)
                {
                    _gm.BeginAfterTutorial();
                }

                // 教程页并行淡出，与关卡1的黑幕淡入交叉衔接（不再硬切）
                if (_fade != null)
                {
                    _fade.FadeOut(true);
                }
                else if (_root != null)
                {
                    _root.SetActive(false);
                }
            }
        }
    }
}
