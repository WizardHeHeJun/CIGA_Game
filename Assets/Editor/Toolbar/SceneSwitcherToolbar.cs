// ------------------------------------------------------------
// SceneSwitcherToolbar.cs
// Author : WizardHeHeJun
// Created: 2026-07-04
// ------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ciga.EditorTools
{
    /// <summary>
    /// 向 Unity 主工具栏注入「场景切换」下拉按钮，列出 Assets/ 下所有 .unity 场景。
    /// 新建或删除场景后自动刷新列表（监听 EditorApplication.projectChanged）。
    /// </summary>
    [InitializeOnLoad]
    public static class SceneSwitcherToolbar
    {
        private const string ButtonName    = "Ciga_SceneSwitcherButton";
        private const string FormalWireButtonName = "Ciga_FormalWireButton";
        private const string TargetZone    = "ToolbarZoneLeftAlign";
        private const string PromptSaveMenu    = "Tools/SceneSwitcher/切换前提示保存";
        private const string PromptSavePrefKey = "Ciga.SceneSwitcher.PromptSaveBeforeSwitch";

        private static readonly Type ToolbarType =
            typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");

        private static ScriptableObject _toolbar;

        // 缓存场景列表，仅在 projectChanged 后才重建。
        private static readonly List<string> ScenePaths = new();
        private static bool _isDirty = true;

        private static bool PromptSaveBeforeSwitch
        {
            get => EditorPrefs.GetBool(PromptSavePrefKey, true);
            set => EditorPrefs.SetBool(PromptSavePrefKey, value);
        }

        static SceneSwitcherToolbar()
        {
            EditorApplication.update         += TryInject;
            EditorApplication.projectChanged += () => _isDirty = true;
        }

        // ── 菜单：切换前是否提示保存 ──────────────────────────────────────

        [MenuItem(PromptSaveMenu)]
        private static void TogglePromptSave() => PromptSaveBeforeSwitch = !PromptSaveBeforeSwitch;

        [MenuItem(PromptSaveMenu, true)]
        private static bool ValidateTogglePromptSave()
        {
            Menu.SetChecked(PromptSaveMenu, PromptSaveBeforeSwitch);
            return true;
        }

        // ── 工具栏注入 ────────────────────────────────────────────────────

        private static void TryInject()
        {
            if (ToolbarType == null) { EditorApplication.update -= TryInject; return; }

            if (_toolbar == null)
            {
                var found = Resources.FindObjectsOfTypeAll(ToolbarType);
                if (found.Length == 0) return;
                _toolbar = (ScriptableObject)found[0];
            }

            var rootField = ToolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            if (rootField?.GetValue(_toolbar) is not VisualElement root) return;

            var zone = root.Q(TargetZone);
            if (zone == null) return;

            bool hasSceneSwitcher = zone.Q(ButtonName) != null;
            bool hasFormalWire = zone.Q(FormalWireButtonName) != null;
            if (hasSceneSwitcher && hasFormalWire) { EditorApplication.update -= TryInject; return; }

            if (!hasFormalWire)
            {
                var formalWireContainer = new IMGUIContainer(OnFormalWireToolbarGUI) { name = FormalWireButtonName };
                formalWireContainer.style.flexGrow = 0f;
                formalWireContainer.style.marginLeft = 2f;
                formalWireContainer.style.alignSelf = Align.Center;
                zone.Add(formalWireContainer);
            }

            if (!hasSceneSwitcher)
            {
                var container = new IMGUIContainer(OnToolbarGUI) { name = ButtonName };
                container.style.flexGrow  = 0f;
                container.style.marginLeft = 2f;
                container.style.alignSelf  = Align.Center;
                zone.Add(container);
            }

            EditorApplication.update -= TryInject;
        }

        // ── 绘制 ──────────────────────────────────────────────────────────

        private static void OnFormalWireToolbarGUI()
        {
            if (GUILayout.Button("接线正式+编译", EditorStyles.toolbarButton, GUILayout.MinWidth(96f)))
            {
                bool ok = TryWireFormalSequence(out string message);
                if (!ok)
                {
                    EditorUtility.DisplayDialog("接线失败", message, "好");
                    return;
                }

                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation();
                Debug.Log("[SceneSwitcherToolbar] " + message + " 已请求编译验证。");
            }
        }

        private static void OnToolbarGUI()
        {
            if (_isDirty) RebuildSceneList();

            var activeScene = EditorSceneManager.GetActiveScene();
            var label = string.IsNullOrEmpty(activeScene.name)
                ? "场景 ▾"
                : activeScene.name + " ▾";

            if (GUILayout.Button(label, EditorStyles.toolbarDropDown, GUILayout.MinWidth(80f)))
                ShowDropdown();
        }

        private static void ShowDropdown()
        {
            var menu = new GenericMenu();
            var activeScenePath = EditorSceneManager.GetActiveScene().path;

            // 统计重名场景，重名时显示父目录消歧。
            var nameCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var p in ScenePaths)
            {
                var n = Path.GetFileNameWithoutExtension(p);
                nameCount[n] = nameCount.TryGetValue(n, out var c) ? c + 1 : 1;
            }

            foreach (var path in ScenePaths)
            {
                var sceneName = Path.GetFileNameWithoutExtension(path);
                var displayName = nameCount[sceneName] > 1
                    ? $"{sceneName}  ({Path.GetFileName(Path.GetDirectoryName(path))})"
                    : sceneName;

                var captured = path;
                menu.AddItem(
                    new GUIContent(displayName, path),
                    path == activeScenePath,
                    () => OpenScene(captured));
            }

            if (ScenePaths.Count == 0)
                menu.AddDisabledItem(new GUIContent("（无场景）"));

            menu.ShowAsContext();
        }

        // ── 内部工具 ──────────────────────────────────────────────────────

        private static void RebuildSceneList()
        {
            ScenePaths.Clear();
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (var guid in guids)
                ScenePaths.Add(AssetDatabase.GUIDToAssetPath(guid));

            ScenePaths.Sort(StringComparer.Ordinal);
            _isDirty = false;
        }

        private static void OpenScene(string path)
        {
            if (PromptSaveBeforeSwitch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static bool TryWireFormalSequence(out string message)
        {
            const string typeName = "Ciga.AnchorHorror.EditorTools.TwoLevelFlowDemoSetup, Ciga.AnchorHorror.EditorTools";
            var type = Type.GetType(typeName);
            var method = type?.GetMethod("TryWireFormalSequence", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                message = "找不到正式关卡接线方法，请确认 Ciga.AnchorHorror.EditorTools 程序集已编译。";
                return false;
            }

            object[] args = { null };
            bool ok = (bool)method.Invoke(null, args);
            message = args[0] as string ?? string.Empty;
            return ok;
        }
    }
}
