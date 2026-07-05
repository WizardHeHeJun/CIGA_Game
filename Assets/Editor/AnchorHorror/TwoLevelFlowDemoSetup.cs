// ------------------------------------------------------------
// TwoLevelFlowDemoSetup.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// Desc   : 两关卡流程冒烟测试数据生成器。
//          建 1 关卡1 LevelData（8 物品，每件各带 8 种 distinct 特征之一）+
//          3 个关卡2 子场景 LevelData（三场景并集覆盖全部 8 种特征，保证可解）+
//          LevelSequence（entry[0]=Level1Select + entry[1..3]=Level2Sub）。
//          幂等：已存在则覆盖重建（清空重填），别堆叠。
//          菜单：Ciga/AnchorHorror/生成两关卡流程 Demo 数据
// ------------------------------------------------------------
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 两关卡流程 demo 数据生成器。
    /// 8 种 distinct 特征（跨 Color/Shape/Material/Texture 四维）：
    ///   F1=Color.Red   F2=Color.Blue   F3=Color.Green
    ///   F4=Shape.Round F5=Shape.Square
    ///   F6=Material.Wood F7=Material.Metal
    ///   F8=Texture.Smooth
    ///
    /// 关卡1（entries[0]）：8 物品，物品 i 只带特征 F(i+1)，覆盖 8 种不重复。
    /// 子场景1（entries[1]）：F1 F2 F4 F6 （4 种）
    /// 子场景2（entries[2]）：F3 F5 F7 F8 （4 种）
    /// 子场景3（entries[3]）：F1 F3 F4 F8 （冗余，三场景并集 = 全 8 种，保可解）
    ///
    /// 三场景并集 = {Red,Blue,Green,Round,Square,Wood,Metal,Smooth} = F1..F8（可解证明见上）。
    /// </summary>
    public static class TwoLevelFlowDemoSetup
    {
        private const string ResDir = "Assets/Res/AnchorHorror";
        private const string LevelsDir = ResDir + "/Levels";
        private const string BgDir = ResDir + "/Backgrounds";
        private const string BootstrapScene = ResDir + "/Bootstrap.unity";
        private const string SquareSpritePath = ResDir + "/WhiteSquare.png";
        private const string FormalSequencePath = LevelsDir + "/Formal_Sequence.asset";

        // 8 种 distinct 特征（维度 + 枚举 int 值）
        // 每个特征以 (dimension, int value) 标记，用于 PlacedItem OverrideFeatures 填写
        private static readonly (FeatureDimension dim, int val, string label)[] Features8 =
        {
            (FeatureDimension.Color,    (int)FeatureColor.Red,     "Color.Red"),
            (FeatureDimension.Color,    (int)FeatureColor.Blue,    "Color.Blue"),
            (FeatureDimension.Color,    (int)FeatureColor.Green,   "Color.Green"),
            (FeatureDimension.Shape,    (int)FeatureShape.Round,   "Shape.Round"),
            (FeatureDimension.Shape,    (int)FeatureShape.Square,  "Shape.Square"),
            (FeatureDimension.Material, (int)FeatureMaterial.Wood, "Material.Wood"),
            (FeatureDimension.Material, (int)FeatureMaterial.Metal,"Material.Metal"),
            (FeatureDimension.Texture,  (int)FeatureTexture.Smooth,"Texture.Smooth"),
        };

        // 关卡1（8 物品，对应 ItemDatabase 里的 8 个 itemId）
        // 按 ItemDatabase 中顺序：chair_wood, lamp_metal, book_paper, cup_glass,
        //                         clock_metal, cloth_red, box_wood, bottle_plastic
        private static readonly string[] Level1ItemIds =
        {
            "chair_wood",    // 带 F1=Color.Red
            "lamp_metal",    // 带 F2=Color.Blue
            "book_paper",    // 带 F3=Color.Green
            "cup_glass",     // 带 F4=Shape.Round
            "clock_metal",   // 带 F5=Shape.Square
            "cloth_red",     // 带 F6=Material.Wood
            "box_wood",      // 带 F7=Material.Metal
            "bottle_plastic",// 带 F8=Texture.Smooth
        };

        // 子场景物品数据：(itemId, featureIndex in Features8[])。5 个子场景，并集覆盖全 8 种特征（可解）。
        // 子场景1：F1(Red), F2(Blue)
        private static readonly (string id, int fi)[] Sub1Items =
        {
            ("chair_wood", 0), // F1=Color.Red
            ("lamp_metal", 1), // F2=Color.Blue
        };

        // 子场景2：F3(Green), F4(Round)
        private static readonly (string id, int fi)[] Sub2Items =
        {
            ("book_paper", 2), // F3=Color.Green
            ("cup_glass",  3), // F4=Shape.Round
        };

        // 子场景3：F5(Square), F6(Wood)
        private static readonly (string id, int fi)[] Sub3Items =
        {
            ("clock_metal", 4), // F5=Shape.Square
            ("cloth_red",   5), // F6=Material.Wood
        };

        // 子场景4：F7(Metal), F8(Smooth)
        private static readonly (string id, int fi)[] Sub4Items =
        {
            ("box_wood",       6), // F7=Material.Metal
            ("bottle_plastic", 7), // F8=Texture.Smooth
        };

        // 子场景5：F1(Red), F4(Round), F8(Smooth)（冗余，方便玩家来回找齐）
        private static readonly (string id, int fi)[] Sub5Items =
        {
            ("chair_wood",     0), // F1=Color.Red
            ("cup_glass",      3), // F4=Shape.Round
            ("bottle_plastic", 7), // F8=Texture.Smooth
        };

        [MenuItem("Ciga/AnchorHorror/生成两关卡流程 Demo 数据")]
        public static void BuildAllMenu()
        {
            try
            {
                BuildAll();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "两关卡流程 Demo 数据",
                    $"已生成/更新：\n" +
                    $"  关卡1 LevelData（8 物品，8 种 distinct 特征）\n" +
                    $"  子场景1..5 LevelData\n" +
                    $"  LevelSequence（entry[0..5]，含 6 张房间背景）\n" +
                    $"  并已接线到 Bootstrap.unity。",
                    "好");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TwoLevelFlowDemoSetup] 生成失败：{ex}");
                EditorUtility.DisplayDialog("生成失败", ex.Message, "确定");
            }
        }

        /// <summary>一键重建：先重建 Bootstrap 装配，再建两关卡数据并重接序列（按序，缺一不可）。</summary>
        [MenuItem("Ciga/AnchorHorror/★ 一键重建全部（装配 + 两关卡Demo数据）")]
        public static void RebuildEverything()
        {
            AnchorHorrorSetup.BuildAll(); // 先重建 Bootstrap（相机跟随/UI/教程图/ResultConfig/操作提示等）
            BuildAll();                   // 再建两关卡数据并重接 _sequence（上一步重建裸场景会清掉序列接线，必须再跑）
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "一键重建完成",
                "已重建 Bootstrap（相机跟随/教程图/结算配置/操作提示）+ 两关卡 Demo 数据并接线。\n从 GameMain 场景 Play 即可。",
                "好");
        }

        /// <summary>安全接线正式关卡：只把 Formal_Sequence.asset 接到 Bootstrap，不生成/覆盖任何关卡数据。</summary>
        [MenuItem("Ciga/AnchorHorror/接线正式关卡数据")]
        public static void WireFormalSequenceMenu()
        {
            bool ok = TryWireFormalSequence(out string message);
            EditorUtility.DisplayDialog(ok ? "接线完成" : "接线失败", message, "好");
        }

        /// <summary>接线正式关卡序列，供菜单 / AI Bridge 调用；不弹窗，便于自动化验证。</summary>
        public static bool TryWireFormalSequence(out string message)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<LevelSequence>(FormalSequencePath);
            if (sequence == null)
            {
                message = $"请先创建正式关卡序列资产：\n{FormalSequencePath}\n\n此操作只负责接线，不会自动生成或覆盖正式关卡数据。";
                Debug.LogWarning("[TwoLevelFlowDemoSetup] " + message);
                return false;
            }

            bool wired = WireBootstrapScene(sequence, "正式关卡数据");
            if (!wired)
            {
                message = $"正式关卡序列接线失败，请检查 Bootstrap 场景是否存在且包含 GameManager：\n{BootstrapScene}";
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            message = $"已将正式关卡序列接到 Bootstrap：\n{FormalSequencePath}\n\n未生成或覆盖任何关卡数据。";
            return true;
        }

        /// <summary>幂等生成入口：建资产 + 接线到 Bootstrap。</summary>
        public static void BuildAll()
        {
            EnsureFolder(LevelsDir);

            var db = LoadItemDatabase();
            if (db == null)
            {
                throw new InvalidOperationException(
                    $"[TwoLevelFlowDemoSetup] 找不到 ItemDatabase（{ResDir}/ItemDatabase.asset），请先运行「生成可运行装配」。");
            }

            var levelCfg = AssetDatabase.LoadAssetAtPath<LevelConfig>(ResDir + "/LevelConfig.asset");
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);

            // 1. 建 6 个 LevelData（关卡1 + 5 子场景）
            var level1 = BuildLevel1Data(db, levelCfg);
            var sub1   = BuildSubLevelData("DemoSub1", "关卡2-子场景1", db, levelCfg, Sub1Items);
            var sub2   = BuildSubLevelData("DemoSub2", "关卡2-子场景2", db, levelCfg, Sub2Items);
            var sub3   = BuildSubLevelData("DemoSub3", "关卡2-子场景3", db, levelCfg, Sub3Items);
            var sub4   = BuildSubLevelData("DemoSub4", "关卡2-子场景4", db, levelCfg, Sub4Items);
            var sub5   = BuildSubLevelData("DemoSub5", "关卡2-子场景5", db, levelCfg, Sub5Items);

            // 2. 建 LevelSequence，并把 6 张房间背景接到 entry._background（数据驱动，重建自动复现）
            //    映射：关卡1=卧室；子场景 1..5 = 起居室/浴室/走廊/厨房/杂物间。
            //    注意关卡2从中间 entry[3] 进入，因此 entry[3] 接 Aisle（走廊）。
            var backgrounds = new[]
            {
                LoadBg("Bedroom"),
                LoadBg("LivingRoom"),
                LoadBg("Bathroom"),
                LoadBg("Aisle"),
                LoadBg("Kitchen"),
                LoadBg("Utility"),
            };
            var seq = BuildLevelSequence(level1, new[] { sub1, sub2, sub3, sub4, sub5 }, square, backgrounds);

            // 3. 接线到 Bootstrap
            WireBootstrapScene(seq);

            AssetDatabase.SaveAssets();
            Debug.Log(
                "[TwoLevelFlowDemoSetup] 已生成两关卡流程 Demo 数据并接线到 Bootstrap.unity。\n" +
                "8 种特征：Color.Red / Color.Blue / Color.Green / Shape.Round / Shape.Square / Material.Wood / Material.Metal / Texture.Smooth\n" +
                "Sub1覆盖：Red Blue Round Wood\n" +
                "Sub2覆盖：Green Square Metal Smooth\n" +
                "Sub3覆盖：Red Green Round Smooth（冗余）\n" +
                "三场景并集 = 全 8 种 → 可解。");
        }

        // ──────────────────────────────────────────────────────────────
        //  关卡1 LevelData
        // ──────────────────────────────────────────────────────────────

        private static LevelData BuildLevel1Data(ItemDatabase db, LevelConfig levelCfg)
        {
            var path = LevelsDir + "/DemoTwoLevelFlow_Level1.asset";
            var ld = LoadOrCreate<LevelData>(path);
            var so = new SerializedObject(ld);

            so.FindProperty("_levelName").stringValue = "两关卡流程·关卡1（选物）";
            so.FindProperty("_itemDatabase").objectReferenceValue = db;
            if (levelCfg != null)
            {
                so.FindProperty("_levelConfig").objectReferenceValue = levelCfg;
            }

            // 玩家出生在下方中央，与物品网格拉开距离
            so.FindProperty("_playerSpawn").vector2Value = new Vector2(0f, -4f);

            // 8 物品：2 行 × 4 列，间距 2.0（保证玩家 interactRadius=1.5 够得到）
            // 行 y=1.5(行0) y=-0.5(行1)；列 x=-3,-1,1,3
            var items = so.FindProperty("_items");
            items.ClearArray();

            for (int i = 0; i < Level1ItemIds.Length; i++)
            {
                int row = i / 4;
                int col = i % 4;
                float x = (col - 1.5f) * 2f;   // -3, -1, 1, 3
                float y = 1.5f - row * 2f;      // row0=1.5, row1=-0.5

                // 每个物品的特征配置：OverrideFeatures=true，只给 Features8[i] 对应的维度一个值，其余 None(0)
                var feat = Features8[i];

                items.InsertArrayElementAtIndex(i);
                var e = items.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("_itemId").stringValue = Level1ItemIds[i];
                e.FindPropertyRelative("_position").vector2Value = new Vector2(x, y);
                e.FindPropertyRelative("_rotationZ").floatValue = 0f;
                e.FindPropertyRelative("_scale").vector2Value = Vector2.one;
                e.FindPropertyRelative("_overrideFeatures").boolValue = true;

                // 默认所有维度都 None（index=0），只给当前特征对应维度赋值
                e.FindPropertyRelative("_color").enumValueIndex    = (feat.dim == FeatureDimension.Color)    ? feat.val : 0;
                e.FindPropertyRelative("_shape").enumValueIndex    = (feat.dim == FeatureDimension.Shape)    ? feat.val : 0;
                e.FindPropertyRelative("_material").enumValueIndex = (feat.dim == FeatureDimension.Material) ? feat.val : 0;
                e.FindPropertyRelative("_texture").enumValueIndex  = (feat.dim == FeatureDimension.Texture)  ? feat.val : 0;
                e.FindPropertyRelative("_sound").enumValueIndex    = 0; // Sound=None

                e.FindPropertyRelative("_overrideSprite").boolValue = false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ld);
            return ld;
        }

        // ──────────────────────────────────────────────────────────────
        //  关卡2 子场景 LevelData
        // ──────────────────────────────────────────────────────────────

        private static LevelData BuildSubLevelData(
            string fileName, string levelName, ItemDatabase db, LevelConfig levelCfg,
            (string id, int fi)[] subItems)
        {
            var path = LevelsDir + "/" + fileName + ".asset";
            var ld = LoadOrCreate<LevelData>(path);
            var so = new SerializedObject(ld);

            so.FindProperty("_levelName").stringValue = levelName;
            so.FindProperty("_itemDatabase").objectReferenceValue = db;
            if (levelCfg != null)
            {
                so.FindProperty("_levelConfig").objectReferenceValue = levelCfg;
            }

            // 玩家出生在下方中央
            so.FindProperty("_playerSpawn").vector2Value = new Vector2(0f, -3.5f);

            // 子场景物品：4 个，1 行横排，间距 2.2
            var items = so.FindProperty("_items");
            items.ClearArray();

            for (int i = 0; i < subItems.Length; i++)
            {
                var (itemId, fi) = subItems[i];
                var feat = Features8[fi];

                float x = (i - (subItems.Length - 1) * 0.5f) * 2.2f;
                float y = 1.5f;

                items.InsertArrayElementAtIndex(i);
                var e = items.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("_itemId").stringValue = itemId;
                e.FindPropertyRelative("_position").vector2Value = new Vector2(x, y);
                e.FindPropertyRelative("_rotationZ").floatValue = 0f;
                e.FindPropertyRelative("_scale").vector2Value = Vector2.one;
                e.FindPropertyRelative("_overrideFeatures").boolValue = true;

                e.FindPropertyRelative("_color").enumValueIndex    = (feat.dim == FeatureDimension.Color)    ? feat.val : 0;
                e.FindPropertyRelative("_shape").enumValueIndex    = (feat.dim == FeatureDimension.Shape)    ? feat.val : 0;
                e.FindPropertyRelative("_material").enumValueIndex = (feat.dim == FeatureDimension.Material) ? feat.val : 0;
                e.FindPropertyRelative("_texture").enumValueIndex  = (feat.dim == FeatureDimension.Texture)  ? feat.val : 0;
                e.FindPropertyRelative("_sound").enumValueIndex    = 0;

                e.FindPropertyRelative("_overrideSprite").boolValue = false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ld);
            return ld;
        }

        // ──────────────────────────────────────────────────────────────
        //  LevelSequence
        // ──────────────────────────────────────────────────────────────

        private static LevelSequence BuildLevelSequence(
            LevelData level1, LevelData[] subs, Sprite square, Sprite[] backgrounds)
        {
            var path = LevelsDir + "/DemoTwoLevelFlow_Sequence.asset";
            var seq = LoadOrCreate<LevelSequence>(path);
            var so = new SerializedObject(seq);
            var entries = so.FindProperty("_entries");
            entries.ClearArray();

            // entry[0]：关卡1，Level1Select，门 EnterLevel2
            AddEntry(entries, 0, level1,
                kind: LevelKind.Level1Select, doorKind: DoorKind.EnterLevel2,
                doorSpawn: new Vector2(4f, -4f),
                doorSprite: square,
                doorPrompt: "按 E 进入第二关",
                background: GetBackground(backgrounds, 0));

            // entry[1..N]：5 个子场景，Level2Sub。左右门由 GameManager 按索引程序化生成（首场景无左门、末场景无右门），
            // doorKind 字段对子场景不再被读取，仅占位。
            for (int i = 0; i < subs.Length; i++)
            {
                AddEntry(entries, i + 1, subs[i],
                    kind: LevelKind.Level2Sub, doorKind: DoorKind.SwitchSubSceneNext,
                    doorSpawn: new Vector2(6f, -3.5f),
                    doorSprite: square,
                    doorPrompt: "按 E 切换场景",
                    background: GetBackground(backgrounds, i + 1));
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seq);
            return seq;
        }

        private static void AddEntry(
            SerializedProperty entries, int idx, LevelData level,
            LevelKind kind, DoorKind doorKind,
            Vector2 doorSpawn, Sprite doorSprite, string doorPrompt, Sprite background)
        {
            entries.InsertArrayElementAtIndex(idx);
            var entry = entries.GetArrayElementAtIndex(idx);

            entry.FindPropertyRelative("_level").objectReferenceValue = level;
            entry.FindPropertyRelative("_kind").enumValueIndex = (int)kind;
            entry.FindPropertyRelative("_doorKind").enumValueIndex = (int)doorKind;

            var door = entry.FindPropertyRelative("_door");
            door.FindPropertyRelative("_spawn").vector2Value       = doorSpawn;
            door.FindPropertyRelative("_sprite").objectReferenceValue = doorSprite;
            door.FindPropertyRelative("_prompt").stringValue       = doorPrompt;

            var bg = entry.FindPropertyRelative("_background");
            if (bg != null)
            {
                bg.objectReferenceValue = background;
            }
        }

        private static Sprite GetBackground(Sprite[] backgrounds, int index)
        {
            return backgrounds != null && index >= 0 && index < backgrounds.Length ? backgrounds[index] : null;
        }

        // ──────────────────────────────────────────────────────────────
        //  接线到 Bootstrap.unity
        // ──────────────────────────────────────────────────────────────

        private static bool WireBootstrapScene(LevelSequence sequence)
        {
            return WireBootstrapScene(sequence, "两关卡 Demo 数据");
        }

        private static bool WireBootstrapScene(LevelSequence sequence, string label)
        {
            if (sequence == null)
            {
                Debug.LogWarning($"[TwoLevelFlowDemoSetup] {label} 为空，跳过接线。");
                return false;
            }

            if (!File.Exists(BootstrapScene))
            {
                Debug.LogWarning($"[TwoLevelFlowDemoSetup] 找不到 Bootstrap.unity：{BootstrapScene}，跳过接线。请先运行「生成可运行装配」。");
                return false;
            }

            // 确保当前活动场景有路径（防 Single 弹保存框）
            var active = SceneManager.GetActiveScene();
            string tempActive = null;
            if (string.IsNullOrEmpty(active.path))
            {
                tempActive = ResDir + "/__temp_two_level_flow.unity";
                EditorSceneManager.SaveScene(active, tempActive);
            }

            var bootstrapScene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Additive);
            bool wired = false;

            foreach (var root in bootstrapScene.GetRootGameObjects())
            {
                var gm = root.GetComponent<GameManager>();
                if (gm != null && !wired)
                {
                    var soGm = new SerializedObject(gm);
                    var prop = soGm.FindProperty("_sequence");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = sequence;
                        soGm.ApplyModifiedPropertiesWithoutUndo();
                        wired = true;
                        Debug.Log($"[TwoLevelFlowDemoSetup] GameManager._sequence 已接线：{label}。");
                    }
                    else
                    {
                        Debug.LogWarning("[TwoLevelFlowDemoSetup] GameManager 找不到 _sequence 字段。");
                    }
                }

                if (wired)
                {
                    break;
                }
            }

            if (!wired)
            {
                Debug.LogWarning("[TwoLevelFlowDemoSetup] Bootstrap 场景未找到 GameManager，_sequence 未接线。");
            }

            EditorSceneManager.MarkSceneDirty(bootstrapScene);
            EditorSceneManager.SaveScene(bootstrapScene, BootstrapScene);
            EditorSceneManager.CloseScene(bootstrapScene, true);

            if (!string.IsNullOrEmpty(tempActive) && File.Exists(tempActive))
            {
                AssetDatabase.DeleteAsset(tempActive);
            }

            return wired;
        }

        // ──────────────────────────────────────────────────────────────
        //  工具方法
        // ──────────────────────────────────────────────────────────────

        private static ItemDatabase LoadItemDatabase()
        {
            return AssetDatabase.LoadAssetAtPath<ItemDatabase>(ResDir + "/ItemDatabase.asset");
        }

        /// <summary>按文件名加载 Backgrounds/ 下的房间背景 Sprite（缺则告警返回 null）。</summary>
        private static Sprite LoadBg(string fileName)
        {
            var bg = AssetDatabase.LoadAssetAtPath<Sprite>(BgDir + "/" + fileName + ".png");
            if (bg == null)
            {
                Debug.LogWarning($"[TwoLevelFlowDemoSetup] 找不到背景图：{BgDir}/{fileName}.png（该关卡将无背景）。");
            }

            return bg;
        }


        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
