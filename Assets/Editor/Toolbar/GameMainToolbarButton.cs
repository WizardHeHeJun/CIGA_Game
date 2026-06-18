// ------------------------------------------------------------
// GameMainToolbarButton.cs
// Author : WizardHeHeJun
// Created: 2026-06-17
// ------------------------------------------------------------
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ciga.EditorTools
{
    /// <summary>
    /// 向 Unity 主工具栏左侧（播放键左边）注入「GameMain」按钮，点击切换到游戏主入口场景。
    /// 是否在切换前提示保存可通过菜单 Tools/GameMain 开关。纯编辑器工具，不进运行时程序集。
    /// </summary>
    [InitializeOnLoad]
    public static class GameMainToolbarButton
    {
        // 游戏主入口场景。日后主场景改名/迁移，只改这一行。
        private const string MainScenePath = "Assets/Res/Scene/GameMain.unity";
        private const string ButtonName = "Ciga_GameMainButton";

        // 注入到左区（播放键左侧）；想放回右侧改成 "ToolbarZoneRightAlign"。
        private const string TargetZone = "ToolbarZoneLeftAlign";

        // 「切换前提示保存」开关，持久化到 EditorPrefs。
        private const string PromptSaveMenu = "Tools/GameMain/切换前提示保存";
        private const string PromptSavePrefKey = "Ciga.GameMain.PromptSaveBeforeSwitch";

        private static readonly Type ToolbarType =
            typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");

        private static readonly GUIContent ButtonContent =
            new GUIContent("GameMain", "切换到游戏主入口场景（" + MainScenePath + "）");

        private static ScriptableObject _toolbar;

        private static bool PromptSaveBeforeSwitch
        {
            get => EditorPrefs.GetBool(PromptSavePrefKey, true);
            set => EditorPrefs.SetBool(PromptSavePrefKey, value);
        }

        static GameMainToolbarButton()
        {
            // 工具栏在域重载后才构建，轮询直到拿到实例并注入成功。
            EditorApplication.update += TryInject;
        }

        [MenuItem(PromptSaveMenu)]
        private static void TogglePromptSave()
        {
            PromptSaveBeforeSwitch = !PromptSaveBeforeSwitch;
        }

        [MenuItem(PromptSaveMenu, true)]
        private static bool TogglePromptSaveValidate()
        {
            Menu.SetChecked(PromptSaveMenu, PromptSaveBeforeSwitch);
            return true;
        }

        private static void TryInject()
        {
            if (ToolbarType == null)
            {
                EditorApplication.update -= TryInject;
                return;
            }

            if (_toolbar == null)
            {
                var toolbars = Resources.FindObjectsOfTypeAll(ToolbarType);
                if (toolbars.Length == 0)
                {
                    return;
                }

                _toolbar = (ScriptableObject)toolbars[0];
            }

            var rootField = ToolbarType.GetField(
                "m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            if (rootField?.GetValue(_toolbar) is not VisualElement root)
            {
                return;
            }

            var zone = root.Q(TargetZone);
            if (zone == null)
            {
                return;
            }

            // 已注入则不重复添加（防止重复轮询或多实例）。
            if (zone.Q(ButtonName) != null)
            {
                EditorApplication.update -= TryInject;
                return;
            }

            // 用 IMGUIContainer + EditorStyles.toolbarButton 绘制：渲染与原生工具栏按钮一致、
            // 主题正确、文字稳定可见。直接用 UIElements Button 注入工具栏常出现白底/黑块、文字不可见。
            var container = new IMGUIContainer(OnToolbarGUI)
            {
                name = ButtonName,
            };
            container.style.flexGrow = 0f;
            container.style.marginLeft = 6f;
            container.style.alignSelf = Align.Center;

            zone.Add(container);

            EditorApplication.update -= TryInject;
        }

        private static void OnToolbarGUI()
        {
            if (GUILayout.Button(ButtonContent, EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                GoToGameMain();
            }
        }

        private static void GoToGameMain()
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(MainScenePath)))
            {
                EditorUtility.DisplayDialog(
                    "GameMain", "找不到主入口场景：\n" + MainScenePath, "知道了");
                return;
            }

            // 开关开启时：当前场景有未保存修改先问是否保存，取消则中止切换。
            // 关闭时：直接切换（与参考实现一致，未保存改动会丢弃）。
            if (PromptSaveBeforeSwitch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }
    }
}
