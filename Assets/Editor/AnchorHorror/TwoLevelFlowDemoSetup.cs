// ------------------------------------------------------------
// TwoLevelFlowDemoSetup.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// Desc   : 两关卡流程冒烟测试数据生成器。
//          建 1 关卡1 LevelData（8 物品，每件各带 8 种 distinct 特征之一）+
//          1 个走廊 Hub + 4 个关卡2房间 LevelData（四房间并集覆盖全部 8 种特征，保证可解）+
//          LevelSequence（entry[0]=Level1Select + entry[1]=Corridor + entry[2..5]=Room）。
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
    /// 走廊（entries[1]）：无物品，仅提供四扇门。
    /// 房间1（entries[2]）：F1 F2
    /// 房间2（entries[3]）：F3 F4
    /// 房间3（entries[4]）：F5 F6
    /// 房间4（entries[5]）：F7 F8
    ///
    /// 四房间并集 = {Red,Blue,Green,Round,Square,Wood,Metal,Smooth} = F1..F8（可解证明见上）。
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

        private static readonly (string id, int fi)[] EmptyItems = new (string id, int fi)[0];

        // 房间物品数据：(itemId, featureIndex in Features8[])。4 个房间，并集覆盖全 8 种特征（可解）。
        // 房间1：F1(Red), F2(Blue)
        private static readonly (string id, int fi)[] Room1Items =
        {
            ("chair_wood", 0), // F1=Color.Red
            ("lamp_metal", 1), // F2=Color.Blue
        };

        // 房间2：F3(Green), F4(Round)
        private static readonly (string id, int fi)[] Room2Items =
        {
            ("book_paper", 2), // F3=Color.Green
            ("cup_glass",  3), // F4=Shape.Round
        };

        // 房间3：F5(Square), F6(Wood)
        private static readonly (string id, int fi)[] Room3Items =
        {
            ("clock_metal", 4), // F5=Shape.Square
            ("cloth_red",   5), // F6=Material.Wood
        };

        // 房间4：F7(Metal), F8(Smooth)
        private static readonly (string id, int fi)[] Room4Items =
        {
            ("box_wood",       6), // F7=Material.Metal
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
                    $"  走廊 + 房间1..4 LevelData\n" +
                    $"  LevelSequence（entry[0]=关卡1，entry[1]=走廊，entry[2..5]=房间）\n" +
                    $"  并已接线到 Bootstrap.unity（正式接线被切走）。\n\n" +
                    $"恢复正式内容：菜单「接线正式关卡数据」。",
                    "好");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TwoLevelFlowDemoSetup] 生成失败：{ex}");
                EditorUtility.DisplayDialog("生成失败", ex.Message, "确定");
            }
        }

        /// <summary>一键重建：重建 Bootstrap 装配（默认接正式序列）+ 重建 Demo 数据资产（不抢接线）。</summary>
        [MenuItem("Ciga/AnchorHorror/★ 一键重建全部（装配+Demo数据，接线正式关卡）")]
        public static void RebuildEverything()
        {
            AnchorHorrorSetup.BuildAll();      // 重建 Bootstrap，内部已默认接线正式关卡序列
            BuildAll(wireToBootstrap: false);  // 只重建 Demo 数据资产，不动 Bootstrap 接线
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "一键重建完成",
                "已重建 Bootstrap（接线正式关卡序列）+ 两关卡 Demo 数据资产。\n从 GameMain 场景 Play 即为正式内容。\n要跑 Demo 用菜单「生成两关卡流程 Demo 数据」显式切换。",
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

        /// <summary>
        /// 幂等生成入口：建资产；wireToBootstrap=true 时才把 Demo 序列接到 Bootstrap。
        /// 测试/一键重建走 false——Demo 接线只能经菜单显式发生，防止隐式抢走正式接线（历史坑）。
        /// </summary>
        public static void BuildAll(bool wireToBootstrap = true)
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

            // 1. 建 6 个 LevelData（关卡1 + 走廊 + 4 房间）
            var level1 = BuildLevel1Data(db, levelCfg);
            var corridor = BuildSubLevelData("DemoCorridor", "关卡2-走廊", db, levelCfg, EmptyItems);
            var room1 = BuildSubLevelData("DemoRoom1", "关卡2-房间1", db, levelCfg, Room1Items);
            var room2 = BuildSubLevelData("DemoRoom2", "关卡2-房间2", db, levelCfg, Room2Items);
            var room3 = BuildSubLevelData("DemoRoom3", "关卡2-房间3", db, levelCfg, Room3Items);
            var room4 = BuildSubLevelData("DemoRoom4", "关卡2-房间4", db, levelCfg, Room4Items);

            // 2. 建 LevelSequence，并把 6 张房间背景接到 entry._background（数据驱动，重建自动复现）
            //    映射：关卡1=卧室；走廊=Aisle；房间1..4=起居室/浴室/厨房/杂物间。
            var backgrounds = new[]
            {
                LoadBg("Bedroom"),
                LoadBg("Aisle"),
                LoadBg("LivingRoom"),
                LoadBg("Bathroom"),
                LoadBg("Kitchen"),
                LoadBg("Utility"),
            };
            var seq = BuildLevelSequence(level1, corridor, new[] { room1, room2, room3, room4 }, square, backgrounds);

            // 3. 接线到 Bootstrap（仅显式要求时；测试路径只建资产不动接线）
            if (wireToBootstrap)
            {
                WireBootstrapScene(seq);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[TwoLevelFlowDemoSetup] 已生成两关卡流程 Demo 数据{(wireToBootstrap ? "并接线到 Bootstrap.unity" : "（未动 Bootstrap 接线）")}。\n" +
                "8 种特征：Color.Red / Color.Blue / Color.Green / Shape.Round / Shape.Square / Material.Wood / Material.Metal / Texture.Smooth\n" +
                "Corridor：无物品，仅四门 Hub\n" +
                "Room1覆盖：Red Blue\n" +
                "Room2覆盖：Green Round\n" +
                "Room3覆盖：Square Wood\n" +
                "Room4覆盖：Metal Smooth\n" +
                "四房间并集 = 全 8 种 → 可解。");
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
        //  关卡2 走廊 / 房间 LevelData
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

            // 房间物品：1 行横排，间距 2.2；走廊传空数组则不生成物品。
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
            LevelData level1, LevelData corridor, LevelData[] rooms, Sprite square, Sprite[] backgrounds)
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
                doorActiveSprite: null,
                doorPrompt: "按 E 进入第二关",
                background: GetBackground(backgrounds, 0));

            // entry[1]：走廊 Hub。四扇进入房间的门由 GameManager 程序化生成。
            AddEntry(entries, 1, corridor,
                kind: LevelKind.Level2Sub, doorKind: DoorKind.EnterRoom1,
                doorSpawn: Vector2.zero,
                doorSprite: square,
                doorActiveSprite: null,
                doorPrompt: "按 E 进入房间",
                background: GetBackground(backgrounds, 1));

            // entry[2..5]：四个房间。每个房间仅生成返回走廊门，位置可由 DoorSetting 控制。
            for (int i = 0; i < rooms.Length; i++)
            {
                AddEntry(entries, i + 2, rooms[i],
                    kind: LevelKind.Level2Sub, doorKind: DoorKind.ReturnToCorridor,
                    doorSpawn: new Vector2(0f, -3.5f),
                    doorSprite: square,
                    doorActiveSprite: null,
                    doorPrompt: "按 E 返回走廊",
                    background: GetBackground(backgrounds, i + 2));
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seq);
            return seq;
        }

        private static void AddEntry(
            SerializedProperty entries, int idx, LevelData level,
            LevelKind kind, DoorKind doorKind,
            Vector2 doorSpawn, Sprite doorSprite, Sprite doorActiveSprite, string doorPrompt, Sprite background)
        {
            entries.InsertArrayElementAtIndex(idx);
            var entry = entries.GetArrayElementAtIndex(idx);

            entry.FindPropertyRelative("_level").objectReferenceValue = level;
            entry.FindPropertyRelative("_kind").enumValueIndex = (int)kind;
            entry.FindPropertyRelative("_doorKind").enumValueIndex = (int)doorKind;

            var door = entry.FindPropertyRelative("_door");
            door.FindPropertyRelative("_spawn").vector2Value       = doorSpawn;
            door.FindPropertyRelative("_sprite").objectReferenceValue = doorSprite;
            door.FindPropertyRelative("_activeSprite").objectReferenceValue = doorActiveSprite;
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
