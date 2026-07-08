// ------------------------------------------------------------
// ItemCsvParser.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 解析 + 强校验 AnchorItems.csv。任何违规抛 ItemCsvException（含行号），调用方中止不落盘。
    /// 表头驱动：第一非注释行是表头；特征列名须与 FeatureDimension 维度名一致（Color/Shape/Material/Texture/Sound…），
    /// 特征取值按枚举成员名反射对齐生成枚举 Feature&lt;DimKey&gt;（与 AnchorFeatures.csv 单一真源保持一致，加维度只需表头加一列）。
    /// 校验：必需列存在 / itemId 非空且唯一 / displayName 非空 / 特征成员合法 / spritePath 可解析 / scale 是数字 / collider ∈ {Box,Circle,空}。
    /// </summary>
    public static class ItemCsvParser
    {
        private static readonly HashSet<string> FixedCols = new HashSet<string>
        {
            "itemId", "displayName", "spritePath", "scaleX", "scaleY", "collider", "note",
        };

        public static ItemCsvData Parse(string csvText)
        {
            if (string.IsNullOrEmpty(csvText))
            {
                throw new ItemCsvException("CSV 内容为空。");
            }

            var data = new ItemCsvData();
            var lines = csvText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            var colIndex = new Dictionary<string, int>();
            var featureCols = new List<KeyValuePair<string, int>>(); // dimKey → 列号
            var seenIds = new Dictionary<string, int>();
            bool headerParsed = false;
            int headerColCount = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                string raw = lines[i].Trim();
                if (raw.Length == 0 || raw.StartsWith("#"))
                {
                    continue;
                }

                if (!headerParsed)
                {
                    var headerCols = raw.Split(',');
                    ParseHeader(headerCols, colIndex, featureCols, data, lineNo);
                    headerColCount = headerCols.Length;
                    headerParsed = true;
                    continue;
                }

                // 按表头列数限制 split：末列（note）可含逗号，不错位。
                var cols = raw.Split(new[] { ',' }, headerColCount, System.StringSplitOptions.None);
                data.Rows.Add(ParseRow(cols, colIndex, featureCols, seenIds, lineNo));
            }

            if (!headerParsed)
            {
                throw new ItemCsvException("CSV 缺表头行（第一非注释行应为列名）。");
            }

            // 零数据行是合法状态：正式物品全部自包含不走 itemId 引用，目录允许为空（仅供未来按 id 复用的物品登记）。
            return data;
        }

        private static void ParseHeader(string[] cols, Dictionary<string, int> colIndex,
            List<KeyValuePair<string, int>> featureCols, ItemCsvData data, int lineNo)
        {
            for (int c = 0; c < cols.Length; c++)
            {
                string name = cols[c].Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                if (colIndex.ContainsKey(name))
                {
                    throw new ItemCsvException($"第 {lineNo} 行表头：列名 '{name}' 重复。");
                }

                colIndex[name] = c;
            }

            RequireCol(colIndex, "itemId", lineNo);
            RequireCol(colIndex, "displayName", lineNo);

            for (int c = 0; c < cols.Length; c++)
            {
                string name = cols[c].Trim();
                if (name.Length == 0 || FixedCols.Contains(name))
                {
                    continue;
                }

                if (Enum.IsDefined(typeof(FeatureDimension), name))
                {
                    featureCols.Add(new KeyValuePair<string, int>(name, c));
                    if (!data.FeatureDimKeys.Contains(name))
                    {
                        data.FeatureDimKeys.Add(name);
                    }
                }
                else
                {
                    throw new ItemCsvException($"第 {lineNo} 行表头：未知列 '{name}'（既非固定列，也不是 FeatureDimension 维度名）。");
                }
            }
        }

        private static ItemCsvRow ParseRow(string[] cols, Dictionary<string, int> colIndex,
            List<KeyValuePair<string, int>> featureCols, Dictionary<string, int> seenIds, int lineNo)
        {
            var row = new ItemCsvRow { SourceLine = lineNo };

            row.ItemId = Get(cols, colIndex, "itemId").Trim();
            if (row.ItemId.Length == 0)
            {
                throw new ItemCsvException($"第 {lineNo} 行：itemId 不可为空。");
            }

            if (seenIds.TryGetValue(row.ItemId, out int prev))
            {
                throw new ItemCsvException($"第 {lineNo} 行：itemId '{row.ItemId}' 与第 {prev} 行重复。");
            }

            seenIds[row.ItemId] = lineNo;

            row.DisplayName = Get(cols, colIndex, "displayName").Trim();
            if (row.DisplayName.Length == 0)
            {
                throw new ItemCsvException($"第 {lineNo} 行：displayName 不可为空（itemId={row.ItemId}）。");
            }

            for (int f = 0; f < featureCols.Count; f++)
            {
                string dimKey = featureCols[f].Key;
                int col = featureCols[f].Value;
                string member = (col < cols.Length ? cols[col] : string.Empty).Trim();
                int value = 0;
                if (member.Length > 0 && member != "None")
                {
                    value = ResolveFeatureValue(dimKey, member, lineNo);
                }

                row.FeatureValues[dimKey] = value;
            }

            string sp = Get(cols, colIndex, "spritePath").Trim();
            if (sp.Length > 0 && !ResolveSpritePath(sp, out _))
            {
                throw new ItemCsvException($"第 {lineNo} 行：spritePath '{sp}' 解析不到资源（可填 GUID 或 Res/AnchorHorror 下相对路径；留空则用兜底图）。");
            }

            row.SpritePath = sp;
            row.ScaleX = ParseFloatOr(Get(cols, colIndex, "scaleX"), 1f, lineNo, "scaleX");
            row.ScaleY = ParseFloatOr(Get(cols, colIndex, "scaleY"), 1f, lineNo, "scaleY");

            string coll = Get(cols, colIndex, "collider").Trim();
            if (coll.Length == 0 || coll.Equals("Box", StringComparison.OrdinalIgnoreCase))
            {
                row.ColliderCircle = false;
            }
            else if (coll.Equals("Circle", StringComparison.OrdinalIgnoreCase))
            {
                row.ColliderCircle = true;
            }
            else
            {
                throw new ItemCsvException($"第 {lineNo} 行：collider '{coll}' 非法（应为 Box / Circle / 空）。");
            }

            row.Note = Get(cols, colIndex, "note");
            return row;
        }

        /// <summary>维度枚举成员名 → int 值（反射对齐生成枚举 Feature&lt;DimKey&gt;，与 CSV 单一真源一致）。</summary>
        private static int ResolveFeatureValue(string dimKey, string member, int lineNo)
        {
            var enumType = typeof(FeatureUnit).Assembly.GetType("Ciga.AnchorHorror.Feature" + dimKey);
            if (enumType == null)
            {
                throw new ItemCsvException($"第 {lineNo} 行：找不到维度 '{dimKey}' 的枚举类型 Feature{dimKey}（AnchorFeatures.csv 是否已生成？）。");
            }

            if (!Enum.IsDefined(enumType, member))
            {
                throw new ItemCsvException($"第 {lineNo} 行：'{member}' 不是 Feature{dimKey} 的合法成员（对照 AnchorFeatures.csv）。");
            }

            return (int)Enum.Parse(enumType, member);
        }

        /// <summary>spritePath 支持 32 位 GUID 或 Res/AnchorHorror 下相对路径 / 完整 Assets 路径。</summary>
        public static bool ResolveSpritePath(string spec, out string assetPath)
        {
            assetPath = null;
            if (string.IsNullOrEmpty(spec))
            {
                return false;
            }

            if (spec.Contains("/") || spec.Contains("."))
            {
                string rel = spec.StartsWith("Assets/") ? spec : "Assets/Res/AnchorHorror/" + spec;
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(rel)))
                {
                    assetPath = rel;
                    return true;
                }

                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(spec);
            if (!string.IsNullOrEmpty(path))
            {
                assetPath = path;
                return true;
            }

            return false;
        }

        private static void RequireCol(Dictionary<string, int> colIndex, string name, int lineNo)
        {
            if (!colIndex.ContainsKey(name))
            {
                throw new ItemCsvException($"第 {lineNo} 行表头：缺必需列 '{name}'。");
            }
        }

        private static string Get(string[] cols, Dictionary<string, int> colIndex, string name)
        {
            if (colIndex.TryGetValue(name, out int c) && c < cols.Length)
            {
                return cols[c];
            }

            return string.Empty;
        }

        private static float ParseFloatOr(string s, float def, int lineNo, string field)
        {
            s = s.Trim();
            if (s.Length == 0)
            {
                return def;
            }

            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                throw new ItemCsvException($"第 {lineNo} 行：{field} '{s}' 不是数字。");
            }

            return v;
        }
    }
}
