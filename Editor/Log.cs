using UnityEngine;

namespace Foxscore.EasyLogin
{
    public static class Log
    {
        private const string Prefix =
            "<color=grey>[</color><b><color=cyan>Easy Login</color></b><color=grey>]</color> ";
        
        public static void Info(string message) => Debug.Log(Prefix + message);
        public static void Warning(string message) => Debug.LogWarning(Prefix + message);
        public static void Error(string message) => Debug.LogError(Prefix + message);
    }
}