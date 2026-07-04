// ------------------------------------------------------------
// LevelEditorDataDrivenTests.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// SC-8：数据驱动核心链路 EditMode 测试
//   断言①：Spawn 返回的 FeatureTag 数量 = 有效 PlacedItem 数（查不到的 itemId 被跳过）
//   断言②：每个生成物品四维特征 = OverrideFeatures 时取覆盖值，否则取定义默认值
//   断言③：LevelFeatureRegistry.Scan(tags) 后 CountOf 计数正确且不含关卡外特征
// ------------------------------------------------------------
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.Tests
{
    /// <summary>
    /// SC-8 数据驱动核心链路 EditMode 回归测试。
    /// 用反射向 [SerializeField] private 字段注入测试数据，不修改任何业务实现。
    /// </summary>
    public class LevelEditorDataDrivenTests
    {
        // ------------------------------------------------------------------ 常量

        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        // ------------------------------------------------------------------ 字段

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private GameObject _levelRoot;
        private ItemDatabase _db;
        private LevelData _levelData;

        // ------------------------------------------------------------------ 三个 ItemDefinition 的 ID 约定

        private const string IdA = "item_A";   // OverrideFeatures=false  → 使用定义默认值
        private const string IdB = "item_B";   // OverrideFeatures=true   → 使用关卡覆盖值
        private const string IdC = "item_C";   // 第三个物品，四维全不同
        private const string IdMissing = "item_MISSING"; // 在库中不存在

        // 定义默认四维（IdA）
        private static readonly FeatureColor DefA_Color = FeatureColor.Red;
        private static readonly FeatureShape DefA_Shape = FeatureShape.Round;
        private static readonly FeatureMaterial DefA_Material = FeatureMaterial.Wood;
        private static readonly FeatureTexture DefA_Texture = FeatureTexture.Smooth;

        // 定义默认四维（IdB）
        private static readonly FeatureColor DefB_Color = FeatureColor.Blue;
        private static readonly FeatureShape DefB_Shape = FeatureShape.Square;
        private static readonly FeatureMaterial DefB_Material = FeatureMaterial.Metal;
        private static readonly FeatureTexture DefB_Texture = FeatureTexture.Rough;

        // PlacedItem 对 IdB 的覆盖四维
        private static readonly FeatureColor OvrB_Color = FeatureColor.Green;
        private static readonly FeatureShape OvrB_Shape = FeatureShape.Long;
        private static readonly FeatureMaterial OvrB_Material = FeatureMaterial.Glass;
        private static readonly FeatureTexture OvrB_Texture = FeatureTexture.Glossy;

        // 定义默认四维（IdC）
        private static readonly FeatureColor DefC_Color = FeatureColor.Yellow;
        private static readonly FeatureShape DefC_Shape = FeatureShape.Flat;
        private static readonly FeatureMaterial DefC_Material = FeatureMaterial.Fabric;
        private static readonly FeatureTexture DefC_Texture = FeatureTexture.Matte;

        // ------------------------------------------------------------------ SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            // 构建 ItemDatabase（ScriptableObject，用 SerializedObject 写入字段）
            _db = ScriptableObject.CreateInstance<ItemDatabase>();

            var defA = MakeItemDefinition(IdA, "Item A", DefA_Color, DefA_Shape, DefA_Material, DefA_Texture, ColliderKind.Box);
            var defB = MakeItemDefinition(IdB, "Item B", DefB_Color, DefB_Shape, DefB_Material, DefB_Texture, ColliderKind.Circle);
            var defC = MakeItemDefinition(IdC, "Item C", DefC_Color, DefC_Shape, DefC_Material, DefC_Texture, ColliderKind.Box);

            var dbSo = new SerializedObject(_db);
            var itemsProp = dbSo.FindProperty("_items");
            itemsProp.arraySize = 3;

            // SerializedObject 无法直接写 [Serializable] class 内部字段；
            // ItemDefinition 是普通 [Serializable] class，走反射注入到 List<ItemDefinition>
            dbSo.ApplyModifiedPropertiesWithoutUndo();

            // 直接反射写 _items 私有字段（List<ItemDefinition>）
            var itemsField = typeof(ItemDatabase).GetField("_items", PrivateInstance);
            Assert.IsNotNull(itemsField, "_items 字段应存在于 ItemDatabase");
            itemsField.SetValue(_db, new List<ItemDefinition> { defA, defB, defC });

            // 构建 LevelData
            _levelData = ScriptableObject.CreateInstance<LevelData>();

            // PlacedItem A：OverrideFeatures=false → 用定义默认值
            var placedA = MakePlacedItem(IdA, new Vector2(1f, 0f), overrideFeatures: false,
                FeatureColor.None, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None);

            // PlacedItem B：OverrideFeatures=true → 用覆盖值
            var placedB = MakePlacedItem(IdB, new Vector2(2f, 0f), overrideFeatures: true,
                OvrB_Color, OvrB_Shape, OvrB_Material, OvrB_Texture);

            // PlacedItem C：OverrideFeatures=false → 用定义默认值
            var placedC = MakePlacedItem(IdC, new Vector2(3f, 0f), overrideFeatures: false,
                FeatureColor.None, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None);

            // PlacedItem Missing：在库中不存在 → 应被 LogWarning 跳过
            var placedMissing = MakePlacedItem(IdMissing, new Vector2(4f, 0f), overrideFeatures: false,
                FeatureColor.None, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None);

            // 写入 LevelData._items 和 LevelData._itemDatabase
            var ldItemsField = typeof(LevelData).GetField("_items", PrivateInstance);
            Assert.IsNotNull(ldItemsField, "_items 字段应存在于 LevelData");
            ldItemsField.SetValue(_levelData, new List<PlacedItem> { placedA, placedB, placedC, placedMissing });

            var ldDbField = typeof(LevelData).GetField("_itemDatabase", PrivateInstance);
            Assert.IsNotNull(ldDbField, "_itemDatabase 字段应存在于 LevelData");
            ldDbField.SetValue(_levelData, _db);

            // 关卡根节点
            _levelRoot = new GameObject("LevelRoot");
            _spawned.Add(_levelRoot);
        }

        [TearDown]
        public void TearDown()
        {
            // 销毁所有 Spawn 出来的子对象与根节点
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }
            _spawned.Clear();

            if (_db != null)
            {
                Object.DestroyImmediate(_db);
                _db = null;
            }
            if (_levelData != null)
            {
                Object.DestroyImmediate(_levelData);
                _levelData = null;
            }
        }

        // ================================================================== 断言①
        // Spawn 返回的 FeatureTag 数量 = 有效 PlacedItem 数（3 个有效 + 1 个找不到的被跳过）

        [Test]
        public void SC8_Assert1_SpawnCount_EqualsValidPlacedItems_MissingSkipped()
        {
            var tags = LevelSpawner.Spawn(_levelData, _levelRoot.transform);

            // 关卡共 4 个 PlacedItem，其中 1 个 itemId 不在库中 → 期望 3 个生成
            Assert.AreEqual(3, tags.Count,
                "有效 PlacedItem 3 个（A/B/C），缺失 itemId 应被 LogWarning 跳过，返回列表长度应为 3。");
        }

        // ================================================================== 断言②
        // OverrideFeatures=false 时四维 = 定义默认值；OverrideFeatures=true 时四维 = 覆盖值

        [Test]
        public void SC8_Assert2a_NoOverride_FeaturesMatchDefinitionDefaults()
        {
            var tags = LevelSpawner.Spawn(_levelData, _levelRoot.transform);

            // tags[0] 对应 PlacedA（itemId=IdA, OverrideFeatures=false）
            var tagA = FindTagByName(tags, "Item A");
            Assert.IsNotNull(tagA, "应能找到名为 'Item A' 的 FeatureTag");

            Assert.AreEqual(DefA_Color, tagA.Color,
                "PlacedItem A OverrideFeatures=false，Color 应等于定义默认值");
            Assert.AreEqual(DefA_Shape, tagA.Shape,
                "PlacedItem A OverrideFeatures=false，Shape 应等于定义默认值");
            Assert.AreEqual(DefA_Material, tagA.Material,
                "PlacedItem A OverrideFeatures=false，Material 应等于定义默认值");
            Assert.AreEqual(DefA_Texture, tagA.Texture,
                "PlacedItem A OverrideFeatures=false，Texture 应等于定义默认值");
        }

        [Test]
        public void SC8_Assert2b_WithOverride_FeaturesMatchOverrideValues()
        {
            var tags = LevelSpawner.Spawn(_levelData, _levelRoot.transform);

            // tags[1] 对应 PlacedB（itemId=IdB, OverrideFeatures=true）
            var tagB = FindTagByName(tags, "Item B");
            Assert.IsNotNull(tagB, "应能找到名为 'Item B' 的 FeatureTag");

            Assert.AreEqual(OvrB_Color, tagB.Color,
                "PlacedItem B OverrideFeatures=true，Color 应等于关卡覆盖值");
            Assert.AreEqual(OvrB_Shape, tagB.Shape,
                "PlacedItem B OverrideFeatures=true，Shape 应等于关卡覆盖值");
            Assert.AreEqual(OvrB_Material, tagB.Material,
                "PlacedItem B OverrideFeatures=true，Material 应等于关卡覆盖值");
            Assert.AreEqual(OvrB_Texture, tagB.Texture,
                "PlacedItem B OverrideFeatures=true，Texture 应等于关卡覆盖值");
        }

        [Test]
        public void SC8_Assert2c_NoOverride_FeaturesMatchDefinitionDefaults_ItemC()
        {
            var tags = LevelSpawner.Spawn(_levelData, _levelRoot.transform);

            var tagC = FindTagByName(tags, "Item C");
            Assert.IsNotNull(tagC, "应能找到名为 'Item C' 的 FeatureTag");

            Assert.AreEqual(DefC_Color, tagC.Color,
                "PlacedItem C OverrideFeatures=false，Color 应等于定义默认值");
            Assert.AreEqual(DefC_Shape, tagC.Shape,
                "PlacedItem C OverrideFeatures=false，Shape 应等于定义默认值");
            Assert.AreEqual(DefC_Material, tagC.Material,
                "PlacedItem C OverrideFeatures=false，Material 应等于定义默认值");
            Assert.AreEqual(DefC_Texture, tagC.Texture,
                "PlacedItem C OverrideFeatures=false，Texture 应等于定义默认值");
        }

        // ================================================================== 断言③
        // LevelFeatureRegistry.Scan(tags) 后 CountOf 计数正确且不含关卡外特征

        [Test]
        public void SC8_Assert3_RegistryScan_CountOf_CorrectAndIsolated()
        {
            var tags = LevelSpawner.Spawn(_levelData, _levelRoot.transform);
            Assert.AreEqual(3, tags.Count, "前置条件：应有 3 个有效 tag");

            var registry = new LevelFeatureRegistry();
            registry.Scan(tags);

            // --- 验证关卡内特征计数 ---

            // Item A：Color=Red, Shape=Round, Material=Wood, Texture=Smooth（各 1 个）
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Color, (int)DefA_Color)),
                "Color.Red 在关卡内应计数 1（来自 Item A）");
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Shape, (int)DefA_Shape)),
                "Shape.Round 在关卡内应计数 1（来自 Item A）");
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Material, (int)DefA_Material)),
                "Material.Wood 在关卡内应计数 1（来自 Item A）");
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Texture, (int)DefA_Texture)),
                "Texture.Smooth 在关卡内应计数 1（来自 Item A）");

            // Item B（OverrideFeatures=true）：Color=Green, Shape=Long, Material=Glass, Texture=Glossy（各 1 个）
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Color, (int)OvrB_Color)),
                "Color.Green 在关卡内应计数 1（来自 Item B 覆盖值）");
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Shape, (int)OvrB_Shape)),
                "Shape.Long 在关卡内应计数 1（来自 Item B 覆盖值）");
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Material, (int)OvrB_Material)),
                "Material.Glass 在关卡内应计数 1（来自 Item B 覆盖值）");
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Texture, (int)OvrB_Texture)),
                "Texture.Glossy 在关卡内应计数 1（来自 Item B 覆盖值）");

            // Item C：Color=Yellow, Shape=Flat, Material=Fabric, Texture=Matte（各 1 个）
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Color, (int)DefC_Color)),
                "Color.Yellow 在关卡内应计数 1（来自 Item C）");
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Shape, (int)DefC_Shape)),
                "Shape.Flat 在关卡内应计数 1（来自 Item C）");
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Material, (int)DefC_Material)),
                "Material.Fabric 在关卡内应计数 1（来自 Item C）");
            Assert.AreEqual(1, registry.CountOf(new FeatureUnit(FeatureDimension.Texture, (int)DefC_Texture)),
                "Texture.Matte 在关卡内应计数 1（来自 Item C）");

            // --- 验证隔离性：不含关卡外特征 ---
            // IdB 的定义默认值（Blue/Square/Metal/Rough）因 OverrideFeatures=true 被覆盖，不应出现在 registry
            Assert.AreEqual(0, registry.CountOf(new FeatureUnit(FeatureDimension.Color, (int)DefB_Color)),
                "Color.Blue（Item B 定义默认值）被覆盖，不应出现在 registry（隔离性）");
            Assert.AreEqual(0, registry.CountOf(new FeatureUnit(FeatureDimension.Shape, (int)DefB_Shape)),
                "Shape.Square（Item B 定义默认值）被覆盖，不应出现在 registry（隔离性）");
            Assert.AreEqual(0, registry.CountOf(new FeatureUnit(FeatureDimension.Material, (int)DefB_Material)),
                "Material.Metal（Item B 定义默认值）被覆盖，不应出现在 registry（隔离性）");
            Assert.AreEqual(0, registry.CountOf(new FeatureUnit(FeatureDimension.Texture, (int)DefB_Texture)),
                "Texture.Rough（Item B 定义默认值）被覆盖，不应出现在 registry（隔离性）");

            // Missing itemId 完全不在关卡内，其 None 特征不应计入（IsNone 时 CountOf 恒返回 0）
            Assert.AreEqual(0, registry.CountOf(new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.None)),
                "None 特征不应被 registry 计数");

            // 不在任何 PlacedItem 中出现的关卡外特征（如 Black）计数应为 0
            Assert.AreEqual(0, registry.CountOf(new FeatureUnit(FeatureDimension.Color, (int)FeatureColor.Black)),
                "Color.Black 不在关卡内，CountOf 应为 0（隔离性）");
        }

        // ================================================================== 辅助：空 / 无效输入边界

        [Test]
        public void SC8_Boundary_NullLevelData_ReturnsEmptyList()
        {
            var tags = LevelSpawner.Spawn(null, _levelRoot.transform);
            Assert.IsNotNull(tags, "Spawn(null) 不应返回 null");
            Assert.AreEqual(0, tags.Count, "LevelData 为 null 时应返回空列表");
        }

        [Test]
        public void SC8_Boundary_NullItemDatabase_ReturnsEmptyList()
        {
            // 构建一个 _itemDatabase=null 的 LevelData
            var emptyLevel = ScriptableObject.CreateInstance<LevelData>();
            var ldItemsField = typeof(LevelData).GetField("_items", PrivateInstance);
            ldItemsField.SetValue(emptyLevel, new List<PlacedItem>
            {
                MakePlacedItem(IdA, Vector2.zero, false,
                    FeatureColor.None, FeatureShape.None, FeatureMaterial.None, FeatureTexture.None)
            });
            // _itemDatabase 保持默认 null

            var tags = LevelSpawner.Spawn(emptyLevel, _levelRoot.transform);
            Assert.IsNotNull(tags, "Spawn（无 DB）不应返回 null");
            Assert.AreEqual(0, tags.Count, "ItemDatabase 为 null 时应返回空列表");

            Object.DestroyImmediate(emptyLevel);
        }

        // ================================================================== helpers

        /// <summary>用反射构造 ItemDefinition（[Serializable] 普通类，无 ScriptableObject）。</summary>
        private static ItemDefinition MakeItemDefinition(
            string id, string displayName,
            FeatureColor color, FeatureShape shape, FeatureMaterial material, FeatureTexture texture,
            ColliderKind collider)
        {
            var def = new ItemDefinition();
            var type = typeof(ItemDefinition);
            SetPrivate(def, type, "_id", id);
            SetPrivate(def, type, "_displayName", displayName);
            SetPrivate(def, type, "_sprite", null);
            SetPrivate(def, type, "_color", color);
            SetPrivate(def, type, "_shape", shape);
            SetPrivate(def, type, "_material", material);
            SetPrivate(def, type, "_texture", texture);
            SetPrivate(def, type, "_defaultScale", Vector2.one);
            SetPrivate(def, type, "_collider", collider);
            return def;
        }

        /// <summary>用反射构造 PlacedItem（[Serializable] 普通类）。</summary>
        private static PlacedItem MakePlacedItem(
            string itemId, Vector2 position, bool overrideFeatures,
            FeatureColor color, FeatureShape shape, FeatureMaterial material, FeatureTexture texture)
        {
            var placed = new PlacedItem();
            var type = typeof(PlacedItem);
            SetPrivate(placed, type, "_itemId", itemId);
            SetPrivate(placed, type, "_position", position);
            SetPrivate(placed, type, "_rotationZ", 0f);
            SetPrivate(placed, type, "_scale", Vector2.one);
            SetPrivate(placed, type, "_overrideFeatures", overrideFeatures);
            SetPrivate(placed, type, "_color", color);
            SetPrivate(placed, type, "_shape", shape);
            SetPrivate(placed, type, "_material", material);
            SetPrivate(placed, type, "_texture", texture);
            SetPrivate(placed, type, "_overrideSprite", false);
            SetPrivate(placed, type, "_sprite", null);
            return placed;
        }

        private static void SetPrivate(object obj, System.Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, PrivateInstance);
            Assert.IsNotNull(field, $"字段 '{fieldName}' 未找到于 {type.Name}，请核对字段名");
            field.SetValue(obj, value);
        }

        /// <summary>在返回的 tags 列表中按 GameObject.name 查找 FeatureTag。</summary>
        private static FeatureTag FindTagByName(IReadOnlyList<FeatureTag> tags, string name)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] != null && tags[i].gameObject.name == name)
                {
                    return tags[i];
                }
            }
            return null;
        }
    }
}
