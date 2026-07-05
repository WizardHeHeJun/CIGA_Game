// ------------------------------------------------------------
// FeatureCsvParser.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 解析 + 强校验 AnchorFeatures.csv。任何违规抛 FeatureCsvException（含行号），调用方中止不落盘。
    /// 校验：rowType 合法 / 维度已声明且唯一 / valueId ∈ [1,255] 且每维唯一 / 禁显式 0 /
    ///       enumMember 是合法 C# 标识符且每维唯一 / displayNameZh 非空 / colorHex 可解析 / audioGuid 若非空可解析。
    /// </summary>
    public static class FeatureCsvParser
    {
        public static FeatureCsvData Parse(string csvText)
        {
            var data = new FeatureCsvData();
            if (string.IsNullOrEmpty(csvText))
            {
                throw new FeatureCsvException("CSV 内容为空。");
            }

            var lines = csvText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                string raw = lines[i].Trim();
                if (raw.Length == 0 || raw.StartsWith("#"))
                {
                    continue;
                }

                var cols = SplitRow(raw);
                string rowType = cols[0].Trim();

                if (rowType == "@dim")
                {
                    ParseDim(data, cols, lineNo);
                }
                else if (rowType == "@val")
                {
                    ParseVal(data, cols, lineNo);
                }
                else
                {
                    throw new FeatureCsvException($"第 {lineNo} 行：未知 rowType '{rowType}'（应为 @dim / @val / #）。");
                }
            }

            if (data.Dims.Count == 0)
            {
                throw new FeatureCsvException("未声明任何维度（缺 @dim 行）。");
            }

            return data;
        }

        private static void ParseDim(FeatureCsvData data, string[] cols, int lineNo)
        {
            if (!int.TryParse(cols[1].Trim(), out int dimId))
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @dim：dimId '{cols[1]}' 不是整数。");
            }

            string dimKey = cols[2].Trim();
            if (!IsValidIdentifier(dimKey))
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @dim：dimKey '{dimKey}' 不是合法 C# 标识符。");
            }

            for (int i = 0; i < data.Dims.Count; i++)
            {
                if (data.Dims[i].Id == dimId)
                {
                    throw new FeatureCsvException($"第 {lineNo} 行 @dim：dimId {dimId} 与第 {data.Dims[i].SourceLine} 行重复。");
                }

                if (data.Dims[i].Key == dimKey)
                {
                    throw new FeatureCsvException($"第 {lineNo} 行 @dim：dimKey '{dimKey}' 与第 {data.Dims[i].SourceLine} 行重复。");
                }
            }

            data.Dims.Add(new FeatureDimDecl { Id = dimId, Key = dimKey, SourceLine = lineNo });
        }

        private static void ParseVal(FeatureCsvData data, string[] cols, int lineNo)
        {
            if (!int.TryParse(cols[1].Trim(), out int dimId))
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：dimId '{cols[1]}' 不是整数。");
            }

            string dimKey = cols[2].Trim();
            FeatureDimDecl dim = null;
            for (int i = 0; i < data.Dims.Count; i++)
            {
                if (data.Dims[i].Id == dimId)
                {
                    dim = data.Dims[i];
                    break;
                }
            }

            if (dim == null)
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：维度 dimId {dimId} 未在任何 @dim 行声明。");
            }

            if (dim.Key != dimKey)
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：dimKey '{dimKey}' 与 dimId {dimId} 声明的 '{dim.Key}' 不符。");
            }

            if (!int.TryParse(cols[3].Trim(), out int valueId))
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：valueId '{cols[3]}' 不是整数。");
            }

            if (valueId == 0)
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：valueId 不可为 0（0 保留给 None，生成器自动注入）。");
            }

            if (valueId < 1 || valueId > 255)
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：valueId {valueId} 越界，须 ∈ [1,255]（FeatureUnit.GetHashCode 位约束）。");
            }

            string enumMember = cols[4].Trim();
            if (!IsValidIdentifier(enumMember))
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：enumMember '{enumMember}' 不是合法 C# 标识符。");
            }

            if (enumMember == "None")
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：enumMember 不可为 None（保留）。");
            }

            string displayName = cols[5].Trim();
            if (displayName.Length == 0)
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：displayNameZh 不可为空。");
            }

            string colorHex = cols[6].Trim();
            if (colorHex.Length > 0 && !ColorUtility.TryParseHtmlString(colorHex, out _))
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：colorHex '{colorHex}' 无法解析（示例 #RRGGBB / #RRGGBBAA）。");
            }

            string audioGuid = cols[7].Trim();
            if (audioGuid.Length > 0 && !ResolveAudioPath(audioGuid, out _))
            {
                throw new FeatureCsvException($"第 {lineNo} 行 @val：audioGuid '{audioGuid}' 解析不到资源（可填 GUID 或 Res/... 相对路径；未入库请留空）。");
            }

            // 每维度 valueId / enumMember 唯一
            for (int i = 0; i < data.Values.Count; i++)
            {
                var v = data.Values[i];
                if (v.DimId != dimId)
                {
                    continue;
                }

                if (v.ValueId == valueId)
                {
                    throw new FeatureCsvException($"第 {lineNo} 行 @val：维度 {dimKey} 内 valueId {valueId} 与第 {v.SourceLine} 行重复。");
                }

                if (v.EnumMember == enumMember)
                {
                    throw new FeatureCsvException($"第 {lineNo} 行 @val：维度 {dimKey} 内 enumMember '{enumMember}' 与第 {v.SourceLine} 行重复。");
                }
            }

            data.Values.Add(new FeatureValRow
            {
                DimId = dimId,
                DimKey = dimKey,
                ValueId = valueId,
                EnumMember = enumMember,
                DisplayNameZh = displayName,
                ColorHex = colorHex,
                AudioGuid = audioGuid,
                Note = cols.Length > 8 ? cols[8] : string.Empty,
                SourceLine = lineNo,
            });
        }

        /// <summary>audioGuid 支持 32 位 GUID 或 Assets/Res 下相对路径。返回是否能解析到资源路径。</summary>
        public static bool ResolveAudioPath(string audioGuid, out string assetPath)
        {
            assetPath = null;
            if (string.IsNullOrEmpty(audioGuid))
            {
                return false;
            }

            if (audioGuid.Contains("/"))
            {
                string rel = audioGuid.StartsWith("Assets/")
                    ? audioGuid
                    : "Assets/Res/" + audioGuid;
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(rel)))
                {
                    assetPath = rel;
                    return true;
                }

                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(audioGuid);
            if (!string.IsNullOrEmpty(path))
            {
                assetPath = path;
                return true;
            }

            return false;
        }

        /// <summary>按 ',' 切成至多 9 段（note 保留内部逗号）。</summary>
        private static string[] SplitRow(string raw)
        {
            var cols = raw.Split(new[] { ',' }, 9, System.StringSplitOptions.None);
            if (cols.Length < 9)
            {
                var padded = new string[9];
                for (int i = 0; i < 9; i++)
                {
                    padded[i] = i < cols.Length ? cols[i] : string.Empty;
                }

                return padded;
            }

            return cols;
        }

        private static bool IsValidIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            if (!(char.IsLetter(s[0]) || s[0] == '_'))
            {
                return false;
            }

            for (int i = 1; i < s.Length; i++)
            {
                if (!(char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
