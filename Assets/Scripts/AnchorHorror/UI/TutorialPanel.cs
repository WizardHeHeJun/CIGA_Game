// ------------------------------------------------------------
// TutorialPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
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
        [SerializeField] private GameObject _root;
        [SerializeField] private Image _image;   // 教程图（占位，可替换真美术）
        [SerializeField] private TMP_Text _prompt;

        private GameManager _gm;
        private bool _showing;

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
            _root.SetActive(true);
            if (_prompt != null)
            {
                _prompt.text = "按任意键继续";
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

            if (Input.anyKeyDown)
            {
                _showing = false;
                if (_root != null)
                {
                    _root.SetActive(false);
                }

                if (_gm != null)
                {
                    _gm.BeginAfterTutorial();
                }
            }
        }
    }
}
