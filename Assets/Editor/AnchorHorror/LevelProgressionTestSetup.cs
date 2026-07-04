// ------------------------------------------------------------
// LevelProgressionTestSetup.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// Desc   : 编辑器工具——一键生成关卡推进 PlayMode 测试内容。
//          幂等（CreateOrLoad），不运行场景，不改现有 GlobalConfig/FeatureDatabase。
//          菜单：Ciga/AnchorHorror/生成关卡推进测试内容
// ------------------------------------------------------------
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 为 ABC-B PlayMode 冒烟生成可通关的关卡推进测试资产，并接线到 Bootstrap.unity。
    /// 测试内容全部放 Assets/Res/AnchorHorror/Test/，不污染正式资产目录。
    /// </summary>
    public static class LevelProgressionTestSetup
    {
        private const string ResDir    = "Assets/Res/AnchorHorror";
        private const string TestDir   = ResDir + "/Test";
        private const string BootstrapScene = ResDir + "/Bootstrap.unity";
        private const string SquareSpritePath = ResDir + "/WhiteSquare.png";

        [MenuItem("Ciga/AnchorHorror/生成关卡推进测试内容")]
        public static void BuildTestContent()
        {
            EnsureFolder(TestDir);

            // 1. 白方块 Sprite（复用已有，否则新建）
            var square = GetOrCreateSquareSprite();

            // 2. ItemDatabase（含 6 个 ItemDefinition，特征覆盖多维，与 InitRoom 候选可匹配）
            var db = BuildItemDatabase(square);

            // 3. 两个 LevelData（各 6 个 PlacedItem，引用同一 LevelConfig）
            var levelCfg = AssetDatabase.LoadAssetAtPath<LevelConfig>(ResDir + "/LevelConfig.asset");
            if (levelCfg == null)
            {
                Debug.LogWarning("[LevelProgressionTest] 找不到 LevelConfig.asset，LevelData._levelConfig 将为空。");
            }

            var level1 = BuildLevelData("TestLevel1", "第一关", db, levelCfg);
            var level2 = BuildLevelData("TestLevel2", "第二关", db, levelCfg);

            // 4. LevelSequence（2 个 Entry）
            var sequence = BuildLevelSequence(level1, level2, square);

            // 5. ResultConfig（三态结算配置）
            var resultCfg = BuildResultConfig();

            // 6. 打开 Bootstrap 场景并接线，然后保存
            WireBootstrapScene(sequence, resultCfg);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LevelProgressionTest] 测试内容已生成并接线到 Bootstrap.unity。可直接按 Play 冒烟。");
        }

        // -------------------------------------------------------
        // Step 2：ItemDatabase
        // -------------------------------------------------------
        private static ItemDatabase BuildItemDatabase(Sprite square)
        {
            var dbPath = TestDir + "/TestItemDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }

            var so = new SerializedObject(db);

            // _fallbackSprite
            var fallback = so.FindProperty("_fallbackSprite");
            if (fallback != null)
            {
                fallback.objectReferenceValue = square;
            }

            // _items：6 个定义，特征与 InitRoom 候选（Red/Round/Wood/Smooth; Blue/Square/Metal/Rough;
            //         Green/Long/Glass/Glossy; Yellow/Flat/Fabric/Matte）直接对应，保证 ExtractTargets 可命中。
            // 额外再覆盖 Brown 材质 Wood 与 White 材质 Glass，增加多样性。
            var items = so.FindProperty("_items");
            items.ClearArray();

            // ItemDefinition 是普通可序列化 class，放在 List<ItemDefinition> 里
            // 枚举值索引与 FeatureColor/Shape/Material/Texture 声明顺序一致（None=0，后续值从 1 起）
            // 读 AnchorHorrorSetup 里 SpawnItem 直接用枚举 cast int，这里同理

            var defs = new[]
            {
                // id            displayName  color(int)  shape(int)  material(int)  texture(int)
                ("item_red_round",    "红圆物",     (int)FeatureColor.Red,    (int)FeatureShape.Round,   (int)FeatureMaterial.Wood,    (int)FeatureTexture.Smooth),
                ("item_blue_square",  "蓝方物",     (int)FeatureColor.Blue,   (int)FeatureShape.Square,  (int)FeatureMaterial.Metal,   (int)FeatureTexture.Rough),
                ("item_green_long",   "绿长物",     (int)FeatureColor.Green,  (int)FeatureShape.Long,    (int)FeatureMaterial.Glass,   (int)FeatureTexture.Glossy),
                ("item_yellow_flat",  "黄扁物",     (int)FeatureColor.Yellow, (int)FeatureShape.Flat,    (int)FeatureMaterial.Fabric,  (int)FeatureTexture.Matte),
                ("item_brown_wood",   "棕木物",     (int)FeatureColor.Brown,  (int)FeatureShape.Round,   (int)FeatureMaterial.Wood,    (int)FeatureTexture.Rough),
                ("item_white_glass",  "白玻物",     (int)FeatureColor.White,  (int)FeatureShape.Long,    (int)FeatureMaterial.Glass,   (int)FeatureTexture.Glossy),
            };

            for (int i = 0; i < defs.Length; i++)
            {
                items.InsertArrayElementAtIndex(i);
                var elem = items.GetArrayElementAtIndex(i);

                SetStr(elem, "_id",          defs[i].Item1);
                SetStr(elem, "_displayName", defs[i].Item2);
                SetObjRef(elem, "_sprite",   square);
                SetInt(elem, "_color",       defs[i].Item3);
                SetInt(elem, "_shape",       defs[i].Item4);
                SetInt(elem, "_material",    defs[i].Item5);
                SetInt(elem, "_texture",     defs[i].Item6);
                SetVec2(elem, "_defaultScale", Vector2.one);
                SetInt(elem, "_collider",    0); // ColliderKind.Box = 0
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
            return db;
        }

        // -------------------------------------------------------
        // Step 3：LevelData（两关共用同一套 ItemDefinition，散布坐标不同）
        // -------------------------------------------------------
        private static LevelData BuildLevelData(string fileName, string levelName,
            ItemDatabase db, LevelConfig levelCfg)
        {
            var path = TestDir + "/" + fileName + ".asset";
            var ld = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (ld == null)
            {
                ld = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(ld, path);
            }

            var so = new SerializedObject(ld);

            SetStrProp(so, "_levelName",    levelName);
            SetObjProp(so, "_itemDatabase", db);
            SetObjProp(so, "_levelConfig",  levelCfg);

            // PlayerSpawn 放在 (0,-3)，位于物品下方空地
            var spawnProp = so.FindProperty("_playerSpawn");
            if (spawnProp != null)
            {
                spawnProp.vector2Value = new Vector2(0f, -3f);
            }

            // 6 个 PlacedItem：itemId 直接对应 ItemDatabase，散布坐标
            // 两关坐标镜像，第二关 x 整体偏移 0.5f，区分视觉
            float xOffset = fileName == "TestLevel1" ? 0f : 0.5f;

            var placed = new[]
            {
                ("item_red_round",   new Vector2(-3f + xOffset,  2f)),
                ("item_blue_square", new Vector2(-1f + xOffset,  2f)),
                ("item_green_long",  new Vector2( 1f + xOffset,  2f)),
                ("item_yellow_flat", new Vector2( 3f + xOffset,  2f)),
                ("item_brown_wood",  new Vector2(-2f + xOffset,  0f)),
                ("item_white_glass", new Vector2( 2f + xOffset,  0f)),
            };

            var itemsProp = so.FindProperty("_items");
            itemsProp.ClearArray();
            for (int i = 0; i < placed.Length; i++)
            {
                itemsProp.InsertArrayElementAtIndex(i);
                var elem = itemsProp.GetArrayElementAtIndex(i);

                SetStr(elem, "_itemId",        placed[i].Item1);
                SetVec2Prop(elem, "_position", placed[i].Item2);
                SetFloatProp(elem, "_rotationZ", 0f);
                SetVec2Prop(elem, "_scale",    Vector2.one);
                // _overrideFeatures = false（使用 ItemDefinition 默认特征），不需显式写
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ld);
            return ld;
        }

        // -------------------------------------------------------
        // Step 4：LevelSequence
        // -------------------------------------------------------
        private static LevelSequence BuildLevelSequence(LevelData level1, LevelData level2, Sprite square)
        {
            var path = TestDir + "/TestLevelSequence.asset";
            var seq = AssetDatabase.LoadAssetAtPath<LevelSequence>(path);
            if (seq == null)
            {
                seq = ScriptableObject.CreateInstance<LevelSequence>();
                AssetDatabase.CreateAsset(seq, path);
            }

            var so = new SerializedObject(seq);
            var entries = so.FindProperty("_entries");
            entries.ClearArray();

            var levels = new[] { level1, level2 };
            var spawns = new[] { new Vector2(3f, -3f), new Vector2(3f, -3f) };
            var prompts = new[] { "按 E 进入下一关", "按 E 进入下一关" };

            for (int i = 0; i < levels.Length; i++)
            {
                entries.InsertArrayElementAtIndex(i);
                var entry = entries.GetArrayElementAtIndex(i);

                var levelProp = entry.FindPropertyRelative("_level");
                if (levelProp != null)
                {
                    levelProp.objectReferenceValue = levels[i];
                }

                var door = entry.FindPropertyRelative("_door");
                if (door != null)
                {
                    var spawnProp = door.FindPropertyRelative("_spawn");
                    if (spawnProp != null)
                    {
                        spawnProp.vector2Value = spawns[i];
                    }

                    var spriteProp = door.FindPropertyRelative("_sprite");
                    if (spriteProp != null)
                    {
                        spriteProp.objectReferenceValue = square;
                    }

                    var promptProp = door.FindPropertyRelative("_prompt");
                    if (promptProp != null)
                    {
                        promptProp.stringValue = prompts[i];
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seq);
            return seq;
        }

        // -------------------------------------------------------
        // Step 5：ResultConfig
        // -------------------------------------------------------
        private static ResultConfig BuildResultConfig()
        {
            var path = TestDir + "/TestResultConfig.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<ResultConfig>(path);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<ResultConfig>();
                AssetDatabase.CreateAsset(path: path, asset: cfg);
            }

            var so = new SerializedObject(cfg);

            // SubClear：等待过门，R/Esc 均关闭（防误按）
            WriteResultEntry(so, "_subClear",
                title:          "本层已锚定",
                color:          new Color(0.7f, 1f, 0.7f),
                hint:           "走到门按 E 前往下一层",
                respondsRestart: false,
                respondsMenu:   false);

            // Victory：末关通关，Esc 返回主菜单
            WriteResultEntry(so, "_victory",
                title:          "全部通关",
                color:          new Color(1f, 0.95f, 0.5f),
                hint:           "按 Esc 返回主菜单",
                respondsRestart: false,
                respondsMenu:   true);

            // Fail：理智崩溃，R 重开 Esc 返回
            WriteResultEntry(so, "_fail",
                title:          "理智崩溃",
                color:          new Color(1f, 0.4f, 0.4f),
                hint:           "按 R 重开  Esc 返回菜单",
                respondsRestart: true,
                respondsMenu:   true);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        private static void WriteResultEntry(SerializedObject so, string fieldName,
            string title, Color color, string hint, bool respondsRestart, bool respondsMenu)
        {
            var entry = so.FindProperty(fieldName);
            if (entry == null)
            {
                Debug.LogWarning($"[LevelProgressionTest] ResultConfig 找不到属性 {fieldName}");
                return;
            }

            var titleProp = entry.FindPropertyRelative("_title");
            if (titleProp != null)
            {
                titleProp.stringValue = title;
            }

            var colorProp = entry.FindPropertyRelative("_color");
            if (colorProp != null)
            {
                colorProp.colorValue = color;
            }

            var hintProp = entry.FindPropertyRelative("_hint");
            if (hintProp != null)
            {
                hintProp.stringValue = hint;
            }

            var rrProp = entry.FindPropertyRelative("_respondsRestart");
            if (rrProp != null)
            {
                rrProp.boolValue = respondsRestart;
            }

            var rmProp = entry.FindPropertyRelative("_respondsMenu");
            if (rmProp != null)
            {
                rmProp.boolValue = respondsMenu;
            }
        }

        // -------------------------------------------------------
        // Step 6：打开 Bootstrap 接线，保存
        // -------------------------------------------------------
        private static void WireBootstrapScene(LevelSequence sequence, ResultConfig resultCfg)
        {
            // 确保当前活动场景有路径（防止 Single 模式弹保存框），参照 AnchorHorrorSetup 范式
            var active = SceneManager.GetActiveScene();
            string tempActive = null;
            if (string.IsNullOrEmpty(active.path))
            {
                tempActive = ResDir + "/__temp_lp_active.unity";
                EditorSceneManager.SaveScene(active, tempActive);
            }

            // 以 Additive 模式打开 Bootstrap（不影响当前活动场景，不弹保存框）
            if (!System.IO.File.Exists(BootstrapScene))
            {
                Debug.LogWarning($"[LevelProgressionTest] 找不到 Bootstrap.unity：{BootstrapScene}，跳过接线。");
                CleanupTemp(tempActive);
                return;
            }

            var bootstrapScene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Additive);

            bool wiredSequence  = false;
            bool wiredResultCfg = false;

            foreach (var root in bootstrapScene.GetRootGameObjects())
            {
                // GameManager：接 _sequence
                var gm = root.GetComponent<GameManager>();
                if (gm != null && !wiredSequence)
                {
                    var soGm = new SerializedObject(gm);
                    var prop = soGm.FindProperty("_sequence");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = sequence;
                        soGm.ApplyModifiedPropertiesWithoutUndo();
                        wiredSequence = true;
                    }
                    else
                    {
                        Debug.LogWarning("[LevelProgressionTest] GameManager 找不到 _sequence 字段。");
                    }
                }

                // ResultScreen：接 _resultConfig（在 AnchorHorror 根对象上）
                var rs = root.GetComponent<ResultScreen>();
                if (rs != null && !wiredResultCfg)
                {
                    var soRs = new SerializedObject(rs);
                    var prop = soRs.FindProperty("_resultConfig");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = resultCfg;
                        soRs.ApplyModifiedPropertiesWithoutUndo();
                        wiredResultCfg = true;
                    }
                    else
                    {
                        Debug.LogWarning("[LevelProgressionTest] ResultScreen 找不到 _resultConfig 字段。");
                    }
                }

                if (wiredSequence && wiredResultCfg)
                {
                    break;
                }
            }

            if (!wiredSequence)
            {
                Debug.LogWarning("[LevelProgressionTest] Bootstrap 场景未找到 GameManager，_sequence 未接线。");
            }

            if (!wiredResultCfg)
            {
                Debug.LogWarning("[LevelProgressionTest] Bootstrap 场景未找到 ResultScreen，_resultConfig 未接线。");
            }

            EditorSceneManager.MarkSceneDirty(bootstrapScene);
            EditorSceneManager.SaveScene(bootstrapScene, BootstrapScene);
            EditorSceneManager.CloseScene(bootstrapScene, true);

            CleanupTemp(tempActive);
        }

        private static void CleanupTemp(string tempPath)
        {
            if (!string.IsNullOrEmpty(tempPath) && System.IO.File.Exists(tempPath))
            {
                AssetDatabase.DeleteAsset(tempPath);
            }
        }

        // -------------------------------------------------------
        // Sprite 工具（复用 / 新建）
        // -------------------------------------------------------
        private static Sprite GetOrCreateSquareSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (existing != null)
            {
                return existing;
            }

            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color32[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(SquareSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(SquareSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SquareSpritePath);
            importer.textureType    = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode     = FilterMode.Point;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        }

        // -------------------------------------------------------
        // EnsureFolder（递归）
        // -------------------------------------------------------
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf   = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        // -------------------------------------------------------
        // 序列化辅助：操作 SerializedProperty（在 List 元素 / SO 上）
        // -------------------------------------------------------

        // 在 SO 顶层属性上设字符串
        private static void SetStrProp(SerializedObject so, string prop, string val)
        {
            var p = so.FindProperty(prop);
            if (p != null)
            {
                p.stringValue = val;
            }
        }

        // 在 SO 顶层属性上设对象引用
        private static void SetObjProp(SerializedObject so, string prop, Object val)
        {
            var p = so.FindProperty(prop);
            if (p != null)
            {
                p.objectReferenceValue = val;
            }
        }

        // 在 List 元素（SerializedProperty）上设字符串子属性
        private static void SetStr(SerializedProperty elem, string rel, string val)
        {
            var p = elem.FindPropertyRelative(rel);
            if (p != null)
            {
                p.stringValue = val;
            }
        }

        // 在 List 元素上设对象引用子属性
        private static void SetObjRef(SerializedProperty elem, string rel, Object val)
        {
            var p = elem.FindPropertyRelative(rel);
            if (p != null)
            {
                p.objectReferenceValue = val;
            }
        }

        // 在 List 元素上设 int（枚举 enumValueIndex）子属性
        private static void SetInt(SerializedProperty elem, string rel, int val)
        {
            var p = elem.FindPropertyRelative(rel);
            if (p != null)
            {
                p.enumValueIndex = val;
            }
        }

        // 在 List 元素上设 Vector2 子属性
        private static void SetVec2(SerializedProperty elem, string rel, Vector2 val)
        {
            var p = elem.FindPropertyRelative(rel);
            if (p != null)
            {
                p.vector2Value = val;
            }
        }

        // 在 List 元素（PlacedItem）上直接设 Vector2（_position/_scale 为直接字段）
        private static void SetVec2Prop(SerializedProperty elem, string rel, Vector2 val)
        {
            var p = elem.FindPropertyRelative(rel);
            if (p != null)
            {
                p.vector2Value = val;
            }
        }

        // 在 List 元素上设 float 子属性
        private static void SetFloatProp(SerializedProperty elem, string rel, float val)
        {
            var p = elem.FindPropertyRelative(rel);
            if (p != null)
            {
                p.floatValue = val;
            }
        }
    }
}
