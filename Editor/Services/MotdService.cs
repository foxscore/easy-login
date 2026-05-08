using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Version = Foxscore.EasyLogin.SemanticVersioning.Version;
using Range = Foxscore.EasyLogin.SemanticVersioning.Range;

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

        private static SafeFileHandler _hiddenMessagesGuidFileHandler;
        private static List<string> _hiddenMessageGuids = new();
        
        private static MotdMessage[] _motdMessages = {};
        public static IReadOnlyCollection<MotdMessage> MotdMessages => _motdMessages;
        
        [InitializeOnLoadMethod]
        private static void StartSyncService()
        {
            if (!Is.FirstRun()) return;

            var elDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fox_score", "EasyLogin");
            var hiddenMessagesPath = Path.Combine(elDir, "hidden_messages.json");
            _hiddenMessagesGuidFileHandler = new SafeFileHandler(hiddenMessagesPath, LoadHiddenMessages);
            if (_hiddenMessagesGuidFileHandler.Exists())
                LoadHiddenMessages(_hiddenMessagesGuidFileHandler.ReadAllText());
            
            var cacheDir = Path.Combine(Application.dataPath, "..", "Temp", "EasyLogin", "cache");
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
        
        [MenuItem("Debug/MOTD/Reload from .../Cache")]
        public static void DEBUG_ReloadMotdFromCache()
        {
            if (!_cacheFileHandler.Exists()) return;
            var content = _cacheFileHandler.ReadAllText();
            LoadCache(content);
        }
        
        [MenuItem("Debug/MOTD/Reload from .../Origin")]
        public static void DEBUG_FetchMotd() => _ = FetchMotd();

#region Mirror switching
        private const string CompileFlag = "FOXY_USE_LOCAL_MOTD";
        
        [MenuItem("Debug/MOTD/Local mirror .../Enabled")]
        public static void DEBUG_EnableLocalMirror()
        {
            var symbolsStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var symbols = symbolsStr.Split(';').ToList();
            if (!symbols.Contains(CompileFlag))
                symbols.Add(CompileFlag);
            symbolsStr = string.Join(";", symbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbolsStr);
            EditorUtility.RequestScriptReload();
        }
        [MenuItem("Debug/MOTD/Local mirror .../Enabled", true)]
        public static bool DEBUG_EnableLocalMirror_Validate()
        {
            var symbolsStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var symbols = symbolsStr.Split(';');
            return !symbols.Contains(CompileFlag);
        }
        
        [MenuItem("Debug/MOTD/Local mirror .../Disabled")]
        public static void DEBUG_DisableLocalMirror()
        {
            var symbolsStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var symbols = symbolsStr.Split(';').ToList();
            if (symbols.Contains(CompileFlag))
                symbols.Remove(CompileFlag);
            symbolsStr = string.Join(";", symbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbolsStr);
            EditorUtility.RequestScriptReload();
        }
        [MenuItem("Debug/MOTD/Local mirror .../Disabled", true)]
        public static bool DEBUG_DisableLocalMirror_Validate()
        {
            var symbolsStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var symbols = symbolsStr.Split(';');
            return symbols.Contains(CompileFlag);
        }
#endregion

#endif
        
        private static async Task FetchMotd()
        {
            try
            {
                using var client = new HttpClient();
                client.SetEasyLoginUserAgent();
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

            try
            {
                _motdMessages = JsonConvert.DeserializeObject<MotdMessage[]>(fileContent);
            }
            catch (Exception e)
            {
                Log.Debug("Failed to load motd cache, resetting...", e);
                _motdMessages = Array.Empty<MotdMessage>();
                WriteCache();
            }
        }

        private static void WriteHiddenMessages()
        {
            var json = JsonConvert.SerializeObject(_hiddenMessageGuids, Formatting.Indented);
            _hiddenMessagesGuidFileHandler.WriteAllText(json);
        }

        private static void LoadHiddenMessages(string fileContent)
        {
            if (string.IsNullOrEmpty(fileContent))
            {
                Thread.Sleep(10);
                fileContent = _cacheFileHandler.ReadAllText();
                if (string.IsNullOrEmpty(fileContent))
                    fileContent = "[]";
            }

            try
            {
                _hiddenMessageGuids = JsonConvert.DeserializeObject<List<string>>(fileContent);
            }
            catch (Exception e)
            {
                Log.Error("Failed to load data relating to what MOTD's are hidden. A backup copy has been made and this data will be reset.", e);
                _hiddenMessageGuids.Clear();
                WriteHiddenMessages();
            }
        }

        public static void HideMessagePermanently(string guid)
        {
            _hiddenMessageGuids.Add(guid);
            WriteHiddenMessages();
        }

        public static bool IsMessageHiddenPermanently(string guid) => _hiddenMessageGuids.Contains(guid);
    }

    public class MotdMessage
    {
        [JsonProperty("id")] public string Guid;
        [JsonProperty("message")] public string Message;
        [JsonProperty("valid_from")] public DateTime? ValidFrom;
        [JsonProperty("valid_until")] public DateTime? ValidUntil;
        [JsonProperty("allow_hiding")] public bool AllowHiding;
        [JsonProperty("versionRange")] public string VersionRange;
        [JsonProperty("operatingSystem")] public string[] OperatingSystem = Array.Empty<string>();

        private bool? StaticShouldShow;

        public bool ShouldShow()
        {
            if (!StaticShouldShow.HasValue)
            {
                if (OperatingSystem is { Length: > 0 })
                {
#if UNITY_EDITOR_WIN
                    if (!OperatingSystem.Contains("windows"))
#elif UNITY_EDITOR_OSX
                if (!OperatingSystem.Contains("osx"))
#elif UNITY_EDITOR_LINUX
                if (!OperatingSystem.Contains("linux"))
#endif
                    {
                        StaticShouldShow = false;
                        return false;
                    }
                }

                if (!string.IsNullOrWhiteSpace(VersionRange))
                {
                    var version = Utils.GetPackageJson().VersionString;
                    StaticShouldShow = Range.IsSatisfied(
                        VersionRange,
                        version,
                        loose: true,
                        includePrerelease: true
                    );
                }
            }
            if (!(StaticShouldShow ??= true))
                return false;

            if (AllowHiding && MotdService.IsMessageHiddenPermanently(Guid))
                return false;
            if (AllowHiding && SessionState.GetBool($"EasyLogin::motd::HiddenMessages::{Guid}", false))
                return false;
            if (ValidFrom.HasValue && ValidFrom.Value > DateTime.UtcNow)
                return false;
            if (ValidUntil.HasValue && ValidUntil.Value < DateTime.UtcNow)
                return false;
            return true;
        }

        public void HideMessage() => SessionState.SetBool($"EasyLogin::motd::HiddenMessages::{Guid}", true);
        public void HideMessageForever() => MotdService.HideMessagePermanently(Guid);
    }
}
