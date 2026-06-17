// AiBridge 控制面板：手动启停桥、看状态。菜单 Window/AI Bridge。
using UnityEditor;
using UnityEngine;

namespace Ciga.AiBridge
{
    public class AiBridgeWindow : EditorWindow
    {
        [MenuItem("Window/AI Bridge")]
        public static void Open()
        {
            GetWindow<AiBridgeWindow>("AI Bridge");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("AI 验证桥", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("端口", AiBridgeServer.Port.ToString());
            EditorGUILayout.LabelField("状态", AiBridgeServer.IsRunning ? "运行中" : "已停止");

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("启动"))
                    AiBridgeServer.Start();
                if (GUILayout.Button("停止"))
                    AiBridgeServer.Stop();
            }

            EditorGUILayout.Space();
            bool auto = EditorPrefs.GetBool("Ciga.AiBridge.AutoStart", true);
            bool newAuto = EditorGUILayout.ToggleLeft("Editor 启动时自动开启", auto);
            if (newAuto != auto)
                EditorPrefs.SetBool("Ciga.AiBridge.AutoStart", newAuto);

            EditorGUILayout.HelpBox(
                "CLI 客户端：python .claude/skills/unity-bridge/scripts/bridge.py <cmd>\n" +
                "cmd: health | compile | console | play | stop | screenshot | test | hierarchy",
                MessageType.Info);
        }
    }
}
