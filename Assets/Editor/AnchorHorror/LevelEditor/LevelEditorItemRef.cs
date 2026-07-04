// ------------------------------------------------------------
// LevelEditorItemRef.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
// 设计说明：预览物体需要在 WriteBack 时追溯其来源 ItemDefinition.Id。
// 使用单独的 MonoBehaviour 而非 Dictionary<GameObject,string>，原因：
//   - Unity Ctrl+D 复制时，组件随 GameObject 一起被克隆，Id 天然保留。
//   - Dictionary 在复制时无法自动同步，需要手动注册，维护成本高且易漏。
//   - 此组件定义在编辑器程序集（Ciga.AnchorHorror.EditorTools），配合
//     HideFlags.DontSaveInBuild 完全不进构建，零运行时开销。
// ------------------------------------------------------------
using UnityEngine;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 编辑器专用标记：记录预览物体来源 <see cref="ItemDefinition"/> 的 Id。
    /// 仅存在于编辑器程序集，不进运行时构建。
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class LevelEditorItemRef : MonoBehaviour
    {
        [SerializeField] private string _itemId;

        /// <summary>来源 ItemDefinition.Id，与 PlacedItem.ItemId 对应。</summary>
        public string ItemId
        {
            get => _itemId;
            set => _itemId = value;
        }
    }
}
