// ------------------------------------------------------------
// FormalSequenceWiring.cs
// Author : WizardHeHeJun
// Created: 2026-07-08
// Desc   : 正式关卡接线工具（自 TwoLevelFlowDemoSetup 迁出，Demo 生成器已删）。
//          「接线正式关卡数据」：只把 Formal_Sequence 接到 Bootstrap，不动关卡数据；
//          「★ 一键重建全部」：重建装配 + 正式关卡美术数据（收尾即正式接线）。
//          SceneSwitcherToolbar / AiBridge 经反射调用 TryWireFormalSequence，
//          改类名/方法名需同步三处 typeName 字符串。
// ------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ciga.AnchorHorror.EditorTools
{
    /// <summary>正式关卡序列接线与一键重建入口。正式接线是默认态，此处是唯一恢复/重建入口。</summary>
    public static class FormalSequenceWiring
    {
        private const string ResDir = "Assets/Res/AnchorHorror";
        private const string BootstrapScene = ResDir + "/Bootstrap.unity";
        private const string FormalSequencePath = ResDir + "/Levels/Formal_Sequence.asset";

        /// <summary>一键重建：Bootstrap 装配（默认接正式序列）+ 正式关卡美术数据（并接线）。</summary>
        [MenuItem("Ciga/AnchorHorror/★ 一键重建全部（装配+正式关卡数据）")]
        public static void RebuildEverything()
        {
            AnchorHorrorSetup.BuildAll();   // 重建 Bootstrap 装配，内部已默认接线正式序列
            FormalSceneArtSetup.BuildAll(); // 重建正式关卡美术数据并接线
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "一键重建完成",
                "已重建 Bootstrap 装配 + 正式关卡美术数据并接线。\n从 GameMain 场景 Play 即为正式内容。",
                "好");
        }

        /// <summary>安全接线正式关卡：只把 Formal_Sequence.asset 接到 Bootstrap，不生成/覆盖任何关卡数据。</summary>
        [MenuItem("Ciga/AnchorHorror/接线正式关卡数据")]
        public static void WireFormalSequenceMenu()
        {
            bool ok = TryWireFormalSequence(out string message);
            EditorUtility.DisplayDialog(ok ? "接线完成" : "接线失败", message, "好");
        }

        /// <summary>接线正式关卡序列，供菜单 / 工具栏 / AI Bridge（反射）调用；不弹窗，便于自动化验证。</summary>
        public static bool TryWireFormalSequence(out string message)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<LevelSequence>(FormalSequencePath);
            if (sequence == null)
            {
                message = $"请先创建正式关卡序列资产：\n{FormalSequencePath}\n\n此操作只负责接线，不会自动生成或覆盖正式关卡数据。";
                Debug.LogWarning("[FormalSequenceWiring] " + message);
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

        private static bool WireBootstrapScene(LevelSequence sequence, string label)
        {
            if (sequence == null)
            {
                Debug.LogWarning($"[FormalSequenceWiring] {label} 为空，跳过接线。");
                return false;
            }

            if (!File.Exists(BootstrapScene))
            {
                Debug.LogWarning($"[FormalSequenceWiring] 找不到 Bootstrap.unity：{BootstrapScene}，跳过接线。请先运行「生成可运行装配」。");
                return false;
            }

            // 确保当前活动场景有路径（防 Single 弹保存框）
            var active = SceneManager.GetActiveScene();
            string tempActive = null;
            if (string.IsNullOrEmpty(active.path))
            {
                tempActive = ResDir + "/__temp_formal_wiring.unity";
                EditorSceneManager.SaveScene(active, tempActive);
            }

            var bootstrapScene = EditorSceneManager.OpenScene(BootstrapScene, OpenSceneMode.Additive);
            bool wired = false;

            foreach (var root in bootstrapScene.GetRootGameObjects())
            {
                var gm = root.GetComponent<GameManager>();
                if (gm != null)
                {
                    var soGm = new SerializedObject(gm);
                    var prop = soGm.FindProperty("_sequence");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = sequence;
                        soGm.ApplyModifiedPropertiesWithoutUndo();
                        wired = true;
                        Debug.Log($"[FormalSequenceWiring] GameManager._sequence 已接线：{label}。");
                    }
                    else
                    {
                        Debug.LogWarning("[FormalSequenceWiring] GameManager 找不到 _sequence 字段。");
                    }
                }

                if (wired)
                {
                    break;
                }
            }

            if (!wired)
            {
                Debug.LogWarning("[FormalSequenceWiring] Bootstrap 场景未找到 GameManager，_sequence 未接线。");
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
    }
}
