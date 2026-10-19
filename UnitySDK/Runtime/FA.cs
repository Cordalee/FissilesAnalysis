using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Fissiles
{
    /// <summary>
    /// Fissiles Analytics SDK. Sessions, online time and DAU are tracked automatically after Init.
    /// <code>
    /// FA.Init(account.Id);                                   // as soon as the account id is known; server and keys come from the settings asset
    /// FA.Track("harvest", "crop", "wheat", "count", 3);      // name + key/value pairs
    /// FA.TrackPurchase(orderId, "gems_68", 6800);            // amount in cents (分)
    /// </code>
    /// Events are queued in memory, sent in batches, and kept in PlayerPrefs while offline.
    /// </summary>
    public static class FA
    {
        public static float FlushInterval = 10f;     // seconds between batches
        public static float HeartbeatInterval = 60f; // keep in sync with the server's 3-minute "online" window
        public static float SessionTimeout = 30f;    // background longer than this starts a new session
        public static bool LogEvents;                // echo every event to the console while integrating

        const string QueueKey = "fa.queue", UidKey = "fa.uid", DidKey = "fa.did";
        const int MaxQueue = 2000, BatchSize = 200;
        static readonly List<string> Queue = new List<string>();
        static readonly object[] None = new object[0];
        static string _url, _appId, _key, _common, _uid, _sid;
        static float _beatAt;
        static Runner _runner;

        public static string UserId => _uid;

        /// <summary>
        /// Starts the SDK from the settings asset (Tools > Fissiles Analytics > 配置窗口). Call it once the player's account id
        /// is known; calling it again later (e.g. on every login) only checks the account: same id = nothing, another id = new session.
        /// Editor and Development Builds report to the development project, release builds to the release project.
        /// </summary>
        public static void Init(string userId)
        {
            if (_runner != null) { Login(userId); return; }
            var s = FASettings.Load();
            if (s == null) { Debug.LogError("[FA] Settings asset missing. Open Tools > Fissiles Analytics > 配置窗口 to create it."); return; }
            if (!s.reportingEnabled) return;

            var dev = Debug.isDebugBuild;
            var id = dev ? s.devAppId : s.appId;
            var key = dev ? s.devAppKey : s.appKey;
            if (string.IsNullOrEmpty(s.serverUrl) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key))
            {
                Debug.LogError("[FA] Server URL / " + (dev ? "development" : "release") + " AppId / AppKey not filled in. Open Tools > Fissiles Analytics > 配置窗口.");
                return;
            }
            LogEvents = dev && s.logEvents;
#if UNITY_ANDROID
            var channel = s.androidChannel;
#elif UNITY_IOS
            var channel = s.iosChannel;
#else
            var channel = s.otherChannel;
#endif
            Init(s.serverUrl, id, key, channel, userId);
        }

        /// <param name="appId">project id shown on the dashboard's project page</param>
        /// <param name="userId">the player's account id. Pass it here when it is already known: the first session is then
        /// recorded under the account, not under a device id that would count the same player twice. Null = last used id, else the device id.</param>
        public static void Init(string serverUrl, string appId, string appKey, string channel = "", string userId = null)
        {
            if (_runner != null) return;
            _url = serverUrl.TrimEnd('/') + "/api/track";
            _appId = appId;
            _key = appKey;
            var did = PlayerPrefs.GetString(DidKey);
            if (string.IsNullOrEmpty(did))
            {
                did = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrEmpty(did) || did == SystemInfo.unsupportedIdentifier) did = Guid.NewGuid().ToString("N"); // WebGL / mini games
                PlayerPrefs.SetString(DidKey, did);
            }
            if (!string.IsNullOrEmpty(userId)) PlayerPrefs.SetString(UidKey, userId);
            _uid = string.IsNullOrEmpty(userId) ? PlayerPrefs.GetString(UidKey, did) : userId;
            _common = "{" + Kv("platform", Application.platform.ToString()) + "," + Kv("ver", Application.version) + ","
                      + Kv("channel", channel) + "," + Kv("model", SystemInfo.deviceModel) + "}";
            foreach (var line in PlayerPrefs.GetString(QueueKey).Split('\n'))
                if (line.Length > 0) Queue.Add(line);

            var go = new GameObject("[FissilesAnalytics]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
            StartSession();
        }

        /// Switch to another account (or to the account id after an anonymous start). Remembered across launches.
        /// A running app closes the previous account's session and opens one for the new account.
        public static void Login(string userId)
        {
            if (string.IsNullOrEmpty(userId) || userId == _uid) return;
            if (_runner != null) Add("$session_end", TakeDuration(), None);
            _uid = userId;
            PlayerPrefs.SetString(UidKey, userId);
            if (_runner != null) StartSession();
        }

        /// <param name="kv">alternating key, value: "crop", "wheat", "count", 3. Values: string, number, bool.</param>
        public static void Track(string name, params object[] kv) => Add(name, 0, kv);

        /// Client-side purchase report. For revenue you can trust, report from the game server after
        /// receipt verification and give the project a ServerKey on the analytics server (see README).
        public static void TrackPurchase(string orderId, string productId, long amountCents, string currency = "CNY") =>
            Add("$purchase", 0, new object[] { "orderId", orderId, "productId", productId, "amount", amountCents, "currency", currency });

        /// Sends queued events now, e.g. before a scene that may stall the main thread.
        public static void Flush() => _runner?.Flush();

        static void Add(string name, int dur, object[] kv)
        {
            if (_runner == null) { Debug.LogWarning("[FA] call FA.Init first, dropped: " + name); return; }
            var sb = new StringBuilder(160).Append('{').Append(Kv("name", name))
                .Append(",\"ts\":").Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                .Append(',').Append(Kv("uid", _uid)).Append(',').Append(Kv("sid", _sid));
            if (dur > 0) sb.Append(",\"dur\":").Append(dur);
            if (kv.Length > 1)
            {
                sb.Append(",\"props\":{");
                for (var i = 0; i + 1 < kv.Length; i += 2)
                    sb.Append(i > 0 ? "," : "").Append(Str(Convert.ToString(kv[i], CultureInfo.InvariantCulture))).Append(':').Append(Val(kv[i + 1]));
                sb.Append('}');
            }
            var json = sb.Append('}').ToString();
            if (LogEvents) Debug.Log("[FA] " + json);
            lock (Queue)
                if (Queue.Count < MaxQueue) Queue.Add(json); // full while offline for long: drop new rather than grow PlayerPrefs forever
        }

        static void StartSession()
        {
            _sid = Guid.NewGuid().ToString("N").Substring(0, 16);
            _beatAt = Time.realtimeSinceStartup;
            Add("$session_start", 0, None);
        }

        /// Foreground seconds since the last heartbeat / session start; the remainder carries over.
        static int TakeDuration()
        {
            var d = (int)(Time.realtimeSinceStartup - _beatAt);
            _beatAt += d;
            return d;
        }

        static void Save()
        {
            lock (Queue) PlayerPrefs.SetString(QueueKey, string.Join("\n", Queue));
            PlayerPrefs.Save();
        }

        static string Kv(string k, string v) => Str(k) + ":" + Str(v);

        static string Val(object v)
        {
            switch (v)
            {
                case null: return "null";
                case bool b: return b ? "true" : "false";
                case float f: return float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture);
                case Enum e: return Str(e.ToString());
                case IConvertible c when c.GetTypeCode() >= TypeCode.SByte && c.GetTypeCode() <= TypeCode.Decimal:
                    return c.ToString(CultureInfo.InvariantCulture);
                default: return Str(Convert.ToString(v, CultureInfo.InvariantCulture));
            }
        }

        static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2).Append('"');
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break; // also keeps the newline-separated PlayerPrefs queue intact
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        sealed class Runner : MonoBehaviour
        {
            float _nextFlush;
            int _fails;
            bool _sending;
            DateTime? _pausedAt;

            void Update()
            {
                if (_pausedAt != null) return;
                if (Time.realtimeSinceStartup - _beatAt >= HeartbeatInterval) Add("$heartbeat", TakeDuration(), None);
                int count;
                lock (Queue) count = Queue.Count;
                if (!_sending && count > 0 && (Time.realtimeSinceStartup >= _nextFlush || count >= BatchSize)) StartCoroutine(Send());
            }

            public void Flush()
            {
                if (!_sending) StartCoroutine(Send());
            }

            void OnApplicationPause(bool paused)
            {
                if (paused)
                {
                    if (_pausedAt != null) return;
                    Add("$session_end", TakeDuration(), None);
                    _pausedAt = DateTime.UtcNow;
                    Save();
                    Flush(); // may not finish before the OS suspends us; the saved queue covers that
                }
                else if (_pausedAt is DateTime at)
                {
                    _pausedAt = null;
                    if ((DateTime.UtcNow - at).TotalSeconds > SessionTimeout) StartSession();
                    else _beatAt = Time.realtimeSinceStartup; // short break: same session, background time not counted
                }
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL / WeChat mini games report focus changes instead of pause.
            void OnApplicationFocus(bool focus) => OnApplicationPause(!focus);
#endif

            void OnApplicationQuit()
            {
                if (_pausedAt == null) Add("$session_end", TakeDuration(), None);
                Save();
            }

            IEnumerator Send()
            {
                _sending = true;
                int n;
                string body;
                lock (Queue)
                {
                    n = Math.Min(Queue.Count, BatchSize);
                    body = "{\"common\":" + _common + ",\"events\":[" + string.Join(",", Queue.GetRange(0, n)) + "]}";
                }
                if (n == 0) { _sending = false; yield break; }

                using (var req = new UnityWebRequest(_url, "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("X-App-Id", _appId);
                    req.SetRequestHeader("X-App-Key", _key);
                    req.timeout = 15;
                    yield return req.SendWebRequest();

                    var code = req.responseCode;
                    // 400/413 = the batch itself is bad (e.g. malformed JSON): retrying would block the queue forever.
                    if (req.result == UnityWebRequest.Result.Success || code == 400 || code == 413)
                    {
                        if (code >= 400) Debug.LogWarning($"[FA] server rejected {n} events ({code}): {req.downloadHandler.text}");
                        lock (Queue) Queue.RemoveRange(0, n); // new events are only appended, so the first n are the ones sent
                        _fails = 0;
                        _nextFlush = Time.realtimeSinceStartup + FlushInterval;
                    }
                    else
                    {
                        if (code == 401) Debug.LogError("[FA] AppId/AppKey rejected by server - check FA.Init and the server's Apps config");
                        _fails++;
                        _nextFlush = Time.realtimeSinceStartup + Mathf.Min(FlushInterval * (1 << Mathf.Min(_fails, 5)), 300f);
                    }
                }
                Save();
                _sending = false;
            }
        }
    }
}
