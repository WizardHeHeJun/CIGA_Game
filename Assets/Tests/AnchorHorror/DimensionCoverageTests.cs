// ------------------------------------------------------------
// DimensionCoverageTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// ------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>
    /// 维度覆盖守卫：AnchorFeatures.csv 若新增维度（FeatureDimension 多一个值）而 ItemDefinition / PlacedItem
    /// 未补对应 typed 字段，本测试大声失败——把「数据侧静默丢维度」变成显式红灯。
    /// （关卡编辑器 popup / 回写已改为动态维度驱动，无需随维度改动；本守卫兜住数据类。）
    /// </summary>
    public class DimensionCoverageTests
    {
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

        private static string FieldName(FeatureDimension dim)
        {
            string k = dim.ToString();
            return "_" + char.ToLowerInvariant(k[0]) + k.Substring(1);
        }

        [Test]
        public void ItemDefinition_HasEnumFieldForEveryDimension()
        {
            foreach (FeatureDimension dim in Enum.GetValues(typeof(FeatureDimension)))
            {
                var f = typeof(ItemDefinition).GetField(FieldName(dim), Priv);
                Assert.IsNotNull(f, $"ItemDefinition 缺维度 {dim} 的字段 {FieldName(dim)}（CSV 新增了维度？补 typed 字段 + 只读属性）。");
                Assert.IsTrue(f.FieldType.IsEnum, $"ItemDefinition.{FieldName(dim)} 应为枚举字段。");
            }
        }

        [Test]
        public void PlacedItem_HasEnumFieldForEveryDimension()
        {
            foreach (FeatureDimension dim in Enum.GetValues(typeof(FeatureDimension)))
            {
                var f = typeof(PlacedItem).GetField(FieldName(dim), Priv);
                Assert.IsNotNull(f, $"PlacedItem 缺维度 {dim} 的覆盖字段 {FieldName(dim)}（CSV 新增了维度？补 typed 覆盖字段 + 只读属性）。");
                Assert.IsTrue(f.FieldType.IsEnum, $"PlacedItem.{FieldName(dim)} 应为枚举字段。");
            }
        }

        [Test]
        public void FeatureTag_SetFeatures_RoundTripsAllDimensions()
        {
            var go = new GameObject("__ft_test", typeof(BoxCollider2D));
            try
            {
                var tag = go.AddComponent<FeatureTag>();
                var feats = new List<FeatureUnit>();
                foreach (FeatureDimension dim in Enum.GetValues(typeof(FeatureDimension)))
                {
                    feats.Add(new FeatureUnit(dim, 1)); // 各维度 valueId=1（CSV 保证每维起于 1）
                }

                tag.SetFeatures(feats);

                var got = tag.GetFeatures();
                foreach (FeatureDimension dim in Enum.GetValues(typeof(FeatureDimension)))
                {
                    int v = 0;
                    for (int i = 0; i < got.Count; i++)
                    {
                        if (got[i].Dimension == dim)
                        {
                            v = got[i].Value;
                            break;
                        }
                    }

                    Assert.AreEqual(1, v, $"SetFeatures 未能通用写入维度 {dim}。");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
