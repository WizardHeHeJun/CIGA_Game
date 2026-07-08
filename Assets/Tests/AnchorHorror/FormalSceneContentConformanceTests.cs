// ------------------------------------------------------------
// FormalSceneContentConformanceTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-08
// Desc   : 正式关卡内容对表回归——以策划配置表为真源，校验六场景
//          每个物品的五维特征 / 美术接线 / 特征词典中文名 / 音效 clip。
//          策划改表或重跑生成器后跑此测试即可发现配置漂移。
// ------------------------------------------------------------
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>
    /// 正式关卡内容对表（真源 = 策划配置表 2026-07-08 版）。
    /// 物品顺序与 FormalSceneArtSetup 各 SceneSpec 的声明顺序一致。
    /// </summary>
    // 类名带 SceneContent 使字母序排在 FormalSceneArtSetupTests（生成器重建）之后，
    // 保证同一轮测试先重建资产、再对表校验。
    public class FormalSceneContentConformanceTests
    {
        private const string LevelsDir = "Assets/Res/AnchorHorror/Levels/";
        private const string FeatureDbPath = "Assets/Res/AnchorHorror/FeatureDatabase.asset";

        private sealed class Row
        {
            public readonly string Name;
            public readonly bool VisualOnly;
            public readonly int C;
            public readonly int S;
            public readonly int M;
            public readonly int T;
            public readonly int Snd;

            public Row(string name, int c, int s, int m, int t, int snd, bool visualOnly = false)
            {
                Name = name;
                C = c;
                S = s;
                M = m;
                T = t;
                Snd = snd;
                VisualOnly = visualOnly;
            }
        }

        // 策划配置表（真源）。枚举值参照 FeatureEnums.Generated.cs。
        private static readonly Dictionary<string, Row[]> Expected = new Dictionary<string, Row[]>
        {
            ["Formal_Bedroom"] = new[]
            {
                new Row("床", 5, 3, 4, 6, 1),
                new Row("衣柜", 7, 2, 1, 2, 2),
                new Row("梳妆台", 5, 2, 1, 1, 2),
                new Row("台灯", 8, 1, 2, 7, 3),
                new Row("和母亲的合照", 7, 2, 9, 1, 4),
                new Row("窗户", 9, 2, 3, 8, 5),
                new Row("地毯", 10, 3, 4, 6, 1),
                new Row("椅子", 5, 5, 1, 1, 2),
            },
            ["Formal_Aisle"] = new[]
            {
                new Row("门A", 11, 2, 1, 9, 2, visualOnly: true),
                new Row("门B", 5, 2, 1, 1, 2, visualOnly: true),
                new Row("地毯", 12, 3, 4, 10, 1),
                new Row("墙灯", 13, 1, 10, 3, 3),
                new Row("钥匙", 14, 5, 2, 1, 6),
                new Row("楼梯", 15, 3, 8, 2, 2),
            },
            ["Formal_LivingRoom"] = new[]
            {
                new Row("沙发", 16, 3, 4, 6, 1),
                new Row("电视机", 6, 2, 3, 1, 6),
                new Row("相框", 7, 2, 1, 1, 4),
                new Row("茶几", 11, 2, 1, 11, 2),
                new Row("落地灯", 8, 7, 4, 7, 3),
                new Row("玩具箱", 17, 2, 7, 2, 7),
            },
            ["Formal_Kitchen"] = new[]
            {
                new Row("电饭煲", 5, 2, 11, 1, 6),
                new Row("水龙头", 18, 6, 2, 8, 5),
                new Row("冰箱", 5, 2, 2, 4, 6),
                new Row("碗柜", 7, 2, 1, 2, 2),
                new Row("餐桌", 11, 2, 1, 11, 2),
                new Row("窗户", 9, 2, 3, 1, 4),
                new Row("钟表", 14, 1, 2, 13, 5),
                new Row("水槽", 18, 2, 2, 12, 5),
            },
            ["Formal_Bathroom"] = new[]
            {
                new Row("马桶", 5, 1, 6, 1, 5),
                new Row("洗衣机", 5, 1, 2, 4, 6),
                new Row("镜子", 18, 2, 3, 8, 4),
                new Row("洗手台", 5, 2, 6, 1, 5),
                new Row("毛巾", 2, 3, 4, 6, 1),
                new Row("排水口", 6, 1, 2, 12, 5),
            },
            ["Formal_Utility"] = new[]
            {
                new Row("旧电饭煲", 8, 2, 2, 14, 6),
                new Row("破钟", 6, 1, 2, 15, 5),
                new Row("纸箱", 7, 2, 5, 16, 2),
                new Row("镜子碎片", 18, 5, 3, 17, 4),
                new Row("坏玩具", 17, 5, 7, 18, 7),
                new Row("折叠椅", 6, 5, 2, 18, 6),
            },
        };

        // 策划表用词 ↔ FeatureDatabase 中文显示名（仅覆盖表内用到的值）
        private static readonly Dictionary<FeatureDimension, Dictionary<int, string>> ExpectedNames =
            new Dictionary<FeatureDimension, Dictionary<int, string>>
            {
                [FeatureDimension.Color] = new Dictionary<int, string>
                {
                    [2] = "蓝色", [5] = "白色", [6] = "黑色", [7] = "棕色", [8] = "米黄",
                    [9] = "透明", [10] = "浅灰", [11] = "深棕", [12] = "深红", [13] = "暖黄",
                    [14] = "金色", [15] = "灰色", [16] = "深灰", [17] = "彩色", [18] = "银色",
                },
                [FeatureDimension.Shape] = new Dictionary<int, string>
                {
                    [1] = "圆形", [2] = "方形", [3] = "长条", [5] = "不规则", [6] = "弯曲", [7] = "锥形",
                },
                [FeatureDimension.Material] = new Dictionary<int, string>
                {
                    [1] = "木质", [2] = "金属", [3] = "玻璃", [4] = "布料", [5] = "纸质", [6] = "陶瓷",
                    [7] = "塑料", [8] = "石材", [9] = "木质玻璃", [10] = "玻璃金属", [11] = "金属塑料",
                },
                [FeatureDimension.Texture] = new Dictionary<int, string>
                {
                    [1] = "光滑", [2] = "粗糙", [3] = "亮面", [4] = "哑光", [6] = "柔软", [7] = "柔光",
                    [8] = "反光", [9] = "剥落", [10] = "纤维", [11] = "磨损", [12] = "湿润",
                    [13] = "刻度", [14] = "脱漆", [15] = "裂纹", [16] = "破损", [17] = "裂痕", [18] = "划痕",
                },
                [FeatureDimension.Sound] = new Dictionary<int, string>
                {
                    [1] = "布料轻触声", [2] = "木质摩擦声", [3] = "灯光嗡鸣", [4] = "玻璃轻响",
                    [5] = "滴答声", [6] = "金属机械声", [7] = "塑料轻响",
                },
            };

        [Test]
        public void AllScenes_ItemFeatures_MatchPlanningTable()
        {
            foreach (var pair in Expected)
            {
                var level = LoadLevel(pair.Key);
                Assert.AreEqual(pair.Value.Length, level.Items.Count,
                    $"{pair.Key} 物品数量应为 {pair.Value.Length}");

                for (int i = 0; i < pair.Value.Length; i++)
                {
                    var row = pair.Value[i];
                    var placed = level.Items[i];
                    string tag = $"{pair.Key}[{i}] {row.Name}";

                    Assert.AreEqual(row.VisualOnly, placed.VisualOnly, $"{tag} VisualOnly 不符");
                    if (!row.VisualOnly)
                    {
                        Assert.IsTrue(placed.OverrideFeatures, $"{tag} 应覆盖特征（OverrideFeatures）");
                    }

                    // visualOnly（门A/门B）特征为数据留档，运行时不读，但仍与策划表对账
                    Assert.AreEqual(row.C, (int)placed.Color, $"{tag} 颜色不符（期望 {row.C}）");
                    Assert.AreEqual(row.S, (int)placed.Shape, $"{tag} 形状不符（期望 {row.S}）");
                    Assert.AreEqual(row.M, (int)placed.Material, $"{tag} 材质不符（期望 {row.M}）");
                    Assert.AreEqual(row.T, (int)placed.Texture, $"{tag} 质感不符（期望 {row.T}）");
                    Assert.AreEqual(row.Snd, (int)placed.Sound, $"{tag} 声音不符（期望 {row.Snd}）");
                }
            }
        }

        [Test]
        public void AllScenes_ArtHooked_DefaultSpriteAndCollider()
        {
            foreach (var pair in Expected)
            {
                var level = LoadLevel(pair.Key);
                for (int i = 0; i < level.Items.Count && i < pair.Value.Length; i++)
                {
                    var row = pair.Value[i];
                    var placed = level.Items[i];
                    string tag = $"{pair.Key}[{i}] {row.Name}";

                    Assert.IsTrue(placed.OverrideSprite, $"{tag} 应覆盖 Sprite（美术未接）");
                    Assert.IsNotNull(placed.Sprite, $"{tag} Default 图缺失（美术未接）");

                    if (!row.VisualOnly)
                    {
                        Assert.Greater(placed.ColliderSize.x, 0f, $"{tag} 碰撞框宽度未配置");
                        Assert.Greater(placed.ColliderSize.y, 0f, $"{tag} 碰撞框高度未配置");
                    }
                }
            }
        }

        [Test]
        public void FeatureDatabase_UsedUnits_HaveChineseNames_AndSoundClips()
        {
            var db = AssetDatabase.LoadAssetAtPath<FeatureDatabase>(FeatureDbPath);
            Assert.IsNotNull(db, $"找不到 {FeatureDbPath}");

            foreach (var dim in ExpectedNames)
            {
                foreach (var entry in dim.Value)
                {
                    var unit = new FeatureUnit(dim.Key, entry.Key);
                    Assert.AreEqual(entry.Value, db.GetDisplayName(unit),
                        $"特征 {dim.Key}:{entry.Key} 显示名与策划表不符");
                }
            }

            foreach (var entry in ExpectedNames[FeatureDimension.Sound])
            {
                var unit = new FeatureUnit(FeatureDimension.Sound, entry.Key);
                Assert.IsNotNull(db.GetAudioClip(unit),
                    $"声音特征「{entry.Value}」未配置 AudioClip（检视/命中时无声）");
            }
        }

        /// <summary>
        /// 守卫：Bootstrap 的 GameManager._sequence 必须接正式序列。
        /// 历史坑：Demo/测试生成器会把接线抢成 Demo 序列（正式背景上跑白方块物品）。
        /// </summary>
        [Test]
        public void Bootstrap_GameManagerSequence_WiredToFormal()
        {
            string formalGuid = AssetDatabase.AssetPathToGUID(LevelsDir + "Formal_Sequence.asset");
            Assert.IsFalse(string.IsNullOrEmpty(formalGuid), "Formal_Sequence.asset 不存在");

            string sceneText = File.ReadAllText("Assets/Res/AnchorHorror/Bootstrap.unity");
            StringAssert.Contains($"_sequence: {{fileID: 11400000, guid: {formalGuid}", sceneText,
                "Bootstrap 的 GameManager._sequence 未接正式序列（被 Demo/测试序列抢走？菜单「接线正式关卡数据」可恢复）");
        }

        private static LevelData LoadLevel(string assetName)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelsDir + assetName + ".asset");
            Assert.IsNotNull(level, $"找不到 {LevelsDir}{assetName}.asset");
            return level;
        }
    }
}
