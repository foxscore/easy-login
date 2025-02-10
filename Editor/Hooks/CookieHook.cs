using System;
using System.Reflection;
using BestHTTP.Cookies;
using HarmonyLib;
using UnityEditor;

namespace Foxscore.EasyLogin.Hooks
{
    [InitializeOnLoad]
    public static class CookieHook
    {
        static CookieHook()
        {
            if (!PlatformUtils.IsPlatformSupported())
                return;
            
            var method = typeof(Cookie).GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
            var prefix = typeof(CookieHook).GetMethod(nameof(ParsePrefix), BindingFlags.NonPublic | BindingFlags.Static);

            var harmony = new Harmony("dev.foxscore.easy-login.bestHttp-cookie");
            harmony.Patch(method, new HarmonyMethod(prefix));
        }

        private static void ParsePrefix(ref string header)
        {
            header = RemoveSameSiteFromCookie(header);
        }
        
        private static string RemoveSameSiteFromCookie(string cookieString)
        {
            if (string.IsNullOrEmpty(cookieString))
                return cookieString;

            // Find the index of the 'SameSite' attribute, allowing for optional space
            int sameSiteIndex = cookieString.IndexOf("; SameSite", StringComparison.OrdinalIgnoreCase);
            if (sameSiteIndex == -1)
                sameSiteIndex = cookieString.IndexOf(";SameSite", StringComparison.OrdinalIgnoreCase);

            if (sameSiteIndex >= 0)
            {
                // Find the end of the attribute (either the semicolon or end of string)
                int endIndex = cookieString.IndexOf(';', sameSiteIndex + 9);
                if (endIndex == -1)
                    endIndex = cookieString.Length;

                // Remove the attribute and any trailing semicolon
                return cookieString.Remove(sameSiteIndex, endIndex - sameSiteIndex);
            }

            return cookieString;
        }
    }
}