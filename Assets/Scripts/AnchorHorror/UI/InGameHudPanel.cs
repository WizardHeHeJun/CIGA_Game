// ------------------------------------------------------------
// InGameHudPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 关卡2（HorrorLevel）常驻画面 HUD：边框（Frame）+ 顶部命中数
    /// （No Collect 底图 + Collected1..5 叠加图标）。命中数 = 背包覆盖的锚点数
    /// （Inventory.Covers，不用 AnchorTarget.IsActivated，陷阱10），命中 k 个则点亮前 k 个图标。
    /// 同时承载二关快捷按钮：设置 / 回主菜单 / 重开 / 退出。仅 HorrorLevel 相位显示，其余相位隐藏。
    /// </summary>
    public class InGameHudPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _root;

        [Tooltip("Collected1..5 叠加图标（各为一颗填充菱形）；命中 k 个则显示前 k 个。")]
        [SerializeField] private Image[] _collectedIcons;

        [Header("二关快捷按钮")]
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _homeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _quitButton;

        [Header("设置弹层")]
        [SerializeField] private GameObject _settingsRoot;
        [SerializeField] private Button _settingsResumeButton;
        [SerializeField] private Button _settingsRestartButton;
        [SerializeField] private Button _settingsHomeButton;
        [SerializeField] private Button _settingsQuitButton;

        [Header("设置弹层按键")]
        [SerializeField] private KeyCode _settingsKey = KeyCode.Escape;
        [SerializeField] private KeyCode _selectPreviousKey = KeyCode.A;
        [SerializeField] private KeyCode _selectNextKey = KeyCode.D;

        private readonly List<Button> _settingsSelectionButtons = new List<Button>(3);
        private int _lastHit = -1;
        private int _settingsSelectionIndex;
        private bool _settingsOpen;

        private void OnEnable()
        {
            EventBus.PhaseChanged += OnPhaseChanged;
            EventBus.TargetsExtracted += OnTargetsExtracted;
            EventBus.BackpackChanged += OnBackpackChanged;
            EventBus.ItemMatched += OnItemMatched;
            EventBus.ItemMismatched += OnItemMismatched;
            EventBus.AllAnchorsActivated += OnAllActivated;

            AddButtonListeners();
        }

        private void OnDisable()
        {
            EventBus.PhaseChanged -= OnPhaseChanged;
            EventBus.TargetsExtracted -= OnTargetsExtracted;
            EventBus.BackpackChanged -= OnBackpackChanged;
            EventBus.ItemMatched -= OnItemMatched;
            EventBus.ItemMismatched -= OnItemMismatched;
            EventBus.AllAnchorsActivated -= OnAllActivated;

            RemoveButtonListeners();
            CloseSettings();
        }

        private void Start()
        {
            if (_root == null)
            {
                Debug.LogWarning("[AnchorHorror] InGameHudPanel 未接线（_root 为空）：边框/命中数 HUD 不可见。" +
                                 "请运行菜单 Ciga/AnchorHorror/生成可运行装配 重建场景。");
            }

            CacheSettingsSelectionButtons();
            SetVisible(false);
            SetSettingsVisible(false);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            bool inHorrorLevel = gm != null && gm.CurrentPhase == GamePhase.HorrorLevel;
            if (!inHorrorLevel)
            {
                return;
            }

            if (Input.GetKeyDown(_settingsKey))
            {
                ToggleSettings();
                return;
            }

            if (!_settingsOpen)
            {
                return;
            }

            if (Input.GetKeyDown(_selectPreviousKey) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveSettingsSelection(-1);
            }
            else if (Input.GetKeyDown(_selectNextKey) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveSettingsSelection(1);
            }
        }

        private void OnPhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            bool show = newPhase == GamePhase.HorrorLevel;
            SetVisible(show);
            if (show)
            {
                _lastHit = -1; // 相位切入时强制刷新
                RefreshHit();
            }
            else
            {
                CloseSettings();
            }
        }

        private void OnTargetsExtracted(IReadOnlyList<AnchorTarget> targets)
        {
            _lastHit = -1;
            RefreshHit();
        }

        private void OnBackpackChanged(Inventory backpack)
        {
            RefreshHit();
        }

        private void OnItemMatched(FeatureTag item, IReadOnlyList<FeatureUnit> hits)
        {
            RefreshHit();
        }

        private void OnItemMismatched(FeatureTag item)
        {
            RefreshHit();
        }

        private void OnAllActivated()
        {
            RefreshHit();
        }

        /// <summary>命中数 = 背包覆盖的锚点数；仅在数值变化时切换 Collected 图标显隐。</summary>
        private void RefreshHit()
        {
            if (_collectedIcons == null)
            {
                return;
            }

            var gm = GameManager.Instance;
            int hit = 0;
            if (gm != null && gm.Anchor != null)
            {
                var targets = gm.Anchor.Targets;
                var backpack = gm.Backpack;
                if (targets != null && backpack != null)
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        if (backpack.Covers(targets[i]))
                        {
                            hit++;
                        }
                    }
                }
            }

            if (hit == _lastHit)
            {
                return;
            }

            _lastHit = hit;
            for (int i = 0; i < _collectedIcons.Length; i++)
            {
                if (_collectedIcons[i] != null)
                {
                    _collectedIcons[i].enabled = i < hit;
                }
            }
        }

        private void AddButtonListeners()
        {
            if (_settingsButton != null)
            {
                _settingsButton.onClick.AddListener(ToggleSettings);
            }

            if (_homeButton != null)
            {
                _homeButton.onClick.AddListener(ReturnHome);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(RestartGame);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.AddListener(QuitGame);
            }

            if (_settingsResumeButton != null)
            {
                _settingsResumeButton.onClick.AddListener(CloseSettings);
            }

            if (_settingsRestartButton != null)
            {
                _settingsRestartButton.onClick.AddListener(RestartGame);
            }

            if (_settingsHomeButton != null)
            {
                _settingsHomeButton.onClick.AddListener(ReturnHome);
            }

            if (_settingsQuitButton != null)
            {
                _settingsQuitButton.onClick.AddListener(QuitGame);
            }
        }

        private void RemoveButtonListeners()
        {
            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveListener(ToggleSettings);
            }

            if (_homeButton != null)
            {
                _homeButton.onClick.RemoveListener(ReturnHome);
            }

            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(RestartGame);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.RemoveListener(QuitGame);
            }

            if (_settingsResumeButton != null)
            {
                _settingsResumeButton.onClick.RemoveListener(CloseSettings);
            }

            if (_settingsRestartButton != null)
            {
                _settingsRestartButton.onClick.RemoveListener(RestartGame);
            }

            if (_settingsHomeButton != null)
            {
                _settingsHomeButton.onClick.RemoveListener(ReturnHome);
            }

            if (_settingsQuitButton != null)
            {
                _settingsQuitButton.onClick.RemoveListener(QuitGame);
            }
        }

        private void ToggleSettings()
        {
            if (_settingsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }

        private void OpenSettings()
        {
            if (_settingsRoot == null || GameManager.Instance == null || GameManager.Instance.CurrentPhase != GamePhase.HorrorLevel)
            {
                return;
            }

            _settingsOpen = true;
            Time.timeScale = 0f;
            SetSettingsVisible(true);
            CacheSettingsSelectionButtons();
            SelectSettingsButton(0);
        }

        private void CloseSettings()
        {
            if (_settingsOpen)
            {
                Time.timeScale = 1f;
            }

            _settingsOpen = false;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            SetSettingsVisible(false);
        }

        private void ReturnHome()
        {
            CloseSettings();
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.ReturnToMainMenu();
            }
        }

        private void RestartGame()
        {
            CloseSettings();
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.RestartGame();
            }
        }

        private void QuitGame()
        {
            CloseSettings();
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.QuitGame();
            }
        }

        private void CacheSettingsSelectionButtons()
        {
            _settingsSelectionButtons.Clear();
            AddSelectionButton(_settingsHomeButton);
            AddSelectionButton(_settingsRestartButton);
            AddSelectionButton(_settingsQuitButton);
            if (_settingsSelectionIndex >= _settingsSelectionButtons.Count)
            {
                _settingsSelectionIndex = 0;
            }
        }

        private void AddSelectionButton(Button button)
        {
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
            {
                _settingsSelectionButtons.Add(button);
            }
        }

        private void MoveSettingsSelection(int direction)
        {
            CacheSettingsSelectionButtons();
            if (_settingsSelectionButtons.Count == 0)
            {
                return;
            }

            SyncSettingsSelectionFromEventSystem();
            int count = _settingsSelectionButtons.Count;
            SelectSettingsButton((_settingsSelectionIndex + direction + count) % count);
        }

        private void SyncSettingsSelectionFromEventSystem()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            var selected = EventSystem.current.currentSelectedGameObject;
            for (int i = 0; i < _settingsSelectionButtons.Count; i++)
            {
                if (_settingsSelectionButtons[i] != null && _settingsSelectionButtons[i].gameObject == selected)
                {
                    _settingsSelectionIndex = i;
                    return;
                }
            }
        }

        private void SelectSettingsButton(int index)
        {
            if (_settingsSelectionButtons.Count == 0 || EventSystem.current == null)
            {
                return;
            }

            _settingsSelectionIndex = Mathf.Clamp(index, 0, _settingsSelectionButtons.Count - 1);
            EventSystem.current.SetSelectedGameObject(_settingsSelectionButtons[_settingsSelectionIndex].gameObject);
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }

        private void SetSettingsVisible(bool visible)
        {
            if (_settingsRoot != null)
            {
                if (visible)
                {
                    _settingsRoot.transform.SetAsLastSibling();
                }

                _settingsRoot.SetActive(visible);
            }
        }
    }
}
