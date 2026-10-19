using UnityEditor;
using UnityEngine;

namespace Fissiles.EditorUI
{
    /// <summary>Inspector for the settings asset: points to the config window and the README, then shows the raw fields.</summary>
    [CustomEditor(typeof(FASettings))]
    public class FASettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Fissiles Analytics 配置。建议在「配置窗口」里编辑，那里可以测试 AppId 和上报密钥是否有效。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开配置窗口")) FASettingsWindow.Open();
                if (GUILayout.Button("使用说明 (README)")) FAReadmeEditor.Select();
            }
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
