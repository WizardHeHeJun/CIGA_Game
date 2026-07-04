// ------------------------------------------------------------
// FeatureCsvModel.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>CSV @dim 行：一个维度声明。</summary>
    public sealed class FeatureDimDecl
    {
        public int Id;
        public string Key;
        public int SourceLine;
    }

    /// <summary>CSV @val 行：一个维度取值。</summary>
    public sealed class FeatureValRow
    {
        public int DimId;
        public string DimKey;
        public int ValueId;
        public string EnumMember;
        public string DisplayNameZh;
        public string ColorHex;
        public string AudioGuid;
        public string Note;
        public int SourceLine;
    }

    /// <summary>解析 + 校验后的整表数据。</summary>
    public sealed class FeatureCsvData
    {
        public readonly List<FeatureDimDecl> Dims = new List<FeatureDimDecl>();
        public readonly List<FeatureValRow> Values = new List<FeatureValRow>();

        /// <summary>某维度的取值，按 valueId 升序（确定性输出用）。</summary>
        public List<FeatureValRow> ValuesOf(int dimId)
        {
            var result = new List<FeatureValRow>();
            for (int i = 0; i < Values.Count; i++)
            {
                if (Values[i].DimId == dimId)
                {
                    result.Add(Values[i]);
                }
            }

            result.Sort((a, b) => a.ValueId.CompareTo(b.ValueId));
            return result;
        }

        /// <summary>维度声明，按 dimId 升序。</summary>
        public List<FeatureDimDecl> SortedDims()
        {
            var result = new List<FeatureDimDecl>(Dims);
            result.Sort((a, b) => a.Id.CompareTo(b.Id));
            return result;
        }
    }

    /// <summary>解析/校验失败，消息含行号。</summary>
    public sealed class FeatureCsvException : System.Exception
    {
        public FeatureCsvException(string message) : base(message) { }
    }
}
