using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Fissiles.EditorUI
{
    /// <summary>The configuration window (Tools > Fissiles Analytics > 配置窗口): edits the settings asset and can test the credentials.</summary>
    public class FASettingsWindow : EditorWindow
    {
        const string DefaultPath = "Assets/Resources/" + FASettings.ResourceName + ".asset";
        SerializedObject _so;
        Vector2 _scroll;
        string _releaseStatus, _devStatus;
        GUIStyle _title, _hint, _status;

        public static void Open()
        {
            var w = GetWindow<FASettingsWindow>("Fissiles Analytics");
            w.minSize = new Vector2(460, 560);
            w.Show();
        }

        /// <summary>The settings asset Resources.Load will find (any Assets/**/Resources folder), or a new one at the default path.</summary>
        public static FASettings LoadOrCreate()
        {
            foreach (var guid in AssetDatabase.FindAssets(FASettings.ResourceName + " t:FASettings"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/") && path.Contains("/Resources/") && System.IO.Path.GetFileNameWithoutExtension(path) == FASettings.ResourceName)
                    return AssetDatabase.LoadAssetAtPath<FASettings>(path);
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var created = CreateInstance<FASettings>();
            AssetDatabase.CreateAsset(created, DefaultPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>Opens the dashboard on the realtime page of the development or release project.</summary>
        public static void OpenDashboard(bool dev)
        {
            var s = LoadOrCreate();
            var id = dev ? s.devAppId : s.appId;
            var url = s.serverUrl.TrimEnd('/') + (string.IsNullOrEmpty(id) ? "/" : "/?app=" + UnityWebRequest.EscapeURL(id) + "#realtime");
            Application.OpenURL(url);
        }

        void OnEnable() => Bind();
        void OnDisable() => Save();
        void OnLostFocus() => Save();

        void Bind() => _so = new SerializedObject(LoadOrCreate());

        void Save()
        {
            if (_so != null && _so.targetObject != null) AssetDatabase.SaveAssetIfDirty(_so.targetObject);
        }

        void OnGUI()
        {
            if (_so == null || _so.targetObject == null) Bind();
            _title ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
            _hint ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _status ??= new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };

            _so.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(6);
            GUILayout.Label("Fissiles Analytics", _title);
            EditorGUILayout.HelpBox("AppId 和上报密钥在数据看板「项目管理」页复制。修改自动保存到 " + AssetDatabase.GetAssetPath(_so.targetObject) + "。", MessageType.None);

            GUILayout.Space(6);
            EditorGUILayout.PropertyField(_so.FindProperty("reportingEnabled"), new GUIContent("启用上报", "关闭后 SDK 完全不启动，什么都不上报"));
            EditorGUILayout.PropertyField(_so.FindProperty("serverUrl"), new GUIContent("服务器地址"));

            Environment("正式环境", "正式包使用。", "appId", "appKey", false);
            Environment("开发环境", "Editor 和 Development Build 使用。单独建一个项目，测试数据不会混进线上统计。", "devAppId", "devAppKey", true);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("渠道", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_so.FindProperty("androidChannel"), new GUIContent("Android"));
                EditorGUILayout.PropertyField(_so.FindProperty("iosChannel"), new GUIContent("iOS"));
                EditorGUILayout.PropertyField(_so.FindProperty("otherChannel"), new GUIContent("其他平台"));
            }
            EditorGUILayout.PropertyField(_so.FindProperty("logEvents"), new GUIContent("在 Console 打印事件", "仅 Editor 和 Development Build 生效，接入调试时用"));
            _so.ApplyModifiedProperties();

            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开看板 (开发项目)")) OpenDashboard(true);
                if (GUILayout.Button("打开看板 (正式项目)")) OpenDashboard(false);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("使用说明 (README)")) FAReadmeEditor.Select();
                if (GUILayout.Button("在 Project 中定位配置文件"))
                {
                    Selection.activeObject = _so.targetObject;
                    EditorGUIUtility.PingObject(_so.targetObject);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void Environment(string title, string hint, string idProp, string keyProp, bool dev)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(title, EditorStyles.boldLabel);
                GUILayout.Label(hint, _hint);
                EditorGUILayout.PropertyField(_so.FindProperty(idProp), new GUIContent("AppId"));
                EditorGUILayout.PropertyField(_so.FindProperty(keyProp), new GUIContent("上报密钥"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("测试连接", GUILayout.Width(90))) Test(dev);
                    GUILayout.Label(dev ? _devStatus : _releaseStatus, _status);
                }
            }
        }

        void SetStatus(bool dev, string text)
        {
            if (dev) _devStatus = text; else _releaseStatus = text;
            if (this != null) Repaint(); // the window may have been closed while the request was running
        }

        void Test(bool dev)
        {
            _so.ApplyModifiedProperties();
            var s = (FASettings)_so.targetObject;
            var id = dev ? s.devAppId : s.appId;
            var key = dev ? s.devAppKey : s.appKey;
            if (string.IsNullOrEmpty(s.serverUrl) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key))
            {
                SetStatus(dev, "<color=#d9a400>请先填写服务器地址、AppId 和上报密钥</color>");
                return;
            }

            SetStatus(dev, "测试中…");
            var req = new UnityWebRequest(s.serverUrl.TrimEnd('/') + "/api/track", "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{\"events\":[]}")),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-App-Id", id);
            req.SetRequestHeader("X-App-Key", key);
            var op = req.SendWebRequest();

            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!op.isDone) return;
                EditorApplication.update -= poll;
                var code = req.responseCode;
                // The server checks AppId and key first and only then rejects an empty batch with 400,
                // so 400 means "credentials are valid" - and nothing is stored.
                SetStatus(dev,
                    code == 400 || code == 200 ? "<color=#2e9b5f>✓ 连接正常，AppId 和上报密钥有效</color>"
                    : code == 401 ? "<color=#e05252>✗ AppId 或上报密钥错误</color>"
                    : req.result == UnityWebRequest.Result.ConnectionError ? "<color=#e05252>✗ 连不上服务器：" + req.error + "</color>"
                    : "<color=#e05252>✗ 服务器返回 " + code + "</color>");
                req.Dispose();
            };
            EditorApplication.update += poll;
        }
    }
}
