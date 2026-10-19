using UnityEngine;

namespace Fissiles
{
    /// <summary>
    /// SDK configuration, stored as an asset in the project (Assets/Resources/FissilesAnalyticsSettings.asset) so it ships
    /// with the build. Edit it in Tools > Fissiles Analytics > 配置窗口; <see cref="FA.Init(string)"/> reads it.
    /// AppId and keys are copied from the dashboard's project page. Keys are not secrets: they end up inside the game package.
    /// </summary>
    public class FASettings : ScriptableObject
    {
        public const string ResourceName = "FissilesAnalyticsSettings";

        [Tooltip("Master switch. Off = the SDK never starts and nothing is reported.")]
        public bool reportingEnabled = true;
        [Tooltip("Analytics server, e.g. https://analytics.fissiles.net")]
        public string serverUrl = "https://analytics.fissiles.net";

        [Header("Release builds")]
        public string appId = "";
        public string appKey = "";

        [Header("Editor + Development Build (a separate project keeps test data out of the real numbers)")]
        public string devAppId = "";
        public string devAppKey = "";

        [Header("Channel reported with every player")]
        public string androidChannel = "googleplay";
        public string iosChannel = "appstore";
        public string otherChannel = "";

        [Tooltip("Echo every event to the Console. Only applies to Editor / Development Build.")]
        public bool logEvents = true;

        public static FASettings Load() => Resources.Load<FASettings>(ResourceName);
    }
}
