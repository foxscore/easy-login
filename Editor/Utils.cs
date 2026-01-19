using System;
using System.Net.Http;

namespace Foxscore.EasyLogin
{
    public static class Utils
    {
        public static void SetEasyLoginUserAgent(this HttpClient client, string version)
        {
            var userAgent = $"EasyLogin/{version} (Unity Editor, {Environment.OSVersion.VersionString})";
            client.DefaultRequestHeaders.Remove("User-Agent");
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        }
    }
}