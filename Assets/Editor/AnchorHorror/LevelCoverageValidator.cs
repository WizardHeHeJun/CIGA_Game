// ------------------------------------------------------------
// LevelCoverageValidator.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
// 死局检测（迭代B · SC-9）：校验 LevelSequence 中关卡2走廊/房间（Level2Sub）物品特征并集，
// 是否覆盖关卡1（Level1Select）可能抽出的全部锚点特征。
// 背景：新两关卡流程关卡1 抽锚点时关卡2 未加载、无 registry，旧玩法「运行时 clamp 防死局」失效，
//       死局防线转移到本编辑器静态校验（design ADR-4 / 陷阱2）。
// 若关卡1 某特征在关卡2 全部子场景都找不到物品可满足 → 玩家抽到该锚点必超时失败、无从知晓。
// ------------------------------------------------------------
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 关卡覆盖（死局）校验器。菜单 <c>Ciga/AnchorHorror/校验关卡覆盖（死局检测）</c>：
    /// 选中一个或多个 LevelSequence 资产时校验选中项，否则校验工程内全部 LevelSequence。
    /// 纯编辑器工具（Ciga.AnchorHorror.EditorTools asmdef），不进运行时程序集。
    /// </summary>
    public static class LevelCoverageValidator
    {
        [MenuItem("Ciga/AnchorHorror/校验关卡覆盖（死局检测）")]
        public static void ValidateSelectedOrAll()
        {
            var sequences = CollectSequences();
            if (sequences.Count == 0)
            {
                Debug.LogWarning("[死局检测] 未找到 LevelSequence 资产（选中一个，或工程内需存在至少一个）。");
                return;
            }

            int okCount = 0;
            int failCount = 0;
            for (int i = 0; i < sequences.Count; i++)
            {
                if (Validate(sequences[i], out string report))
                {
                    okCount++;
                    Debug.Log(report);
                }
                else
                {
                    failCount++;
                    Debug.LogError(report);
                }
            }

            Debug.Log($"[死局检测] 完成：{okCount} 通过 / {failCount} 有死局风险（共 {sequences.Count} 个 LevelSequence）。");
        }

        /// <summary>
        /// 校验单个 LevelSequence：关卡2（Level2Sub）物品特征并集是否覆盖关卡1（Level1Select）物品特征并集。
        /// 返回 true=覆盖完整、无死局；false=存在关卡2 无法满足的关卡1 特征。report 为可读报告（可被编辑器测试断言）。
        /// </summary>
        public static bool Validate(LevelSequence sequence, out string report)
        {
            var sb = new StringBuilder();

            if (sequence == null)
            {
                report = "[死局检测] LevelSequence 为 null。";
                return false;
            }

            sb.Append($"[死局检测]《{sequence.name}》：");

            var level1 = new HashSet<FeatureUnit>();
            var level2 = new HashSet<FeatureUnit>();

            for (int i = 0; i < sequence.Count; i++)
            {
                var data = sequence.GetLevel(i);
                if (data == null)
                {
                    continue;
                }

                var bucket = sequence.GetKind(i) == LevelKind.Level1Select ? level1 : level2;
                CollectFeatures(data, bucket);
            }

            if (level1.Count == 0)
            {
                report = sb.Append("无关卡1（Level1Select）物品特征，跳过校验。").ToString();
                return true;
            }

            var missing = new List<FeatureUnit>();
            foreach (var f in level1)
            {
                if (!level2.Contains(f))
                {
                    missing.Add(f);
                }
            }

            if (missing.Count == 0)
            {
                report = sb
                    .Append($"通过。关卡1 共 {level1.Count} 种特征，关卡2走廊/房间并集全覆盖。")
                    .ToString();
                return true;
            }

            sb.Append($"⚠️ 死局风险！关卡2 未覆盖关卡1 的 {missing.Count}/{level1.Count} 种特征：");
            for (int i = 0; i < missing.Count; i++)
            {
                sb.Append(i == 0 ? " " : ", ").Append(DescribeFeature(missing[i]));
            }
            sb.Append("。若关卡1 抽到这些锚点，关卡2 无物品可满足 → 玩家必超时失败。请在关卡2走廊/房间补对应特征的物品。");
            report = sb.ToString();
            return false;
        }

        /// <summary>收集一个 LevelData 内所有物品的有效 non-None 特征（Override 优先，否则查 ItemDatabase 定义）。</summary>
        private static void CollectFeatures(LevelData data, HashSet<FeatureUnit> bucket)
        {
            var db = data.ItemDatabase;
            var items = data.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var placed = items[i];
                if (placed == null)
                {
                    continue;
                }

                FeatureColor color;
                FeatureShape shape;
                FeatureMaterial material;
                FeatureTexture texture;
                FeatureSound sound;

                if (placed.OverrideFeatures)
                {
                    color = placed.Color;
                    shape = placed.Shape;
                    material = placed.Material;
                    texture = placed.Texture;
                    sound = placed.Sound;
                }
                else if (db != null && db.TryGetById(placed.ItemId, out var def))
                {
                    color = def.Color;
                    shape = def.Shape;
                    material = def.Material;
                    texture = def.Texture;
                    sound = def.Sound;
                }
                else
                {
                    // 未 Override 且查不到定义 → 该物品有效特征未知，跳过（不计入覆盖）。
                    continue;
                }

                AddIfNotNone(bucket, FeatureDimension.Color, (int)color);
                AddIfNotNone(bucket, FeatureDimension.Shape, (int)shape);
                AddIfNotNone(bucket, FeatureDimension.Material, (int)material);
                AddIfNotNone(bucket, FeatureDimension.Texture, (int)texture);
                AddIfNotNone(bucket, FeatureDimension.Sound, (int)sound);
            }
        }

        private static void AddIfNotNone(HashSet<FeatureUnit> bucket, FeatureDimension dim, int value)
        {
            if (value != 0) // None 约定恒为 0（FeatureUnit.IsNone）
            {
                bucket.Add(new FeatureUnit(dim, value));
            }
        }

        /// <summary>把 FeatureUnit 描述成「维度:值」（值取枚举名，便于人读）。</summary>
        private static string DescribeFeature(FeatureUnit unit)
        {
            string valueName;
            switch (unit.Dimension)
            {
                case FeatureDimension.Color:
                    valueName = ((FeatureColor)unit.Value).ToString();
                    break;
                case FeatureDimension.Shape:
                    valueName = ((FeatureShape)unit.Value).ToString();
                    break;
                case FeatureDimension.Material:
                    valueName = ((FeatureMaterial)unit.Value).ToString();
                    break;
                case FeatureDimension.Texture:
                    valueName = ((FeatureTexture)unit.Value).ToString();
                    break;
                case FeatureDimension.Sound:
                    valueName = ((FeatureSound)unit.Value).ToString();
                    break;
                default:
                    valueName = unit.Value.ToString();
                    break;
            }

            return $"{unit.Dimension}.{valueName}";
        }

        /// <summary>优先取 Selection 中的 LevelSequence；无选中则收集工程内全部。</summary>
        private static List<LevelSequence> CollectSequences()
        {
            var result = new List<LevelSequence>();

            var selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] is LevelSequence seq)
                {
                    result.Add(seq);
                }
            }

            if (result.Count > 0)
            {
                return result;
            }

            var guids = AssetDatabase.FindAssets("t:LevelSequence");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var seq = AssetDatabase.LoadAssetAtPath<LevelSequence>(path);
                if (seq != null)
                {
                    result.Add(seq);
                }
            }

            return result;
        }
    }
}
