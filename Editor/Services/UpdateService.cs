using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Version = SemanticVersioning.Version;
using Range = SemanticVersioning.Range;
using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin.Services
{
    public struct UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public Version InstalledVersion { get; set; }
        [CanBeNull] public Version LatestVersionAvailable { get; set; }
    }
    
    public static class UpdateService
    {
        private const string IndexUrl = "https://foxscore.dev/vpm/index.json";
        private const double TimeBetweenUpdates = 15 * 60; // 15 Minutes
        
        private static double _lastUpdate = -100 - TimeBetweenUpdates; // * Default value must be low enough to trigger an update on startup
        private static DateTime? _lastUpdateDateTime;
        
        public static UpdateCheckResult? LastUpdateCheckResult { get; private set; }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            var rawPreviousLastUpdate = SessionState.GetString("EasyLogin::updateCheck::LastUpdate", null);
            if (!string.IsNullOrWhiteSpace(rawPreviousLastUpdate))
            {
                var lastUpdateValue = double.Parse(rawPreviousLastUpdate, CultureInfo.InvariantCulture);
                _lastUpdate = lastUpdateValue;
            }

            EditorApplication.update += BackgroundTick;
        }
        
#if FOXY_DEBUG
        [MenuItem("Debug/Check for Updates")]
        public static void CheckForUpdatesMenuItem() => _ = CheckForUpdates_BackgroundTask();
#endif

        private static void BackgroundTick()
        {
            if (EditorApplication.timeSinceStartup <= _lastUpdate + TimeBetweenUpdates)
                return;
            _lastUpdate = EditorApplication.timeSinceStartup;
            _ = CheckForUpdates_BackgroundTask();
        }

        private static async Task CheckForUpdates_BackgroundTask()
        {
            var result = await CheckForUpdates();
            if (result is { IsSuccess: true, Value: { IsUpdateAvailable: true } })
            {
                EditorApplication.update -= BackgroundTick;
                Log.Info(
                    $"A new version is available【 <color=grey>{result.Value.InstalledVersion}</color> <b>→</b> <color=cyan>{result.Value.LatestVersionAvailable}</color> 】\n" +
                    "<color=silver>See the authentication tab of the VRChat SDK window for details.</color>\n"
                );
            }
        }

        public static async Task<Result<UpdateCheckResult>> CheckForUpdates()
        {
            try
            {
                // Load currently installed version
                var packageJson = Utils.GetPackageJson();
                var currentSemVer = packageJson.GetSemanticVersion();
                
                // Should we check if 
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VRChatCreatorCompanion",
                    "settings.json"
                );
                var shouldRespectPreReleases = currentSemVer.IsPreRelease;
                if ( ! shouldRespectPreReleases || File.Exists(configPath)
                ) {
                    var fileContents = await File.ReadAllTextAsync(configPath);
                    var config = JsonConvert.DeserializeObject<Abstract.VccConfig>(fileContents);
                    shouldRespectPreReleases = config is { ShowPrereleasePackages: true };
                }

                // Get available versions
                using var client = new HttpClient();
                client.SetEasyLoginUserAgent();
                var rawJson = await client.GetStringAsync(IndexUrl);
                var abstractIndex = JsonConvert.DeserializeObject<Abstract.Index>(rawJson);
                var versions = abstractIndex.Packages.EasyLogin.GetVersions();

                // Compare versions
                var latestVersion = currentSemVer;
                foreach (var version in versions)
                {
                    var semVer = Version.Parse(version);
                    if (!shouldRespectPreReleases && semVer.IsPreRelease)
                        continue;
                    if (semVer > latestVersion)
                        latestVersion = semVer;
                }
                
                // Done
                var updateCheckResult = new UpdateCheckResult()
                {
                    IsUpdateAvailable = latestVersion != currentSemVer,
                    InstalledVersion = currentSemVer,
                    LatestVersionAvailable = latestVersion
                };
                UpdateData(updateCheckResult);
                return Result<UpdateCheckResult>.Success(updateCheckResult);
            }
            catch (Exception e)
            {
                UpdateData(null);
                Log.Error($"There was an error while trying to check for updates: {e}");
                return Result<UpdateCheckResult>.Failure(e);
            }
        }

        private static void UpdateData(UpdateCheckResult? result)
        {
            _lastUpdate = EditorApplication.timeSinceStartup;
            if (result.HasValue)
                LastUpdateCheckResult = result.Value;
            SessionState.SetString("EasyLogin::updateCheck::LastUpdate", _lastUpdate.ToString(CultureInfo.InvariantCulture));
        }
    }
}
