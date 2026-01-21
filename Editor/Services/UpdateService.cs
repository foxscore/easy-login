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

        public static bool IsChecking { get; private set; }
        public static UpdateCheckResult? LastUpdateCheckResult { get; private set; }
        public static bool IsUpdateAvailable => LastUpdateCheckResult is { IsUpdateAvailable: true };

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            var rawPreviousLastUpdate = SessionState.GetString("EasyLogin::updateCheck::LastUpdate", null);
            if (!string.IsNullOrWhiteSpace(rawPreviousLastUpdate))
            {
                var lastUpdateValue = double.Parse(rawPreviousLastUpdate, CultureInfo.InvariantCulture);
                _lastUpdate = lastUpdateValue;
            }

            LoadStateFromDisk();
            EditorApplication.update += BackgroundTick;
        }

        private static string TempDirPath = Path.Combine(Application.dataPath, "..", "Temp", "EasyLogin");
        private static string StatePath => Path.Combine(TempDirPath, "update_check.json");

        private static void LoadStateFromDisk()
        {
            if (!File.Exists(StatePath))
                return;
            var fileContent = File.ReadAllText(StatePath);
            try
            {
                LastUpdateCheckResult = JsonConvert.DeserializeObject<UpdateCheckResult>(fileContent, new VersionSerializer());

                if (LastUpdateCheckResult.Value.InstalledVersion.ToString() != Utils.GetPackageJson().VersionString)
                {
                    Log.Debug("Restored update result is referencing a different installed version, resetting...");
                    LastUpdateCheckResult = null;
                    File.Delete(StatePath);
                }
            }
            catch (Exception e)
            {
                Log.Debug("Failed to restore last update result", e);
                File.Delete(StatePath);
            }
        }

        private static void SaveStateToDisk()
        {
            if (!Directory.Exists(TempDirPath))
                Directory.CreateDirectory(TempDirPath);
            var json = JsonConvert.SerializeObject(LastUpdateCheckResult, Formatting.Indented, new VersionSerializer());
            File.WriteAllText(StatePath, json);
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
            if (IsChecking)
            {
                do
                {
                    await Task.Delay(10);
                } while (IsChecking);
                return LastUpdateCheckResult.HasValue
                    ? Result<UpdateCheckResult>.Success(LastUpdateCheckResult.Value)
                    : Result<UpdateCheckResult>.Failure("Another update check was already running, and it encountered an error.");
            }
            IsChecking = true;

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
                if (!shouldRespectPreReleases || File.Exists(configPath))
                {
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
            finally
            {
                IsChecking = false;
            }
        }

        private static void UpdateData(UpdateCheckResult? result)
        {
            if (
                result.HasValue && (
                    !LastUpdateCheckResult.HasValue || (
                        LastUpdateCheckResult.Value.IsUpdateAvailable != result.Value.IsUpdateAvailable &&
                        LastUpdateCheckResult.Value.InstalledVersion != result.Value.InstalledVersion &&
                        LastUpdateCheckResult.Value.LatestVersionAvailable != result.Value.LatestVersionAvailable
                    )
                )
            )
            {
                LastUpdateCheckResult = result.Value;
                SaveStateToDisk();
            }

            _lastUpdate = EditorApplication.timeSinceStartup;
            SessionState.SetString("EasyLogin::updateCheck::LastUpdate",
                _lastUpdate.ToString(CultureInfo.InvariantCulture));
        }

        public static void InstallUpdate()
        {
            Log.Error("Not yet implemented");
        }
    }
}