// ------------------------------------------------------------
// LevelPreviewSession.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 编辑器预览会话：管理场景内预览根的整个生命周期。
    /// 调用方（LevelEditorWindow）负责订阅 playModeStateChanged 并在进 Play 前调 Cleanup。
    /// </summary>
    internal sealed class LevelPreviewSession
    {
        private const string PreviewRootName = "__LevelEditorPreviewRoot";

        private GameObject _root;

        // 当前加载的关卡（PlaceAt 时用于获取 fallback Sprite）
        private LevelData _currentLevel;

        /// <summary>预览根存在且曾被修改（粗粒度脏检测）。Window 可直接置 true 标脏。</summary>
        public bool IsDirty { get; internal set; }

        // -------- 公开 API --------

        /// <summary>
        /// 从 <paramref name="level"/> 数据展开预览：先 Cleanup，再建预览根，
        /// 遍历 Items 用 ItemFactory 装配每个物品并注册 Undo。
        /// itemId 查不到时记录警告并跳过（防悬空崩溃）。
        /// </summary>
        public void LoadFrom(LevelData level)
        {
            Cleanup();

            _currentLevel = level;

            if (level == null)
            {
                return;
            }

            if (level.ItemDatabase == null)
            {
                Debug.LogWarning("[LevelPreviewSession] LevelData 的 ItemDatabase 为 null，无法展开预览。");
                return;
            }

            _root = new GameObject(PreviewRootName);
            // DontSaveInBuild：预览根不进构建，但 Undo/gizmo/选中均正常（严禁 HideAndDontSave）。
            _root.hideFlags = HideFlags.DontSaveInBuild;
            Undo.RegisterCreatedObjectUndo(_root, "创建预览根");

            var db = level.ItemDatabase;
            var fallback = db.FallbackSprite;
            var items = level.Items;

            for (int i = 0; i < items.Count; i++)
            {
                var placed = items[i];
                if (placed == null)
                {
                    continue;
                }

                if (!db.TryGetById(placed.ItemId, out var def))
                {
                    Debug.LogWarning($"[LevelPreviewSession] itemId '{placed.ItemId}' 在 ItemDatabase 中不存在（悬空），已跳过。");
                    continue;
                }

                var go = ItemFactory.Create(def, placed, fallback, _root.transform);
                go.hideFlags = HideFlags.DontSaveInBuild;
                AttachItemRef(go, def.Id);
                Undo.RegisterCreatedObjectUndo(go, $"预览物品 {def.DisplayName}");
            }

            IsDirty = false;
        }

        /// <summary>
        /// 在场景世界坐标 <paramref name="worldPos"/> 放置一个以 <paramref name="def"/> 默认值构建的预览物体。
        /// 使用 def 默认特征与缩放（未覆盖）；位置设为 worldPos，父节点为预览根。
        /// </summary>
        public void PlaceAt(ItemDefinition def, Vector3 worldPos)
        {
            if (def == null)
            {
                return;
            }

            EnsureRoot();

            // 默认 PlacedItem：_overrideFeatures/_overrideSprite 皆 false → ItemFactory 走 def 默认特征/美术。
            // position/scale 由下方直接设 transform 决定，无需反射注入 placed（去掉反射脆弱性）。
            var placed = new PlacedItem();
            Sprite fallback = _currentLevel?.ItemDatabase?.FallbackSprite;
            var go = ItemFactory.Create(def, placed, fallback, _root.transform);

            // 直接以世界坐标落点 + 定义默认缩放（预览根不在原点时也准确）。
            go.transform.position = worldPos;
            go.transform.localScale = new Vector3(def.DefaultScale.x, def.DefaultScale.y, 1f);
            go.hideFlags = HideFlags.DontSaveInBuild;

            AttachItemRef(go, def.Id);
            Undo.RegisterCreatedObjectUndo(go, $"放置物品 {def.DisplayName}");

            IsDirty = true;
        }

        /// <summary>
        /// 把预览根下所有子物体回写进 <paramref name="level"/> 的 _items 列表。
        /// 读取顺序：transform → FeatureTag 四枚举属性 → SpriteRenderer.sprite。
        /// 与 def 默认值比较决定 override 开关。
        /// 操作全部走 Undo + SetDirty + SaveAssets。
        /// </summary>
        public void WriteBack(LevelData level)
        {
            if (level == null || _root == null)
            {
                return;
            }

            var db = level.ItemDatabase;
            var so = new SerializedObject(level);
            var itemsProp = so.FindProperty("_items");

            if (itemsProp == null)
            {
                Debug.LogWarning("[LevelPreviewSession] LevelData 无 _items 序列化属性，回写中止。");
                return;
            }

            // 清空旧列表。所有改动经下方 so.ApplyModifiedProperties() 统一登记 Undo；
            // 切勿再叠 Undo.RecordObject —— 两套 Undo 路径混用会使数组写入脱离 Undo 批次而回退失效。
            itemsProp.ClearArray();

            int childCount = _root.transform.childCount;
            int writeIndex = 0;
            for (int i = 0; i < childCount; i++)
            {
                var child = _root.transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var itemRef = child.GetComponent<LevelEditorItemRef>();
                if (itemRef == null || string.IsNullOrEmpty(itemRef.ItemId))
                {
                    Debug.LogWarning($"[LevelPreviewSession] 子物体 '{child.name}' 缺少 LevelEditorItemRef，已跳过。");
                    continue;
                }

                // 读当前 Transform（world position → 存为 Vector2；预览根在原点时等于局部坐标）
                var pos = (Vector2)child.position;
                float rotZ = child.eulerAngles.z;
                var scale = new Vector2(child.localScale.x, child.localScale.y);

                // 读 FeatureTag 四枚举属性
                var tag = child.GetComponent<FeatureTag>();
                FeatureColor color = tag != null ? tag.Color : default;
                FeatureShape shape = tag != null ? tag.Shape : default;
                FeatureMaterial material = tag != null ? tag.Material : default;
                FeatureTexture texture = tag != null ? tag.Texture : default;

                // 读 SpriteRenderer.sprite
                var sr = child.GetComponent<SpriteRenderer>();
                Sprite sprite = sr != null ? sr.sprite : null;

                // 与 def 默认比较决定 override 开关
                bool overrideFeatures = false;
                bool overrideSprite = false;

                if (db != null && db.TryGetById(itemRef.ItemId, out var def))
                {
                    overrideFeatures = color != def.Color || shape != def.Shape ||
                                       material != def.Material || texture != def.Texture;
                    overrideSprite = sprite != def.Sprite;
                }
                else
                {
                    // def 悬空：保守写 override=true 保留编辑值
                    overrideFeatures = true;
                    overrideSprite = sprite != null;
                }

                // 写入 SerializedProperty 数组元素
                itemsProp.InsertArrayElementAtIndex(writeIndex);
                var elem = itemsProp.GetArrayElementAtIndex(writeIndex);
                writeIndex++;

                elem.FindPropertyRelative("_itemId").stringValue = itemRef.ItemId;
                elem.FindPropertyRelative("_position").vector2Value = pos;
                elem.FindPropertyRelative("_rotationZ").floatValue = rotZ;
                elem.FindPropertyRelative("_scale").vector2Value = scale;
                elem.FindPropertyRelative("_overrideFeatures").boolValue = overrideFeatures;
                elem.FindPropertyRelative("_color").enumValueIndex = (int)color;
                elem.FindPropertyRelative("_shape").enumValueIndex = (int)shape;
                elem.FindPropertyRelative("_material").enumValueIndex = (int)material;
                elem.FindPropertyRelative("_texture").enumValueIndex = (int)texture;
                elem.FindPropertyRelative("_overrideSprite").boolValue = overrideSprite;
                elem.FindPropertyRelative("_sprite").objectReferenceValue = sprite;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();

            IsDirty = false;
        }

        /// <summary>
        /// 销毁预览根（及其所有子物体）。退出/切关/关窗/进 Play 前必调。
        /// 幂等：_root 为 null 时无操作。
        /// </summary>
        public void Cleanup()
        {
            if (_root != null)
            {
                Undo.DestroyObjectImmediate(_root);
                _root = null;
            }

            _currentLevel = null;
            IsDirty = false;
        }

        // -------- 私有工具 --------

        private void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject(PreviewRootName);
            _root.hideFlags = HideFlags.DontSaveInBuild;
            Undo.RegisterCreatedObjectUndo(_root, "创建预览根");
        }

        private static void AttachItemRef(GameObject go, string itemId)
        {
            var itemRef = go.AddComponent<LevelEditorItemRef>();
            itemRef.ItemId = itemId;
        }
    }
}
