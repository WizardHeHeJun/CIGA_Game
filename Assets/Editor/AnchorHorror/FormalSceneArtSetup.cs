// ------------------------------------------------------------
// FormalSceneArtSetup.cs
// Author : WizardHeHeJun
// Created: 2026-07-05
// Desc   : 正式场景美术导入 + LevelData/LevelSequence 生成器。
//          从 acts/ciga美术资产 中拷贝背景与交互物件 Default/Active 图，
//          生成整场景对齐 overlay 物件，并把 Formal_Sequence 接到 Bootstrap。
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
    /// 正式场景美术数据生成器：背景与交互物件均为同画布 PNG，运行时放原点即可对齐。
    /// 物件碰撞框由 Default 图 alpha bbox 自动计算；Active/achieve 图用于靠近高亮切换。
    /// </summary>
    public static class FormalSceneArtSetup
    {
        private const string ResDir = "Assets/Res/AnchorHorror";
        private const string ArtDir = ResDir + "/SceneArt";
        private const string LevelsDir = ResDir + "/Levels";
        private const string BootstrapScene = ResDir + "/Bootstrap.unity";
        private const string FormalSequencePath = LevelsDir + "/Formal_Sequence.asset";
        private const string LevelConfigPath = ResDir + "/LevelConfig.asset";
        private const float FallbackSceneWorldHeight = 13f;
        private const byte AlphaThreshold = 8;

        private static readonly string[] ArtRootParts = { "acts", "ciga美术资产", "ciga美术资产" };

        [MenuItem("Ciga/AnchorHorror/生成正式关卡美术数据（并接线）")]
        public static void BuildAllMenu()
        {
            try
            {
                BuildAll();
                EditorUtility.DisplayDialog(
                    "正式关卡美术数据",
                    "已导入 Scene1~Scene6 美术，生成 Formal LevelData/LevelSequence，并接线到 Bootstrap。",
                    "好");
            }
            catch (Exception ex)
            {
                Debug.LogError("[FormalSceneArtSetup] 生成失败：" + ex);
                EditorUtility.DisplayDialog("生成失败", ex.Message, "确定");
            }
        }

        /// <summary>生成正式关卡美术数据并接线 Bootstrap。幂等：已存在资产就地重建。</summary>
        public static void BuildAll()
        {
            EnsureFolder(ArtDir);
            EnsureFolder(LevelsDir);

            var db = EnsureItemDatabase();
            var levelCfg = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelConfigPath);
            var config = AssetDatabase.LoadAssetAtPath<GlobalConfig>(ResDir + "/GlobalConfig.asset");
            float sceneWorldHeight = config != null ? config.SceneWorldHeight : FallbackSceneWorldHeight;

            var bedroomSpec = BedroomSpec();
            var aisleSpec = AisleSpec();
            var livingSpec = LivingRoomSpec();
            var bathroomSpec = BathroomSpec();
            var kitchenSpec = KitchenSpec();
            var utilitySpec = UtilitySpec();

            var level1 = BuildSceneLevel(bedroomSpec, db, levelCfg, sceneWorldHeight);
            var corridor = BuildSceneLevel(aisleSpec, db, levelCfg, sceneWorldHeight);
            var living = BuildSceneLevel(livingSpec, db, levelCfg, sceneWorldHeight);
            var bathroom = BuildSceneLevel(bathroomSpec, db, levelCfg, sceneWorldHeight);
            var kitchen = BuildSceneLevel(kitchenSpec, db, levelCfg, sceneWorldHeight);
            var utility = BuildSceneLevel(utilitySpec, db, levelCfg, sceneWorldHeight);

            var sequence = BuildSequence(
                level1, corridor, new[] { living, bathroom, kitchen, utility },
                bedroomSpec, aisleSpec, new[] { livingSpec, bathroomSpec, kitchenSpec, utilitySpec });
            WireBootstrapScene(sequence);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FormalSceneArtSetup] 已生成正式关卡美术数据并接线 Bootstrap.unity。");
        }

        private static SceneSpec BedroomSpec()
        {
            return new SceneSpec(
                "Formal_Bedroom", "正式关卡1·卧室", "Bedroom", "Scene1_Bedroom_v1", string.Empty,
                "Bedroom_BG.PNG", new Vector2(0f, -4f),
                new[]
                {
                    Obj("bed", "床", "Default/Bedrrom_Bed_Default.PNG", "Active/Bedrrom_Bed_Active.PNG",
                        Feature(FeatureColor.White, FeatureShape.Long, FeatureMaterial.Fabric, FeatureTexture.Soft, FeatureSound.ClothTouch)),
                    // 美术源没画衣柜 Active 图：占位 = Default 提亮 25%（程序生成），美术补图后替换 acts 源 + 删进包旧图重导
                    Obj("drobe", "衣柜", "Default/Bedrrom_Drobe_Default.PNG", "Active/Bedrrom_Drobe_Active.PNG",
                        Feature(FeatureColor.Brown, FeatureShape.Square, FeatureMaterial.Wood, FeatureTexture.Rough, FeatureSound.WoodFriction)),
                    Obj("desk", "梳妆台", "Default/Bedrrom_Desk_Default.PNG", "Active/Bedrrom_Desk_Active..PNG",
                        Feature(FeatureColor.White, FeatureShape.Square, FeatureMaterial.Wood, FeatureTexture.Smooth, FeatureSound.WoodFriction)),
                    Obj("lamp", "台灯", "Default/Bedrrom_Lamp_Default.PNG", "Active/Bedrrom_Lamp_Active..PNG",
                        Feature(FeatureColor.Beige, FeatureShape.Round, FeatureMaterial.Metal, FeatureTexture.SoftLight, FeatureSound.LightHum)),
                    Obj("album", "和母亲的合照", "Default/Bedrrom_Album_Default.PNG", "Active/Bedrrom_Album_Active..PNG",
                        Feature(FeatureColor.Brown, FeatureShape.Square, FeatureMaterial.WoodGlass, FeatureTexture.Smooth, FeatureSound.GlassClink)),
                    Obj("window", "窗户", "Default/Bedrrom_Window_Default.PNG", "Active/Bedrrom_Window_Active..PNG",
                        Feature(FeatureColor.Transparent, FeatureShape.Square, FeatureMaterial.Glass, FeatureTexture.Reflective, FeatureSound.Ticking)),
                    Obj("carpet", "地毯", "Default/Bedrrom_Carpet_Default.PNG", "Active/Bedrrom_Carpet_Active..PNG",
                        Feature(FeatureColor.LightGray, FeatureShape.Long, FeatureMaterial.Fabric, FeatureTexture.Soft, FeatureSound.ClothTouch)),
                    Obj("chair", "椅子", "Default/Bedrrom_Chair_Default.PNG", "Active/Bedrrom_Chair_Active..PNG",
                        Feature(FeatureColor.White, FeatureShape.Irregular, FeatureMaterial.Wood, FeatureTexture.Smooth, FeatureSound.WoodFriction),
                        itemId: "chair_single"),
                });
        }

        private static SceneSpec AisleSpec()
        {
            return new SceneSpec(
                "Formal_Aisle", "正式关卡2·走廊", "Aisle", "Scene2_Aisle_v1", string.Empty,
                "Aisle_BG.PNG", new Vector2(0f, -3.5f),
                new[]
                {
                    Obj("door_a", "门A", "Default/Aisle_DoorA_Default.PNG", "Active/Aisle_DoorA_Active.PNG",
                        Feature(FeatureColor.DarkBrown, FeatureShape.Square, FeatureMaterial.Wood, FeatureTexture.Peeling, FeatureSound.WoodFriction),
                        visualOnly: true),
                    Obj("door_b", "门B", "Default/Aisle_DoorB_Default.PNG", "Active/Aisle_DoorB_Active.PNG",
                        Feature(FeatureColor.White, FeatureShape.Square, FeatureMaterial.Wood, FeatureTexture.Smooth, FeatureSound.WoodFriction),
                        visualOnly: true),
                    Obj("carpet", "地毯", "Default/Aisle_Carpet_Default.PNG", "Active/Aisle_Carpet_Active.PNG",
                        Feature(FeatureColor.DarkRed, FeatureShape.Long, FeatureMaterial.Fabric, FeatureTexture.Fiber, FeatureSound.ClothTouch)),
                    Obj("lamp", "墙灯", "Default/Aisle_Lamp_Default.PNG", "Active/Aisle_Lamp_Active.PNG",
                        Feature(FeatureColor.WarmYellow, FeatureShape.Round, FeatureMaterial.GlassMetal, FeatureTexture.Glossy, FeatureSound.LightHum)),
                    Obj("key", "钥匙", "Default/Aisle_Key_Default.PNG", "Active/Aisle_Key_Active.PNG",
                        Feature(FeatureColor.Gold, FeatureShape.Irregular, FeatureMaterial.Metal, FeatureTexture.Smooth, FeatureSound.MetalMechanical)),
                    Obj("stair", "楼梯", "Default/Aisle_Stair_Default.PNG", "Active/Aisle_Stair_Active.PNG",
                        Feature(FeatureColor.Gray, FeatureShape.Long, FeatureMaterial.Stone, FeatureTexture.Rough, FeatureSound.WoodFriction)),
                });
        }

        private static SceneSpec LivingRoomSpec()
        {
            return new SceneSpec(
                "Formal_LivingRoom", "正式关卡2·客厅", "LivingRoom", "Scene3_LivingRom_v1", string.Empty,
                "LivingRoom_BG.PNG", new Vector2(0f, -3.5f),
                new[]
                {
                    Obj("sofa", "沙发", "Default/LivingRoom_Sofa_Default.PNG", "Active/LivingRoom_Sofa_Active.PNG",
                        Feature(FeatureColor.DarkGray, FeatureShape.Long, FeatureMaterial.Fabric, FeatureTexture.Soft, FeatureSound.ClothTouch)),
                    Obj("tv", "电视机", "Default/LivingRoom_TV_Default.PNG", "Active/LivingRoom_TV_Active.PNG",
                        Feature(FeatureColor.Black, FeatureShape.Square, FeatureMaterial.Glass, FeatureTexture.Smooth, FeatureSound.MetalMechanical)),
                    Obj("frame", "相框", "Default/LivingRoom_Frame_Default.PNG", "Active/LivingRoom_Frame_Active.PNG",
                        Feature(FeatureColor.Brown, FeatureShape.Square, FeatureMaterial.Wood, FeatureTexture.Smooth, FeatureSound.GlassClink)),
                    // 美术源把茶几的 Active 图错命名成了 LivingRoom_Bed_Active.PNG（目视确认同一件家具），此处直接引用
                    Obj("table", "茶几", "Default/LivingRoom_Table_Default.PNG", "Active/LivingRoom_Bed_Active.PNG",
                        Feature(FeatureColor.DarkBrown, FeatureShape.Square, FeatureMaterial.Wood, FeatureTexture.Worn, FeatureSound.WoodFriction)),
                    Obj("lamp", "落地灯", "Default/LivingRoom_Lamp_Default.PNG", "Active/LivingRoom_TLamp_Active.PNG",
                        Feature(FeatureColor.Beige, FeatureShape.Cone, FeatureMaterial.Fabric, FeatureTexture.SoftLight, FeatureSound.LightHum)),
                    Obj("toy_box", "玩具箱", "Default/LivingRoom_ToyBox_Default.PNG", "Active/LivingRoom_ToyBox_Active.PNG",
                        Feature(FeatureColor.Colorful, FeatureShape.Square, FeatureMaterial.Plastic, FeatureTexture.Rough, FeatureSound.PlasticClick)),
                });
        }

        private static SceneSpec BathroomSpec()
        {
            return new SceneSpec(
                "Formal_Bathroom", "正式关卡2·浴室", "Bathroom", "Scene5_bathroom", string.Empty,
                "bathroom bg.png", new Vector2(0f, -3.5f),
                new[]
                {
                    Obj("toilet", "马桶", "Default/toilet.png", "achieve/toilet.png",
                        Feature(FeatureColor.White, FeatureShape.Round, FeatureMaterial.Ceramic, FeatureTexture.Smooth, FeatureSound.Ticking)),
                    Obj("laundry", "洗衣机", "Default/laundry.png", "achieve/laundry.png",
                        Feature(FeatureColor.White, FeatureShape.Round, FeatureMaterial.Metal, FeatureTexture.Matte, FeatureSound.MetalMechanical)),
                    Obj("mirror", "镜子", "Default/mirror.png", "achieve/mirror.png",
                        Feature(FeatureColor.Silver, FeatureShape.Square, FeatureMaterial.Glass, FeatureTexture.Reflective, FeatureSound.GlassClink)),
                    Obj("sink", "洗手台", "Default/sink.png", "achieve/sink.png",
                        Feature(FeatureColor.White, FeatureShape.Square, FeatureMaterial.Ceramic, FeatureTexture.Smooth, FeatureSound.Ticking)),
                    Obj("towel", "毛巾", "Default/towel.png", "achieve/towel.png",
                        Feature(FeatureColor.Blue, FeatureShape.Long, FeatureMaterial.Fabric, FeatureTexture.Soft, FeatureSound.ClothTouch)),
                    Obj("outfall", "排水口", "Default/outfall.png", "achieve/outfall.png",
                        Feature(FeatureColor.Black, FeatureShape.Round, FeatureMaterial.Metal, FeatureTexture.Wet, FeatureSound.Ticking)),
                });
        }

        private static SceneSpec KitchenSpec()
        {
            return new SceneSpec(
                "Formal_Kitchen", "正式关卡2·厨房", "Kitchen", "Scene4_kitchen", string.Empty,
                "KITCHEN bg.png", new Vector2(0f, -3.5f),
                new[]
                {
                    Obj("cooker", "电饭煲", "Default/cooker.png", "achieve/cooker.png",
                        Feature(FeatureColor.White, FeatureShape.Square, FeatureMaterial.MetalPlastic, FeatureTexture.Smooth, FeatureSound.MetalMechanical)),
                    Obj("tap", "水龙头", "Default/tap.png", "achieve/tap.png",
                        Feature(FeatureColor.Silver, FeatureShape.Curved, FeatureMaterial.Metal, FeatureTexture.Reflective, FeatureSound.Ticking)),
                    Obj("ice_box", "冰箱", "Default/ice box.png", "achieve/ice box.png",
                        Feature(FeatureColor.White, FeatureShape.Square, FeatureMaterial.Metal, FeatureTexture.Matte, FeatureSound.MetalMechanical)),
                    Obj("case", "碗柜", "Default/case.png", "achieve/case.png",
                        Feature(FeatureColor.Brown, FeatureShape.Square, FeatureMaterial.Wood, FeatureTexture.Rough, FeatureSound.WoodFriction)),
                    Obj("table", "餐桌", "Default/table.png", "achieve/table.png",
                        Feature(FeatureColor.DarkBrown, FeatureShape.Square, FeatureMaterial.Wood, FeatureTexture.Worn, FeatureSound.WoodFriction)),
                    Obj("window", "窗户", "Default/window.png", "achieve/window.png",
                        Feature(FeatureColor.Transparent, FeatureShape.Square, FeatureMaterial.Glass, FeatureTexture.Smooth, FeatureSound.GlassClink)),
                    Obj("clock", "钟表", "Default/clock.png", "achieve/clock.png",
                        Feature(FeatureColor.Gold, FeatureShape.Round, FeatureMaterial.Metal, FeatureTexture.Scaled, FeatureSound.Ticking)),
                    Obj("wash_sink", "水槽", "Default/wash sink.png", "achieve/wash sink.png",
                        Feature(FeatureColor.Silver, FeatureShape.Square, FeatureMaterial.Metal, FeatureTexture.Wet, FeatureSound.Ticking)),
                });
        }

        private static SceneSpec UtilitySpec()
        {
            return new SceneSpec(
                "Formal_Utility", "正式关卡2·杂物间", "Utility", "Scene6_utility_room", "utility room",
                "untility room bg.png", new Vector2(0f, -3.5f),
                new[]
                {
                    Obj("cooker", "旧电饭煲", "Default/cooker.png", "Active/cooker.png",
                        Feature(FeatureColor.Beige, FeatureShape.Square, FeatureMaterial.Metal, FeatureTexture.PaintPeeled, FeatureSound.MetalMechanical)),
                    Obj("clock", "破钟", "Default/clock.png", "Active/clock.png",
                        Feature(FeatureColor.Black, FeatureShape.Round, FeatureMaterial.Metal, FeatureTexture.Cracked, FeatureSound.Ticking)),
                    Obj("box", "纸箱", "Default/box.png", "Active/box.png",
                        Feature(FeatureColor.Brown, FeatureShape.Square, FeatureMaterial.Paper, FeatureTexture.Broken, FeatureSound.WoodFriction)),
                    Obj("mirror", "镜子碎片", "Default/mirror.png", "Active/mirror.png",
                        Feature(FeatureColor.Silver, FeatureShape.Irregular, FeatureMaterial.Glass, FeatureTexture.Fissure, FeatureSound.GlassClink)),
                    Obj("toy", "坏玩具", "Default/toy.png", "Active/toy.png",
                        Feature(FeatureColor.Colorful, FeatureShape.Irregular, FeatureMaterial.Plastic, FeatureTexture.Scratched, FeatureSound.PlasticClick)),
                    Obj("chair", "折叠椅", "Default/chair.png", "Active/chair.png",
                        Feature(FeatureColor.Black, FeatureShape.Irregular, FeatureMaterial.Metal, FeatureTexture.Scratched, FeatureSound.MetalMechanical)),
                });
        }

        private static ArtObjectSpec Obj(
            string id,
            string displayName,
            string defaultRel,
            string activeRel,
            FeatureSpec feature,
            string itemId = null,
            bool visualOnly = false)
        {
            return new ArtObjectSpec(id, displayName, defaultRel, activeRel, feature, itemId, visualOnly);
        }

        private static FeatureSpec Feature(
            FeatureColor color,
            FeatureShape shape,
            FeatureMaterial material,
            FeatureTexture texture,
            FeatureSound sound)
        {
            return new FeatureSpec(color, shape, material, texture, sound);
        }

        private static LevelData BuildSceneLevel(SceneSpec spec, ItemDatabase db, LevelConfig levelCfg, float sceneWorldHeight)
        {
            var sceneTargetDir = ArtDir + "/" + spec.Key;
            EnsureFolder(sceneTargetDir);

            string srcRoot = SourcePath(spec.SourceSceneDir, spec.NestedDir);
            float ppu = DeterminePixelsPerUnit(Path.Combine(srcRoot, spec.BackgroundRel), sceneWorldHeight);
            spec.Background = CopyImportArtSprite(
                Path.Combine(srcRoot, spec.BackgroundRel), sceneTargetDir, spec.Key + "_BG.png", ppu);

            var level = LoadOrCreate<LevelData>(LevelsDir + "/" + spec.AssetName + ".asset");
            var so = new SerializedObject(level);
            so.FindProperty("_levelName").stringValue = spec.LevelName;
            so.FindProperty("_itemDatabase").objectReferenceValue = db;
            so.FindProperty("_levelConfig").objectReferenceValue = levelCfg;
            so.FindProperty("_playerSpawn").vector2Value = spec.PlayerSpawn;

            var items = so.FindProperty("_items");
            items.ClearArray();

            for (int i = 0; i < spec.Objects.Length; i++)
            {
                var obj = spec.Objects[i];
                string defaultSource = string.IsNullOrEmpty(obj.DefaultRel)
                    ? null
                    : Path.Combine(srcRoot, obj.DefaultRel);
                string defaultTarget = spec.Key + "_" + obj.Id + "_Default.png";
                var defaultSprite = string.IsNullOrEmpty(defaultSource)
                    ? null
                    : CopyImportArtSprite(defaultSource, sceneTargetDir, defaultTarget, ppu);

                Sprite activeSprite = null;
                if (!string.IsNullOrEmpty(obj.ActiveRel))
                {
                    activeSprite = CopyImportArtSprite(
                        Path.Combine(srcRoot, obj.ActiveRel), sceneTargetDir,
                        spec.Key + "_" + obj.Id + "_Active.png", ppu);
                }

                var bounds = CalculateAlphaBounds(defaultSource, ppu, defaultSprite);

                items.InsertArrayElementAtIndex(i);
                var e = items.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("_itemId").stringValue = obj.ItemId;
                e.FindPropertyRelative("_position").vector2Value = Vector2.zero;
                e.FindPropertyRelative("_rotationZ").floatValue = 0f;
                e.FindPropertyRelative("_scale").vector2Value = Vector2.one;
                // visualOnly 条目也保留策划表特征值做数据留档（运行时不读：ItemFactory 对 VisualOnly 提前 return，
                // GameManager 汇总可获得特征也跳过），仅 _overrideFeatures=false 表示不参与玩法。
                var feature = obj.Feature;
                e.FindPropertyRelative("_overrideFeatures").boolValue = !obj.VisualOnly;
                SetFeatureProperties(e, feature);
                e.FindPropertyRelative("_overrideSprite").boolValue = defaultSprite != null;
                e.FindPropertyRelative("_sprite").objectReferenceValue = defaultSprite;
                e.FindPropertyRelative("_activeSprite").objectReferenceValue = activeSprite;
                e.FindPropertyRelative("_alignWithBackground").boolValue = true;
                e.FindPropertyRelative("_colliderOffset").vector2Value = bounds.offset;
                e.FindPropertyRelative("_colliderSize").vector2Value = bounds.size;
                e.FindPropertyRelative("_visualOnly").boolValue = obj.VisualOnly;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssetIfDirty(level);
            return level;
        }

        private static void SetFeatureProperties(SerializedProperty e, FeatureSpec feature)
        {
            e.FindPropertyRelative("_color").enumValueIndex = (int)feature.Color;
            e.FindPropertyRelative("_shape").enumValueIndex = (int)feature.Shape;
            e.FindPropertyRelative("_material").enumValueIndex = (int)feature.Material;
            e.FindPropertyRelative("_texture").enumValueIndex = (int)feature.Texture;
            e.FindPropertyRelative("_sound").enumValueIndex = (int)feature.Sound;
        }

        private static LevelSequence BuildSequence(
            LevelData level1,
            LevelData corridor,
            LevelData[] rooms,
            SceneSpec level1Spec,
            SceneSpec corridorSpec,
            SceneSpec[] roomSpecs)
        {
            var seq = LoadOrCreate<LevelSequence>(FormalSequencePath);
            var so = new SerializedObject(seq);
            var entries = so.FindProperty("_entries");
            entries.ClearArray();

            AddEntry(entries, 0, level1, LevelKind.Level1Select, DoorKind.EnterLevel2,
                new Vector2(4f, -4f), null, null, "选满后自动进入第二关", level1Spec.Background);
            AddEntry(entries, 1, corridor, LevelKind.Level2Sub, DoorKind.EnterRoom1,
                Vector2.zero, null, null, "按 E 进入房间", corridorSpec.Background);

            for (int i = 0; i < rooms.Length; i++)
            {
                AddEntry(entries, i + 2, rooms[i], LevelKind.Level2Sub, DoorKind.ReturnToCorridor,
                    new Vector2(0f, -3.5f), null, null, "按 E 返回走廊", roomSpecs[i].Background);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(seq);
            AssetDatabase.SaveAssetIfDirty(seq);
            return seq;
        }

        private static void AddEntry(
            SerializedProperty entries,
            int index,
            LevelData level,
            LevelKind kind,
            DoorKind doorKind,
            Vector2 doorSpawn,
            Sprite doorSprite,
            Sprite doorActiveSprite,
            string doorPrompt,
            Sprite background)
        {
            entries.InsertArrayElementAtIndex(index);
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("_level").objectReferenceValue = level;
            entry.FindPropertyRelative("_kind").enumValueIndex = (int)kind;
            entry.FindPropertyRelative("_doorKind").enumValueIndex = (int)doorKind;

            var door = entry.FindPropertyRelative("_door");
            door.FindPropertyRelative("_spawn").vector2Value = doorSpawn;
            door.FindPropertyRelative("_sprite").objectReferenceValue = doorSprite;
            door.FindPropertyRelative("_activeSprite").objectReferenceValue = doorActiveSprite;
            door.FindPropertyRelative("_prompt").stringValue = doorPrompt;

            entry.FindPropertyRelative("_background").objectReferenceValue = background;
        }

        private static Sprite CopyImportArtSprite(string sourceAbs, string targetDir, string targetFile, float ppu)
        {
            string assetPath = targetDir + "/" + targetFile;
            if (!File.Exists(sourceAbs))
            {
                Debug.LogWarning("[FormalSceneArtSetup] 找不到美术源：" + sourceAbs + "（" + targetFile + " 将缺图）。");
                return null;
            }

            if (!File.Exists(assetPath))
            {
                File.Copy(sourceAbs, assetPath, true);
                AssetDatabase.ImportAsset(assetPath);
            }

            ConfigureArtSprite(assetPath, ppu);
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void ConfigureArtSprite(string assetPath, float ppu)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, ppu))
            {
                importer.spritePixelsPerUnit = ppu;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (importer.maxTextureSize < 4096)
            {
                importer.maxTextureSize = 4096;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static (Vector2 offset, Vector2 size) CalculateAlphaBounds(string sourceAbs, float ppu, Sprite fallback)
        {
            if (!File.Exists(sourceAbs))
            {
                return (Vector2.zero, fallback != null ? (Vector2)fallback.bounds.size : Vector2.one);
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(sourceAbs)))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return (Vector2.zero, fallback != null ? (Vector2)fallback.bounds.size : Vector2.one);
            }

            var pixels = tex.GetPixels32();
            int width = tex.width;
            int height = tex.height;
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= AlphaThreshold)
                    {
                        continue;
                    }

                    if (x < minX) { minX = x; }
                    if (y < minY) { minY = y; }
                    if (x > maxX) { maxX = x; }
                    if (y > maxY) { maxY = y; }
                }
            }

            UnityEngine.Object.DestroyImmediate(tex);
            if (maxX < minX || maxY < minY)
            {
                return (Vector2.zero, fallback != null ? (Vector2)fallback.bounds.size : Vector2.one);
            }

            // 物件图 alpha bbox 覆盖近整张画布 = 美术导出多半把白底拍平了（丢透明通道），
            // 生成出的碰撞框会占满全场景、渲染也会盖住背景——提前告警拦住这类源图缺陷。
            if (maxX - minX + 1 >= width * 0.95f && maxY - minY + 1 >= height * 0.95f)
            {
                Debug.LogWarning($"[FormalSceneArtSetup] 物件图 alpha 区域覆盖≥95% 画布，疑似源图未保留透明通道：{sourceAbs}");
            }

            float centerX = (minX + maxX + 1) * 0.5f;
            float centerY = (minY + maxY + 1) * 0.5f;
            var offset = new Vector2((centerX - width * 0.5f) / ppu, (centerY - height * 0.5f) / ppu);
            var size = new Vector2((maxX - minX + 1) / ppu, (maxY - minY + 1) / ppu);
            return (offset, size);
        }

        private static float DeterminePixelsPerUnit(string backgroundSourceAbs, float sceneWorldHeight)
        {
            if (!File.Exists(backgroundSourceAbs))
            {
                return 100f;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(backgroundSourceAbs)))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return 100f;
            }

            float ppu = sceneWorldHeight > 0.001f ? tex.height / sceneWorldHeight : 100f;
            UnityEngine.Object.DestroyImmediate(tex);
            return ppu;
        }

        private static string SourcePath(string sceneDir, string nestedDir)
        {
            string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Path.Combine(ArtRootParts));
            return string.IsNullOrEmpty(nestedDir)
                ? Path.Combine(root, sceneDir)
                : Path.Combine(root, sceneDir, nestedDir);
        }

        private static ItemDatabase EnsureItemDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemCatalogCodegen.DbPath);
            if (db == null || db.Items.Count == 0)
            {
                ItemCatalogCodegen.ImportAll();
                db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemCatalogCodegen.DbPath);
            }

            return db;
        }

        private static bool WireBootstrapScene(LevelSequence sequence)
        {
            if (sequence == null)
            {
                return false;
            }

            if (!File.Exists(BootstrapScene))
            {
                Debug.LogWarning("[FormalSceneArtSetup] 找不到 Bootstrap.unity：" + BootstrapScene + "，跳过接线。请先运行生成可运行装配。");
                return false;
            }

            var active = SceneManager.GetActiveScene();
            string tempActive = null;
            if (string.IsNullOrEmpty(active.path))
            {
                tempActive = ResDir + "/__temp_formal_scene_art.unity";
                EditorSceneManager.SaveScene(active, tempActive);
            }

            var bootstrapScene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Additive);
            bool wired = false;
            foreach (var root in bootstrapScene.GetRootGameObjects())
            {
                var gm = root.GetComponent<GameManager>();
                if (gm == null || wired)
                {
                    continue;
                }

                var so = new SerializedObject(gm);
                var prop = so.FindProperty("_sequence");
                if (prop != null)
                {
                    prop.objectReferenceValue = sequence;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    wired = true;
                    Debug.Log("[FormalSceneArtSetup] GameManager._sequence 已接线：正式关卡美术数据。");
                }
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

        private readonly struct FeatureSpec
        {
            public FeatureSpec(
                FeatureColor color,
                FeatureShape shape,
                FeatureMaterial material,
                FeatureTexture texture,
                FeatureSound sound)
            {
                Color = color;
                Shape = shape;
                Material = material;
                Texture = texture;
                Sound = sound;
            }

            public FeatureColor Color { get; }
            public FeatureShape Shape { get; }
            public FeatureMaterial Material { get; }
            public FeatureTexture Texture { get; }
            public FeatureSound Sound { get; }
        }

        private sealed class SceneSpec
        {
            public SceneSpec(
                string assetName,
                string levelName,
                string key,
                string sourceSceneDir,
                string nestedDir,
                string backgroundRel,
                Vector2 playerSpawn,
                ArtObjectSpec[] objects)
            {
                AssetName = assetName;
                LevelName = levelName;
                Key = key;
                SourceSceneDir = sourceSceneDir;
                NestedDir = nestedDir;
                BackgroundRel = backgroundRel;
                PlayerSpawn = playerSpawn;
                Objects = objects;
            }

            public string AssetName { get; }
            public string LevelName { get; }
            public string Key { get; }
            public string SourceSceneDir { get; }
            public string NestedDir { get; }
            public string BackgroundRel { get; }
            public Vector2 PlayerSpawn { get; }
            public ArtObjectSpec[] Objects { get; }
            public Sprite Background { get; set; }
        }

        private sealed class ArtObjectSpec
        {
            public ArtObjectSpec(
                string id,
                string displayName,
                string defaultRel,
                string activeRel,
                FeatureSpec feature,
                string itemId,
                bool visualOnly)
            {
                Id = id;
                DisplayName = displayName;
                DefaultRel = defaultRel;
                ActiveRel = activeRel;
                Feature = feature;
                ItemId = itemId;
                VisualOnly = visualOnly;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string DefaultRel { get; }
            public string ActiveRel { get; }
            public FeatureSpec Feature { get; }
            public string ItemId { get; }
            public bool VisualOnly { get; }
        }
    }
}
