// ------------------------------------------------------------
// ItemCsvModel.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System.Collections.Generic;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>物品 CSV 一行（一个物品定义）。特征以 dimKey→valueId 存，支持任意维度。</summary>
    public sealed class ItemCsvRow
    {
        public string ItemId;
        public string DisplayName;

        /// <summary>维度名（Color/Shape/…）→ 该维取值 int（0 = None）。仅含表头出现的特征维度。</summary>
        public readonly Dictionary<string, int> FeatureValues = new Dictionary<string, int>();

        public string SpritePath;
        public float ScaleX = 1f;
        public float ScaleY = 1f;
        public bool ColliderCircle;
        public string Note;
        public int SourceLine;
    }

    /// <summary>解析 + 校验后的物品目录整表数据。</summary>
    public sealed class ItemCsvData
    {
        /// <summary>表头中识别到的特征维度列（按出现顺序，维度名）。</summary>
        public readonly List<string> FeatureDimKeys = new List<string>();

        public readonly List<ItemCsvRow> Rows = new List<ItemCsvRow>();
    }

    /// <summary>物品 CSV 解析/校验失败，消息含行号。</summary>
    public sealed class ItemCsvException : System.Exception
    {
        public ItemCsvException(string message) : base(message) { }
    }
}
