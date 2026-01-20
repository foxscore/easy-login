using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin.Services
{
    public static class MotdService
    {
#if FOXY_USE_LOCAL_MOTD
        private const string Url = "http://localhost:80/motd.json";
        private const double TimeBetweenUpdates = 1; // 1 Second
#else
        private const string Url = "https://raw.githubusercontent.com/foxscore/easy-login/refs/heads/main/motd.json";
        private const double TimeBetweenUpdates = 5 * 60; // 5 Minutes
#endif
        private static double _lastUpdate = -100 - TimeBetweenUpdates; // * Default value must be low enough to trigger an update on startup 
        private static SafeFileHandler _cacheFileHandler;

        private static MotdMessage[] _motdMessages = {};
        public static IReadOnlyCollection<MotdMessage> MotdMessages => _motdMessages;
        
        [InitializeOnLoadMethod]
        private static void StartSyncService()
        {
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fox_score", "EasyLogin", "cache");
            var cacheFilePath = Path.Combine(cacheDir, "motd.json");
            _cacheFileHandler = new SafeFileHandler(cacheFilePath, LoadCache);
            
            if (_cacheFileHandler.Exists())
                LoadCache(_cacheFileHandler.ReadAllText());
            
            var rawPreviousLastUpdate = SessionState.GetString("EasyLogin::motd::LastUpdate", null);
            if (!string.IsNullOrWhiteSpace(rawPreviousLastUpdate))
            {
                var lastUpdateValue = double.Parse(rawPreviousLastUpdate, CultureInfo.InvariantCulture);
                _lastUpdate = lastUpdateValue;
            }
            
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup <= _lastUpdate + TimeBetweenUpdates)
                    return;
                _lastUpdate = EditorApplication.timeSinceStartup;
                _ = FetchMotd();
            };
        }
        
#if FOXY_DEBUG
        [MenuItem("Debug/Reload MOTD from cache")]
        public static void DEBUG_ReloadMotdFromCache()
        {
            if (!_cacheFileHandler.Exists()) return;
            var content = _cacheFileHandler.ReadAllText();
            LoadCache(content);
        }
        
        [MenuItem("Debug/Fetch MOTD")]
        public static void DEBUG_FetchMotd() => _ = FetchMotd();
#endif
        
        private static async Task FetchMotd()
        {
            try
            {
                var packageJson = Utils.GetPackageJson();
                using var client = new HttpClient();
                client.SetEasyLoginUserAgent(packageJson.VersionString);
                var rawJson = await client.GetStringAsync(Url);
                var messages = JsonConvert.DeserializeObject<MotdMessage[]>(rawJson);
                UpdateData(messages);
                WriteCache();
            }
            catch (Exception e)
            {
                UpdateData(null);
                Log.Error($"There was an error while trying to fetch the motd: {e}");
            }
        }

        private static void UpdateData(MotdMessage[] newMotdMessages)
        {
            _lastUpdate = EditorApplication.timeSinceStartup;
            if (newMotdMessages != null)
                _motdMessages = newMotdMessages;
            SessionState.SetString("EasyLogin::motd::LastUpdate", _lastUpdate.ToString(CultureInfo.InvariantCulture));
        }
        
        private static void WriteCache()
        {
            var json = JsonConvert.SerializeObject(_motdMessages, Formatting.Indented);
            _cacheFileHandler.WriteAllText(json);
        }

        private static void LoadCache(string fileContent)
        {
            if (string.IsNullOrEmpty(fileContent))
            {
                Thread.Sleep(10);
                fileContent = _cacheFileHandler.ReadAllText();
                if (string.IsNullOrEmpty(fileContent))
                {
                    // Now we know for sure that the cache file is either empty or non-existent.
                    // Strange, but not a problem. Just reset the data stored in here.
                    fileContent = "[]";
                }
            }
            _motdMessages = JsonConvert.DeserializeObject<MotdMessage[]>(fileContent);
        }
    }

    public class MotdMessage
    {
        [JsonProperty("id")] public string Guid;
        [JsonProperty("message")] public string Message;
        [JsonProperty("valid_from")] public DateTime? ValidFrom;
        [JsonProperty("valid_until")] public DateTime? ValidUntil;
        [JsonProperty("allow_hiding")] public bool AllowHiding;

        public bool ShouldShow()
        {
            if (AllowHiding && SessionState.GetBool($"EasyLogin::motd::HiddenMessages::{Guid}", false))
                return false;
            if (ValidFrom.HasValue && ValidFrom.Value > DateTime.UtcNow)
                return false;
            if (ValidUntil.HasValue && ValidUntil.Value < DateTime.UtcNow)
                return false;
            return true;
        }

        public void HideMessage()
        {
            SessionState.SetBool($"EasyLogin::motd::HiddenMessages::{Guid}", true);
        }
    }
}
