using UnityEditor;
using UnityEngine;

namespace Fissiles.EditorUI
{
    /// <summary>Draws <see cref="FAReadme"/> in the Inspector: title, headings, selectable text, and clickable links.</summary>
    [CustomEditor(typeof(FAReadme))]
    public class FAReadmeEditor : Editor
    {
        public const string AssetPath = "Packages/com.fissiles.analytics/Editor/FAReadme.asset";
        GUIStyle _title, _heading, _body, _link;

        /// <summary>Selects the README asset, so its text shows in the Inspector.</summary>
        public static void Select()
        {
            var readme = AssetDatabase.LoadAssetAtPath<FAReadme>(AssetPath);
            if (readme == null)
            {
                Debug.LogWarning("[FA] README asset not found: " + AssetPath);
                return;
            }
            Selection.activeObject = readme;
            EditorGUIUtility.PingObject(readme);
        }

        /// <summary>"menu:Tools/..." runs a menu item, anything else opens in the browser.</summary>
        public static void Open(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (url.StartsWith("menu:")) EditorApplication.ExecuteMenuItem(url.Substring(5));
            else Application.OpenURL(url);
        }

        protected override bool ShouldHideOpenButton() => true;

        public override void OnInspectorGUI()
        {
            if (_title == null)
            {
                _title = new GUIStyle(EditorStyles.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
                _heading = new GUIStyle(EditorStyles.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true };
                _body = new GUIStyle(EditorStyles.label) { fontSize = 13, wordWrap = true };
                _link = new GUIStyle(_body) { stretchWidth = false, normal = { textColor = new Color(0.33f, 0.64f, 1f) } };
            }

            var readme = (FAReadme)target;
            GUILayout.Space(8);
            GUILayout.Label(readme.title, _title);
            foreach (var s in readme.sections ?? new FAReadme.Section[0])
            {
                GUILayout.Space(12);
                if (!string.IsNullOrEmpty(s.heading)) GUILayout.Label(s.heading, _heading);
                if (!string.IsNullOrEmpty(s.text))
                {
                    // SelectableLabel so code samples can be copied; it needs its height worked out by hand.
                    var height = _body.CalcHeight(new GUIContent(s.text), EditorGUIUtility.currentViewWidth - 28);
                    EditorGUILayout.SelectableLabel(s.text, _body, GUILayout.Height(height));
                }
                if (!string.IsNullOrEmpty(s.linkText))
                {
                    if (GUILayout.Button(s.linkText, _link)) Open(s.url);
                    EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
                }
            }
        }
    }
}
