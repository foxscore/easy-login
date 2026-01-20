using UnityEngine;

namespace Foxscore.EasyLogin
{
    public static class Log
    {
        private const string Prefix =
            "<color=grey>[</color><b><color=cyan>Easy Login</color></b><color=grey>]</color> ";
        
        public static void Info(string message) => UnityEngine.Debug.Log(Prefix + message);
        public static void Warning(string message) => UnityEngine.Debug.LogWarning(Prefix + message);
        public static void Error(string message) => UnityEngine.Debug.LogError(Prefix + message);

        public static void Debug(string message)
        {
#if FOXY_DEBUG
            UnityEngine.Debug.Log(
                "<color=grey>[<b>Easy Login</b>]</color> " +
                message
            );
#endif
        }
    }
}