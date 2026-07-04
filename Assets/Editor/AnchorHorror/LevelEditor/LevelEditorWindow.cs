// ------------------------------------------------------------
// LevelEditorWindow.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>
    /// 关卡编辑器主窗口：关卡选择/新建、物品调色板、选中实例属性、每关配置区、保存/关闭。
    /// </summary>
    internal sealed class LevelEditorWindow : EditorWindow
    {
        private const string LevelsDir = "Assets/Res/AnchorHorror/Levels";
        private const string PaletteItemSize = "80";

        // -------- 状态 --------
        private LevelData _currentLevel;
        private ItemDefinition _selectedDef;          // 调色板选中（待放置）
        private LevelPreviewSession _session;

        // 选中实例属性面板
        private GameObject _inspectedGo;
        private FeatureTag _inspectedTag;
        private SpriteRenderer _inspectedSr;

        // 每关配置区内嵌 Editor + 复用的每关 SerializedObject（避免每帧 new 造成 GC / Undo 不稳）
        private Editor _levelConfigEditor;
        private SerializedObject _levelSerialized;

        // 滚动区
        private Vector2 _paletteScroll;
        private Vector2 _mainScroll;

        // 关卡列表（FindAssets 缓存，窗口打开/刷新时重建）
        private List<LevelData> _allLevels = new List<LevelData>();

        // 调色板每行格子数
        private const int PaletteCols = 4;
        private const float PaletteThumbSize = 64f;

        // 复用的 GUIContent 标签（避免 OnGUI 每帧 new）
        private static readonly GUIContent _labelName = new GUIContent("关卡名称");
        private static readonly GUIContent _labelSpawn = new GUIContent("玩家出生点");
        private static readonly GUIContent _labelDb = new GUIContent("物品目录 (ItemDatabase)");
        private static readonly GUIContent _labelCfg = new GUIContent("关卡配置 (LevelConfig)");

        // -------- 入口 --------

        [MenuItem("Ciga/AnchorHorror/关卡编辑器")]
        public static void Open()
        {
            var win = GetWindow<LevelEditorWindow>("关卡编辑器");
            win.minSize = new Vector2(380f, 520f);
            win.Show();
        }

        // -------- 生命周期 --------

        private void OnEnable()
        {
            _session = new LevelPreviewSession();
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            RefreshLevelList();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            TryCleanupWithPrompt();
            DestroyEditors();
        }

        // -------- 主 GUI --------

        private void OnGUI()
        {
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            DrawLevelSection();
            EditorGUILayout.Space(4f);

            if (_currentLevel != null)
            {
                DrawPaletteSection();
                EditorGUILayout.Space(4f);
                DrawLevelConfigSection();
                EditorGUILayout.Space(4f);
                DrawInspectedSection();
                EditorGUILayout.Space(8f);
                DrawSaveCloseButtons();
            }

            EditorGUILayout.EndScrollView();
        }

        // -------- 关卡选择区（任务 4.1）--------

        private void DrawLevelSection()
        {
            EditorGUILayout.LabelField("关卡选择", EditorStyles.boldLabel);

            // ObjectField：直接拖拽或 Picker 选 LevelData
            var newLevel = (LevelData)EditorGUILayout.ObjectField("当前关卡", _currentLevel, typeof(LevelData), false);
            if (newLevel != _currentLevel)
            {
                SwitchLevel(newLevel);
            }

            EditorGUILayout.BeginHorizontal();

            // 下拉列表显示工程内所有 LevelData
            if (_allLevels.Count > 0)
            {
                int curIdx = _currentLevel != null ? _allLevels.IndexOf(_currentLevel) : -1;
                var names = new string[_allLevels.Count + 1];
                names[0] = "-- 选择关卡 --";
                for (int i = 0; i < _allLevels.Count; i++)
                {
                    names[i + 1] = _allLevels[i] != null ? _allLevels[i].LevelName : "(无名)";
                }

                int newIdx = EditorGUILayout.Popup(curIdx + 1, names) - 1;
                if (newIdx != curIdx && newIdx >= 0 && newIdx < _allLevels.Count)
                {
                    SwitchLevel(_allLevels[newIdx]);
                }
            }

            if (GUILayout.Button("刷新列表", GUILayout.Width(70f)))
            {
                RefreshLevelList();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("新建关卡"))
            {
                CreateNewLevel();
            }
        }

        private void SwitchLevel(LevelData level)
        {
            if (_currentLevel == level)
            {
                return;
            }

            // 切换前若有未保存改动，提示
            if (_session != null && _session.IsDirty)
            {
                bool save = EditorUtility.DisplayDialog("未保存的改动",
                    $"关卡 '{(_currentLevel != null ? _currentLevel.LevelName : "未命名")}' 有未保存的改动，是否保存？",
                    "保存", "丢弃");
                if (save && _currentLevel != null)
                {
                    _session.WriteBack(_currentLevel);
                }
            }

            _session?.Cleanup();
            DestroyEditors();
            _inspectedGo = null;
            _inspectedTag = null;
            _inspectedSr = null;
            _selectedDef = null;

            _currentLevel = level;
            if (_currentLevel != null)
            {
                _session.LoadFrom(_currentLevel);
                Selection.activeObject = _currentLevel;
            }

            Repaint();
        }

        private void CreateNewLevel()
        {
            EnsureFolder(LevelsDir);
            string path = AssetDatabase.GenerateUniqueAssetPath(LevelsDir + "/NewLevel.asset");
            var asset = CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshLevelList();
            SwitchLevel(asset);
            EditorGUIUtility.PingObject(asset);
        }

        private void RefreshLevelList()
        {
            _allLevels.Clear();
            var guids = AssetDatabase.FindAssets("t:LevelData");
            for (int i = 0; i < guids.Length; i++)
            {
                var p = AssetDatabase.GUIDToAssetPath(guids[i]);
                var ld = AssetDatabase.LoadAssetAtPath<LevelData>(p);
                if (ld != null)
                {
                    _allLevels.Add(ld);
                }
            }
        }

        // -------- 物品调色板（任务 4.2）--------

        private void DrawPaletteSection()
        {
            EditorGUILayout.LabelField("物品调色板", EditorStyles.boldLabel);

            var db = _currentLevel.ItemDatabase;
            if (db == null)
            {
                EditorGUILayout.HelpBox("ItemDatabase 未设置，请在关卡配置区先绑定。", MessageType.Warning);
                return;
            }

            var items = db.Items;
            if (items == null || items.Count == 0)
            {
                EditorGUILayout.HelpBox("ItemDatabase 中无物品定义。", MessageType.Info);
                return;
            }

            if (_selectedDef != null)
            {
                EditorGUILayout.HelpBox($"待放置：{_selectedDef.DisplayName}（点击场景放置，右键取消）", MessageType.Info);
            }

            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll,
                GUILayout.Height(Mathf.Min(items.Count / PaletteCols + 1, 3) * (PaletteThumbSize + 20f) + 8f));

            int col = 0;
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < items.Count; i++)
            {
                var def = items[i];
                if (def == null)
                {
                    continue;
                }

                if (col >= PaletteCols)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    col = 0;
                }

                DrawPaletteItem(def);
                col++;
            }

            // 补空格对齐
            while (col > 0 && col < PaletteCols)
            {
                GUILayout.Space(PaletteThumbSize + 4f);
                col++;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void DrawPaletteItem(ItemDefinition def)
        {
            bool isSelected = _selectedDef == def;
            var style = isSelected ? EditorStyles.selectionRect : GUIStyle.none;

            EditorGUILayout.BeginVertical(style, GUILayout.Width(PaletteThumbSize + 4f));

            // 缩略图
            Texture2D thumb = def.Sprite != null
                ? AssetPreview.GetAssetPreview(def.Sprite)
                : null;

            var thumbRect = GUILayoutUtility.GetRect(PaletteThumbSize, PaletteThumbSize,
                GUILayout.Width(PaletteThumbSize), GUILayout.Height(PaletteThumbSize));

            if (thumb != null)
            {
                GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(thumbRect, new Color(0.3f, 0.3f, 0.3f));
                GUI.Label(thumbRect, "?", EditorStyles.centeredGreyMiniLabel);
            }

            // 按名称按钮选中
            if (GUILayout.Button(def.DisplayName ?? def.Id,
                GUILayout.Width(PaletteThumbSize), GUILayout.Height(16f)))
            {
                _selectedDef = isSelected ? null : def;
                Repaint();
            }

            EditorGUILayout.EndVertical();
        }

        // -------- 每关配置区（任务 4.3）--------

        private void DrawLevelConfigSection()
        {
            EditorGUILayout.LabelField("关卡配置", EditorStyles.boldLabel);

            // 复用成员 SerializedObject（避免每帧 new 造成 GC / Undo 不稳）；目标变化时重建。
            if (_levelSerialized == null || _levelSerialized.targetObject != _currentLevel)
            {
                _levelSerialized?.Dispose();
                _levelSerialized = new SerializedObject(_currentLevel);
            }

            var so = _levelSerialized;
            so.Update();

            var dbProp = so.FindProperty("_itemDatabase");
            var cfgProp = so.FindProperty("_levelConfig");
            var nameProp = so.FindProperty("_levelName");
            var spawnProp = so.FindProperty("_playerSpawn");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(nameProp, _labelName);
            EditorGUILayout.PropertyField(spawnProp, _labelSpawn);
            EditorGUILayout.PropertyField(dbProp, _labelDb);

            if (dbProp.objectReferenceValue != null)
            {
                if (GUILayout.Button("在 Inspector 中编辑物品定义", GUILayout.Height(20f)))
                {
                    Selection.activeObject = dbProp.objectReferenceValue;
                    EditorGUIUtility.PingObject(dbProp.objectReferenceValue);
                }
            }

            EditorGUILayout.PropertyField(cfgProp, _labelCfg);

            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties(); // 自带 Undo 登记，无需再 SetDirty
                // ItemDatabase 变化时刷新调色板
                Repaint();
            }

            // 内嵌 LevelConfig Editor（若有）
            var levelConfig = _currentLevel.LevelConfig;
            if (levelConfig != null)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("关卡配置详情", EditorStyles.miniBoldLabel);

                if (_levelConfigEditor == null || _levelConfigEditor.target != levelConfig)
                {
                    DestroyImmediate(_levelConfigEditor);
                    _levelConfigEditor = Editor.CreateEditor(levelConfig);
                }

                _levelConfigEditor.OnInspectorGUI();
            }
        }

        // -------- 选中实例属性（任务 5.3）--------

        private void DrawInspectedSection()
        {
            // 实时跟踪 Unity Selection
            var sel = Selection.activeGameObject;
            if (sel != _inspectedGo)
            {
                _inspectedGo = sel;
                _inspectedTag = sel != null ? sel.GetComponent<FeatureTag>() : null;
                _inspectedSr = sel != null ? sel.GetComponent<SpriteRenderer>() : null;
            }

            if (_inspectedTag == null)
            {
                return;
            }

            // 确认选中物体在预览根下
            if (!IsUnderPreviewRoot(sel.transform))
            {
                return;
            }

            EditorGUILayout.LabelField("选中实例属性", EditorStyles.boldLabel);

            // 四枚举 Popup
            var newColor = (FeatureColor)EditorGUILayout.EnumPopup("颜色", _inspectedTag.Color);
            var newShape = (FeatureShape)EditorGUILayout.EnumPopup("形状", _inspectedTag.Shape);
            var newMaterial = (FeatureMaterial)EditorGUILayout.EnumPopup("材质", _inspectedTag.Material);
            var newTexture = (FeatureTexture)EditorGUILayout.EnumPopup("纹理", _inspectedTag.Texture);

            bool featureChanged = newColor != _inspectedTag.Color || newShape != _inspectedTag.Shape ||
                                  newMaterial != _inspectedTag.Material || newTexture != _inspectedTag.Texture;

            if (featureChanged)
            {
                Undo.RecordObject(_inspectedTag, "修改物品特征");
                _inspectedTag.Configure(newColor, newShape, newMaterial, newTexture);
                MarkSessionDirty();
                EditorUtility.SetDirty(_inspectedTag);
            }

            // Sprite ObjectField
            if (_inspectedSr != null)
            {
                var newSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", _inspectedSr.sprite, typeof(Sprite), false);
                if (newSprite != _inspectedSr.sprite)
                {
                    Undo.RecordObject(_inspectedSr, "修改物品 Sprite");
                    _inspectedSr.sprite = newSprite;
                    MarkSessionDirty();
                    EditorUtility.SetDirty(_inspectedSr);
                }
            }
        }

        private void MarkSessionDirty()
        {
            if (_session != null)
            {
                _session.IsDirty = true;
            }
        }

        private bool IsUnderPreviewRoot(Transform t)
        {
            while (t != null)
            {
                if (t.name == "__LevelEditorPreviewRoot")
                {
                    return true;
                }

                t = t.parent;
            }

            return false;
        }

        // -------- 保存/关闭按钮 --------

        private void DrawSaveCloseButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _session != null && _session.IsDirty;
            if (GUILayout.Button("保存关卡", GUILayout.Height(28f)))
            {
                _session?.WriteBack(_currentLevel);
            }

            GUI.enabled = true;

            if (GUILayout.Button("关闭编辑", GUILayout.Height(28f)))
            {
                TryCleanupWithPrompt();
                _currentLevel = null;
                DestroyEditors();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        // -------- 场景 GUI（任务 5.2）--------

        private void OnSceneGUI(SceneView sv)
        {
            if (_selectedDef == null || _currentLevel == null)
            {
                return;
            }

            var evt = Event.current;

            // 右键取消放置模式
            if (evt.type == EventType.MouseDown && evt.button == 1)
            {
                _selectedDef = null;
                evt.Use();
                Repaint();
                return;
            }

            // 左键放置
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                // 2D 场景：取 ray.origin（Z=0 平面）
                var worldPos = new Vector3(ray.origin.x, ray.origin.y, 0f);
                _session.PlaceAt(_selectedDef, worldPos);
                evt.Use();
                Repaint();
            }

            // 绘制跟随鼠标的放置预览标签
            Handles.BeginGUI();
            var labelPos = evt.mousePosition + new Vector2(12f, -20f);
            GUI.Label(new Rect(labelPos.x, labelPos.y, 160f, 20f),
                $"放置: {_selectedDef.DisplayName}", EditorStyles.whiteLabel);
            Handles.EndGUI();

            sv.Repaint();
        }

        // -------- PlayMode 钩子（任务 5.5）--------

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 进入 Play 前强制 Cleanup，防止预览根污染运行时物品列表
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _session?.Cleanup();
                _inspectedGo = null;
                _inspectedTag = null;
                _inspectedSr = null;
            }

            // 退出 Play 后若有关卡，重新展开预览
            if (state == PlayModeStateChange.EnteredEditMode && _currentLevel != null)
            {
                _session?.LoadFrom(_currentLevel);
            }
        }

        // -------- 工具方法 --------

        private void TryCleanupWithPrompt()
        {
            if (_session == null)
            {
                return;
            }

            if (_session.IsDirty && _currentLevel != null)
            {
                bool save = EditorUtility.DisplayDialog("未保存的改动",
                    $"关卡 '{_currentLevel.LevelName}' 有未保存的改动，是否保存？",
                    "保存", "丢弃");
                if (save)
                {
                    _session.WriteBack(_currentLevel);
                }
            }

            _session.Cleanup();
        }

        private void DestroyEditors()
        {
            if (_levelConfigEditor != null)
            {
                DestroyImmediate(_levelConfigEditor);
                _levelConfigEditor = null;
            }

            if (_levelSerialized != null)
            {
                _levelSerialized.Dispose();
                _levelSerialized = null;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
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
