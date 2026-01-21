using System;
using System.IO;
using System.Net.Http;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace Foxscore.EasyLogin
{
    public static class Utils
    {
        public static string GetUserAgentValue()
        {
            var version = GetPackageJson().VersionString;
            return $"EasyLogin/{version} (Unity Editor, {Environment.OSVersion.VersionString})";
        }
        
        public static void SetEasyLoginUserAgent(this HttpClient client)
        {
            client.DefaultRequestHeaders.Remove("User-Agent");
            client.DefaultRequestHeaders.Add("User-Agent", GetUserAgentValue());
        }

        [CanBeNull] private static Abstract.PackageJson _packageJsonCache;
        public static Abstract.PackageJson GetPackageJson()
        {
            if (_packageJsonCache == null)
            {
                var rawPackageJson =File.ReadAllText(Path.Combine(Application.dataPath, "..", "Packages", "dev.foxscore.easy-login", "package.json"));
                _packageJsonCache = JsonConvert.DeserializeObject<Abstract.PackageJson>(rawPackageJson);
            }
            return _packageJsonCache;
        }
    }
}