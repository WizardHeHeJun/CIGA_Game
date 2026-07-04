// ------------------------------------------------------------
// DebugHUD.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Text;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 联调之眼：屏幕常驻显示 Phase / San / 背包 / 倒计时 / 锚点覆盖。用 IMGUI（仅调试，不进正式表现）。
    /// M1 就上，避免逻辑对不对全靠猜。可用 _visible 关闭。
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [SerializeField] private SanitySystem _sanity;
        [SerializeField] private bool _visible = true;

        private readonly StringBuilder _sb = new StringBuilder(256);
        private GUIStyle _style;

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null)
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
                _style.normal.textColor = Color.white;
            }

            _sb.Clear();
            _sb.AppendLine($"Phase : {gm.CurrentPhase}");
            if (_sanity != null)
            {
                _sb.AppendLine($"San   : {_sanity.Current:0.0}/{_sanity.Max:0} [{_sanity.State}]");
            }

            var bp = gm.Backpack;
            if (bp != null)
            {
                string locked = gm.SelectionLocked ? " [锁定]" : string.Empty;
                _sb.AppendLine($"背包  : {bp.Count}/{bp.Capacity}{locked}");
            }

            if (gm.CurrentPhase == GamePhase.HorrorLevel)
            {
                _sb.AppendLine($"倒计时: {gm.RemainingTime:0.0}s");
            }

            if (gm.Anchor != null)
            {
                var targets = gm.Anchor.Targets;
                for (int i = 0; i < targets.Count; i++)
                {
                    var t = targets[i];
                    bool covered = bp != null && bp.Covers(t); // 新模型：满足态看背包覆盖，非 AnchorTarget.Hit 累计
                    string flag = covered ? "√" : " ";
                    _sb.AppendLine($"  [{flag}] {t.Feature}");
                }
            }

            GUI.Box(new Rect(10, 10, 320, 24 * (5 + (gm.Anchor != null ? gm.Anchor.Targets.Count : 0))), string.Empty);
            GUI.Label(new Rect(18, 14, 320, 400), _sb.ToString(), _style);
        }
    }
}
