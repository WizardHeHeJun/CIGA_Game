// ------------------------------------------------------------
// CjkFontRepair.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using TMPro;
using UnityEngine;

namespace Ciga.AnchorHorror
{
    /// <summary>
    /// 运行时修复 CJK 动态字体图集。
    /// TMP 动态字体（AnchorCJK SDF，挂在 TMP 全局 fallback）的运行时 atlas 纹理在域重载后
    /// 可能变成野引用，抛 MissingReferenceException: "m_AtlasTextures doesn't exist anymore"，
    /// 导致此后**新请求的中文字形无法渲染**（表现为 TMP 中文文本空白，如记忆面板/门提示）。
    /// 播放开始时清空 默认字体 + fallback 字体 的动态数据，强制下次渲染时重建干净图集。
    /// 纯运行时，无编辑器依赖。
    /// </summary>
    public static class CjkFontRepair
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Repair()
        {
            // 默认字体本身（其 fallback 链上挂了 CJK）
            var def = TMP_Settings.defaultFontAsset;
            if (def != null)
            {
                ClearSafe(def);
                if (def.fallbackFontAssetTable != null)
                {
                    for (int i = 0; i < def.fallbackFontAssetTable.Count; i++)
                    {
                        ClearSafe(def.fallbackFontAssetTable[i]);
                    }
                }
            }

            // 全局 fallback 列表（AnchorCJK SDF 就挂在这里）
            var fallbacks = TMP_Settings.fallbackFontAssets;
            if (fallbacks != null)
            {
                for (int i = 0; i < fallbacks.Count; i++)
                {
                    ClearSafe(fallbacks[i]);
                }
            }
        }

        /// <summary>清空一个字体资产的动态数据（重建图集），忽略空引用与异常。</summary>
        private static void ClearSafe(TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            try
            {
                font.ClearFontAssetData(true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AnchorHorror] CjkFontRepair 清理字体 '{font.name}' 失败：{e.Message}");
            }
        }
    }
}
