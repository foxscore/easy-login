using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin
{
    [InitializeOnLoad]
    public static class Log
    {
        static Log()
        {
            InitializeLogFile();
            ClearOldLogs();
        }
        
        private static readonly object Lock = new();
        private static FileStream _fileStream;

        private static void InitializeLogFile()
        {
            var logsDir = Path.Combine(Application.dataPath, "..", "Logs");
            if (!Directory.Exists(logsDir))
                Directory.CreateDirectory(logsDir);
            var filePath = Path.Combine(logsDir, $"easy-login_{DateTime.Now:yyMMdd}.log");
            _fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            _fileStream.Seek(0, SeekOrigin.End);

            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                lock (Lock)
                {
                    _fileStream.Dispose();
                }
            };
        }

        private static void ClearOldLogs()
        {
            const int maxLogFiles = 10;
            
            var logsDir = new DirectoryInfo(Path.Combine(Application.dataPath, "..", "Logs"));
            var files = logsDir
                .GetFiles("easy-login_*")
                .OrderBy(f => f.LastWriteTime)
                .ToList();
            while (files.Count > maxLogFiles)
            {
                try
                {
                    files[0].Delete();
                }
                catch (Exception e)
                {
                    WriteToLogFile("ERR", $"Failed to delete log file `{files[0].Name}`", e);
                }
                finally
                {
                    files.RemoveAt(0);
                }
            }
        }

        // * I don't know how exactly this regex works, but it does.
        private static readonly Regex RichTextRegex = new(@"<([^=>/]+)(?:=[^>]+)?>(.*?)</\1>", RegexOptions.Compiled | RegexOptions.Singleline);
        private static void WriteToLogFile(string type, string message, [CanBeNull] Exception exception = null)
        {
            message = RichTextRegex.Replace(message, "$2");
            var str = $"[{DateTime.Now:yy-MM-dd} {DateTime.Now:HH:mm:ss}] [{type}] {message}";
            if (!str.EndsWith('\n'))
                str += '\n';
            if (exception != null)
                str += exception.ToString() + '\n';
            var bytes = Encoding.UTF8.GetBytes(str);
            lock (Lock)
            {
                _fileStream.Write(bytes, 0, bytes.Length);
                _fileStream.Flush();
            }
        }
        
        private const string UnityDebugPrefix =
            "<color=grey>[</color><b><color=cyan>Easy Login</color></b><color=grey>]</color> ";

        public static void Debug(string message, Exception exception = null)
        {
#if FOXY_DEBUG
            UnityEngine.Debug.Log("<color=grey>[<b>Easy Login</b>]</color> " + message);
#endif
            WriteToLogFile("DBG", message, exception);
        }

        public static void Info(string message, Exception exception = null)
        {
            UnityEngine.Debug.Log(UnityDebugPrefix + message);
            WriteToLogFile("INF", message, exception);
        }
        public static void Warning(string message, Exception exception = null)
        {
            UnityEngine.Debug.LogWarning(UnityDebugPrefix + message);
            WriteToLogFile("WRN", message, exception);
        }

        public static void Error(string message, Exception exception = null)
        {
            UnityEngine.Debug.LogError(UnityDebugPrefix + message);
            WriteToLogFile("ERR", message, exception);
        }
    }
}
