using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin.Services
{
    public static class MotdService
    {
#if FOYX_DEBUG
        private const string Url = "http://localhost:80/motd.json";
        private const double TimeBetweenUpdates = 1; // 1 Second
#else
        private const string Url = "https://raw.githubusercontent.com/foxscore/easy-login/refs/heads/main/motd.json";
        private const double TimeBetweenUpdates = 5 * 60; // 5 Minutes
#endif
        private static double _lastUpdate = 0;

        private static MotdMessage[] _motdMessages = {};
        public static IReadOnlyCollection<MotdMessage> MotdMessages => _motdMessages;
        
        [InitializeOnLoadMethod]
        private static void StartSyncService()
        {
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

        private static async Task FetchMotd()
        {
            try
            {
                var rawPackageJson = await File.ReadAllTextAsync(Path.Combine(Application.dataPath, "..", "Packages", "dev.foxscore.easy-login", "package.json"));
                var packageJson = JsonConvert.DeserializeObject<dynamic>(rawPackageJson);
                
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-EasyLogin-Version", packageJson.version as string);
                var rawJson = await client.GetStringAsync(Url);
                var messages = JsonConvert.DeserializeObject<MotdMessage[]>(rawJson);
                UpdateData(messages);
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
