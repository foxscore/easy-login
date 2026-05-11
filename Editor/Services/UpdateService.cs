using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Security.Cryptography;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Version = Foxscore.EasyLogin.SemanticVersioning.Version;
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
        private const string IndexUrl = "https://short.foxscore.dev/easy-login-vpm-index-json";
        private const double TimeBetweenUpdates = 15 * 60; // 15 Minutes
        
        private static double _lastUpdate = -100 - TimeBetweenUpdates; // * Default value must be low enough to trigger an update on startup
        private static DateTime? _lastUpdateDateTime;

        public static bool IsChecking { get; private set; }
        public static UpdateCheckResult? LastUpdateCheckResult { get; private set; }
        public static bool IsUpdateAvailable => LastUpdateCheckResult is { IsUpdateAvailable: true };

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (!Is.FirstRun()) return;

            var rawPreviousLastUpdate = SessionState.GetString("EasyLogin::updateCheck::LastUpdate", null);
            if (!string.IsNullOrWhiteSpace(rawPreviousLastUpdate))
            {
                var lastUpdateValue = double.Parse(rawPreviousLastUpdate, CultureInfo.InvariantCulture);
                _lastUpdate = lastUpdateValue;
            }

            LoadStateFromDisk();
            EditorApplication.update += BackgroundTick;
        }

        private static string TempDirPath = Path.Combine(Application.dataPath, "..", "Temp", "EasyLogin","cache");
        private static string StatePath => Path.Combine(TempDirPath, "updates.json");

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
                if (File.Exists(configPath))
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
                Version latestVersion = null;
                foreach (var version in versions)
                {
                    var semVer = Version.Parse(version);
                    if (semVer.IsPreRelease && !shouldRespectPreReleases)
                    {
                        // Always include pre-releases of the same base version when currently on a pre-release
                        if (!currentSemVer.IsPreRelease || semVer.BaseVersion() != currentSemVer.BaseVersion())
                            continue;
                    }
                    if (latestVersion == null || semVer > latestVersion)
                        latestVersion = semVer;
                }

                // Done
                var updateCheckResult = new UpdateCheckResult()
                {
                    IsUpdateAvailable = latestVersion > currentSemVer,
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
                        LastUpdateCheckResult.Value.IsUpdateAvailable != result.Value.IsUpdateAvailable ||
                        LastUpdateCheckResult.Value.InstalledVersion != result.Value.InstalledVersion ||
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
            if (!LastUpdateCheckResult.HasValue || LastUpdateCheckResult.Value.LatestVersionAvailable == null)
            {
                Log.Error("Update data missing");
                return;
            }
            
            Log.Debug($"Beginning update from {LastUpdateCheckResult.Value.InstalledVersion} to {LastUpdateCheckResult.Value.LatestVersionAvailable}");
            EditorApplication.LockReloadAssemblies();
            
            // Paths
            var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "vpm-manifest.json");
            var installationPath = Path.Combine(Application.dataPath, "..", "Packages", "dev.foxscore.easy-login");
            var tempPath = Path.Combine(Application.dataPath, "..", "Temp", "EasyLogin");
            var zipDownloadPath = Path.Combine(tempPath, "update.zip");
            var backupPathRoot = Path.Combine(tempPath, "update-snapshot");
            var manifestBackupPath = Path.Combine(backupPathRoot, "vpm-manifest.json");
            var installationBackupPath = Path.Combine(backupPathRoot, "dev.foxscore.easylogin");
            
            Log.Debug("Preparing update...");
            EditorUtility.DisplayProgressBar("Easy Login Update", "Getting ready", 0);
            if (Directory.Exists(backupPathRoot))
                ForceDeleteDirectory(backupPathRoot);
            else if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);
                
            try
            {
                // Get available versions
                EditorUtility.DisplayProgressBar("Easy Login Update", "Fetching update metadata", 0);
                Log.Debug("Getting update metadata...");
                using var client = new HttpClient();
                client.SetEasyLoginUserAgent();
                var rawJson = client.GetStringAsync(IndexUrl).Result;
                var abstractIndex = JsonConvert.DeserializeObject<Abstract.Index>(rawJson);
                var versionsObjects = abstractIndex.Packages.EasyLogin.Versions;
                var wantedVersionObject = versionsObjects
                    .Properties()
                    .First(p => p.Name == LastUpdateCheckResult.Value.LatestVersionAvailable.ToString())
                    .Value
                    .ToObject<Abstract.PackageJson>();
                Log.Debug($"Discovered url: {wantedVersionObject.ZipDownloadUrl}");
                
                // Download package
                EditorUtility.DisplayProgressBar("Easy Login Update", "Downloading update", 0);
                Log.Debug($"Downloading package to {zipDownloadPath}");
                using var response = client.GetAsync(wantedVersionObject.ZipDownloadUrl, HttpCompletionOption.ResponseHeadersRead).Result;
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength;
                using var downloadStream = response.Content.ReadAsStreamAsync().Result;
                using var fileStream = new FileStream(zipDownloadPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                var buffer = new byte[8192];
                long totalRead = 0;
                int read;
                while ((read = downloadStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fileStream.Write(buffer, 0, read);
                    totalRead += read;

                    if (totalBytes.HasValue)
                    {
                        var progress = (float)totalRead / totalBytes.Value;
                        EditorUtility.DisplayProgressBar("Easy Login Update", "Downloading update", progress);
                    }
                }
                fileStream.Flush();
                fileStream.Seek(0, SeekOrigin.Begin);
                Log.Debug("Download complete");
                
                if (string.IsNullOrWhiteSpace(wantedVersionObject.ZipSHA256))
                    Log.Debug("No SHA256 hash provided for this version.");
                else
                {
                    Log.Debug("Computing SHA256 hash of the downloaded file.");
                    using var hasher = SHA256.Create();
                    var hash = hasher.ComputeHash(fileStream);
                    fileStream.Seek(0, SeekOrigin.Begin);
                    var hashString = BitConverter.ToString(hash).Replace("-", string.Empty);
                    if (!hashString.Equals(wantedVersionObject.ZipSHA256, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Error($"SHA256 hash mismatch for downloaded file. Computed: '{hashString}', Expected: '{wantedVersionObject.ZipSHA256}'");
                        throw new SecurityException("Downloaded file hash does not match expected value.");
                    }Log.Debug("SHA256 hashes match successfully.");
                }
                
                Directory.CreateDirectory(backupPathRoot);
                CopyDirectory(installationPath, installationBackupPath);
                if (File.Exists(manifestPath))
                    File.Copy(manifestPath, manifestBackupPath);
                
                // Install package
                EditorUtility.DisplayProgressBar("Easy Login Update", "Installing update", 0);
                Log.Debug("Deleting current installation");
                ForceDeleteDirectory(installationPath);
                Directory.CreateDirectory(installationPath);
                using var zip = new ZipArchive(fileStream, ZipArchiveMode.Read);
                zip.ExtractToDirectory(installationPath);
                
                if (File.Exists(manifestPath))
                {
                    var rawManifest = File.ReadAllText(manifestPath);
                    var json = JsonConvert.DeserializeObject<JObject>(rawManifest);
                    var dependencies = json["dependencies"]!.Value<JObject>();
                    if (dependencies.TryGetValue("dev.foxscore.easy-login", out var dependencyEntry))
                        dependencyEntry["version"] = wantedVersionObject.VersionString;
                    var locked = json["locked"]!.Value<JObject>();
                    if (locked.TryGetValue("dev.foxscore.easy-login", out var lockedEntry))
                        lockedEntry["version"] = wantedVersionObject.VersionString;
                    rawManifest = JsonConvert.SerializeObject(json, Formatting.Indented);
                    File.WriteAllText(manifestPath, rawManifest);
                }

                Log.Info("Update complete");
                EditorUtility.DisplayDialog("Easy Login Update", "Update complete", "Reload Unity");
            }
            catch (Exception e)
            {
                Log.Error("Failed to install update", e);
                EditorUtility.DisplayDialog(
                    "Easy Login",
                    "There was an issue while installing the update. Please update using ALCOM or the VRChat Creator Companion.",
                    "Ok"
                );

                if (Directory.Exists(backupPathRoot))
                {
                    Log.Debug("Performing rollback");
                    EditorUtility.DisplayProgressBar("Easy Login Update", "Performing rollback", 0);
                    ForceDeleteDirectory(installationPath);
                    CopyDirectory(installationBackupPath, installationPath);
                    if (File.Exists(manifestBackupPath))
                        File.Copy(manifestBackupPath, manifestPath, true);
                }
            }
            finally
            {
#if FOXY_DEBUG
                Log.Debug("Skipping cleanup (FOXY_DEBUG)");
#else
                if (File.Exists(zipDownloadPath))
                {
                    Log.Debug("Deleting downloaded file");
                    EditorUtility.DisplayProgressBar("Easy Login Update", "Cleaning up", 0);
                    File.Delete(zipDownloadPath);
                }
                
                if (Directory.Exists(backupPathRoot))
                {
                    Log.Debug("Deleting backup");
                    EditorUtility.DisplayProgressBar("Easy Login Update", "Cleaning up", 0);
                    ForceDeleteDirectory(backupPathRoot);
                }
#endif

                EditorUtility.ClearProgressBar();
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.Refresh();
                EditorUtility.RequestScriptReload();
            }
        }
    
        // * In case the .git directory exists
        static void ForceDeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;

            var directory = new DirectoryInfo(path);

            // Set attributes of all files to Normal (removing Read-Only)
            foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes = FileAttributes.Normal;
            }

            // Set attributes of all subdirectories to Normal
            foreach (var dir in directory.GetDirectories("*", SearchOption.AllDirectories))
            {
                dir.Attributes = FileAttributes.Normal;
            }

            // Now the recursive delete will work
            directory.Delete(true);
        }
        
        // From: https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        static void CopyDirectory(string sourceDir, string destinationDir)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // Recursively call this method
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}