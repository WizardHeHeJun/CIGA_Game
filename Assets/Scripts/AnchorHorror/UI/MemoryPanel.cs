// ------------------------------------------------------------
// MemoryPanel.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 记忆面板：Tab 开关，打开时暂停（Time.timeScale=0），列出 5 个目标锚点及激活状态。
    /// 激活锚点显示金色"已锚定"。数据来自 AnchorSystem + 订阅 AnchorActivated / TargetsExtracted。
    /// </summary>
    public class MemoryPanel : MonoBehaviour
    {
        private const string GoldHex = "#FFD700";
        private const string DimHex = "#AAAAAA";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _content;
        [SerializeField] private KeyCode _toggleKey = KeyCode.Tab;

        private readonly StringBuilder _sb = new StringBuilder(256);
        private bool _open;

        private void OnEnable()
        {
            EventBus.TargetsExtracted += OnTargetsChanged;
            EventBus.AnchorActivated += OnAnchorActivated;
        }

        private void OnDisable()
        {
            EventBus.TargetsExtracted -= OnTargetsChanged;
            EventBus.AnchorActivated -= OnAnchorActivated;

            // 面板打开（timeScale=0）时被禁用/销毁，避免游戏永久暂停
            if (_open)
            {
                _open = false;
                Time.timeScale = 1f;
            }
        }

        private void Start()
        {
            if (_root == null)
            {
                Debug.LogWarning("[AnchorHorror] MemoryPanel 未接线（_root 为空）：记忆面板不可见，Tab 将不暂停游戏。" +
                                 "请运行菜单 Ciga/AnchorHorror/生成可运行装配 重建场景以接上面板。");
            }

            SetOpen(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                SetOpen(!_open);
            }
        }

        private void SetOpen(bool open)
        {
            // 接线缺失时运行时自建/复用面板，摆脱生成器接线依赖（用户反馈：面板弹出但内容空白）。
            EnsureUI();

            // 仍拿不到 _root（极端异常）时判空降级：不接管 timeScale，避免"看不见面板还冻住游戏"。
            if (_root == null)
            {
                _open = false;
                return;
            }

            _open = open;
            _root.SetActive(open);
            Time.timeScale = open ? 0f : 1f;
            if (open)
            {
                Refresh();
            }
        }

        /// <summary>
        /// 确保 _root / _content 可用：优先用生成器接线；_content 缺失则复用 _root 子级 TMP，
        /// 再缺则运行时自建 Canvas + 面板 + 文本（用户反馈 Tab 面板空白：_root 接了但 _content 没接）。
        /// </summary>
        private void EnsureUI()
        {
            if (_content != null)
            {
                return;
            }

            // 1) _root 已接线但 _content 没接 → 复用其子级现成的 TMP 文本
            if (_root != null)
            {
                _content = _root.GetComponentInChildren<TMP_Text>(true);
                if (_content != null)
                {
                    return;
                }
            }

            // 2) 找/建一个 Overlay Canvas
            Canvas canvas = _root != null ? _root.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                var canvasGo = new GameObject("MemoryPanelCanvas(runtime)");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            // 3) 缺 _root 则自建居中暗色面板
            if (_root == null)
            {
                var rootGo = new GameObject("MemoryPanelRoot(runtime)", typeof(RectTransform));
                rootGo.transform.SetParent(canvas.transform, false);
                var rt = (RectTransform)rootGo.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(760f, 620f);
                var bg = rootGo.AddComponent<Image>();
                bg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
                bg.raycastTarget = false;
                _root = rootGo;
            }

            // 4) 自建内容文本，铺满 _root（留边距）
            var contentGo = new GameObject("MemoryContent(runtime)", typeof(RectTransform));
            contentGo.transform.SetParent(_root.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(40f, 40f);
            contentRt.offsetMax = new Vector2(-40f, -40f);
            var tmp = contentGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 32f;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = Color.white;
            tmp.richText = true;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            _content = tmp;
        }

        private void OnTargetsChanged(IReadOnlyList<AnchorTarget> targets)
        {
            if (_open)
            {
                Refresh();
            }
        }

        private void OnAnchorActivated(AnchorTarget anchor)
        {
            if (_open)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_content == null)
            {
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null || gm.Anchor == null)
            {
                _content.text = string.Empty;
                return;
            }

            var targets = gm.Anchor.Targets;
            var db = gm.Database;
            var backpack = gm.Backpack;

            _sb.Clear();
            _sb.AppendLine("<b>记忆锚点</b>");
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                string name = db != null ? db.GetDisplayName(t.Feature) : t.Feature.ToString();
                // 新模型：靠背包覆盖判定（Inventory.Covers），不靠 AnchorTarget.IsActivated（SC-5，陷阱 10）
                bool satisfied = backpack != null && backpack.Covers(t);
                if (satisfied)
                {
                    _sb.AppendLine($"<color={GoldHex}>{name}　已满足</color>");
                }
                else
                {
                    _sb.AppendLine($"<color={DimHex}>{name}　未满足</color>");
                }
            }

            // 背包内容（用户要求：Tab 能看已收集的物品及其特征）
            _sb.AppendLine();
            int count = backpack != null ? backpack.Count : 0;
            int cap = backpack != null ? backpack.Capacity : 0;
            _sb.AppendLine($"<b>背包 {count}/{cap}</b>");
            if (backpack != null)
            {
                for (int i = 0; i < backpack.Items.Count; i++)
                {
                    var bi = backpack.Items[i];
                    _sb.Append("  · ");
                    bool any = false;
                    for (int f = 0; f < bi.Features.Count; f++)
                    {
                        if (bi.Features[f].IsNone)
                        {
                            continue;
                        }

                        string fname = db != null ? db.GetDisplayName(bi.Features[f]) : bi.Features[f].ToString();
                        if (any)
                        {
                            _sb.Append(" / ");
                        }

                        _sb.Append(fname);
                        any = true;
                    }

                    if (!any)
                    {
                        _sb.Append("(无特征)");
                    }

                    _sb.AppendLine();
                }
            }

            _content.text = _sb.ToString();
        }
    }
}
