using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Foxscore.EasyLogin
{
    public static class PlatformUtils
    {
        private static bool? _isPlatformSupported;

        public static bool IsPlatformSupported()
        {
            if (_isPlatformSupported is not null)
                return _isPlatformSupported.Value;

            if (
                RuntimeInformation.ProcessArchitecture is not Architecture.Arm and not Architecture.Arm64 ||
                typeof(Harmony).Assembly.GetName().Version is { Major: >= 2, Minor: >= 4 }
            )
                return (_isPlatformSupported = true).Value;

            if (SessionState.GetBool("EasyLogin::ArmPlatformCheck::DisplayedWarningMessage", false))
                return (_isPlatformSupported = false).Value;

            SessionState.SetBool("EasyLogin::ArmPlatformCheck::DisplayedWarningMessage", true);
            EditorApplication.delayCall += () =>
            {
                if (EditorUtility.DisplayDialog(
                        "Incompatible Library",
                        "Warning: You are currently using the Unity Editor for ARM64.\n\n" +
                        "The version of Harmony Library installed in this project is unsupported on this platform. Easy Login, and other packages may fail to work correctly.\n\n" +
                        "This package offers the choice to install a community build of Harmony, which may be uninstalled at any time.\n\n" +
                        "Otherwise, please use the Intel version of Unity, or manually install a compatible version of Harmony for .NET 4.5.2.\n\n" +
                        $"Harmony Version: {typeof(Harmony).Assembly.GetName().Version} | Minimum Required: 2.4.0.0\n",
                        ok: "Install community build",
                        cancel: "Ignore for this session"
                    ))
                    InstallCommunityBuildOfHarmony();
            };
            return (_isPlatformSupported = false).Value;
        }

        private static void InstallCommunityBuildOfHarmony()
        {
            const string releaseUrl = "https://api.github.com/repos/MisutaaAsriel/VRCHarmony/releases/latest";
            using var webClient = new WebClient();

            // Set User-Agent header (required by GitHub API)
            webClient.Headers.Add("User-Agent", "Easy Login");

            Log.Info("Fetching latest release information...");
            var releaseJson = webClient.DownloadString(releaseUrl);

            // Parse JSON using Newtonsoft.Json
            var releaseData = JObject.Parse(releaseJson);

            // Find the correct ZIP asset (not source code)
            var assets = releaseData["assets"] as JArray;
            var targetAsset = assets?.FirstOrDefault(asset =>
            {
                var name = asset["name"]?.ToString();
                return name != null &&
                       name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                       name.Contains("dev.pardeike.harmony") &&
                       !name.Equals("Source code (zip)", StringComparison.OrdinalIgnoreCase);
            });

            if (targetAsset == null)
                throw new InvalidOperationException("Could not find the harmony ZIP asset in the latest release");

            var downloadUrl = targetAsset["browser_download_url"]?.ToString();
            var fileName = targetAsset["name"]?.ToString();

            if (downloadUrl is null || fileName is null)
                throw new InvalidOperationException("Could not find the harmony ZIP asset in the latest release");

            Log.Info($"Found asset: {fileName}");
            Log.Info($"Download URL: {downloadUrl}");

            // Create temporary file path
            var tempDir = Path.GetTempPath();
            var tempFilePath = Path.Combine(tempDir, fileName);

            // Download the ZIP file
            Log.Info("Downloading...");
            webClient.DownloadFile(downloadUrl, tempFilePath);

            Log.Info($"Downloaded to: {tempFilePath}");

            // Create target directory
            var targetDir = Path.Combine(Application.dataPath, "..", "Packages", "dev.pardeike.harmony");
            if (Directory.Exists(targetDir))
            {
                Log.Warning("The community build appears to already be installed, but something is clearly not working. Removing old installation...");
                Directory.Delete(targetDir, true);
            }
            Directory.CreateDirectory(targetDir);

            // Extract ZIP file
            Log.Info($"Extracting to: {targetDir}");
            EditorApplication.LockReloadAssemblies();
            ZipFile.ExtractToDirectory(tempFilePath, targetDir);
            EditorApplication.UnlockReloadAssemblies();

            // Clean up temporary file
            File.Delete(tempFilePath);

            Log.Info("Successfully downloaded and extracted VRCHarmony!");

            EditorUtility.DisplayDialog(
                "Restart Required",
                "The Unity Editor will now restart to finish importing libraries.",
                "Continue"
            );
            EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }
    }
}
