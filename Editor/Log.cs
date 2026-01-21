using System;
using System.Diagnostics;
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
        
        // Internal calls
        private const string UnityDebugPrefix =
            "<color=grey>[</color><b><color=cyan>Easy Login</color></b><color=grey>]</color> ";
        private static void Internal_Debug(Type type, string message, Exception exception)
        {
#if FOXY_DEBUG
            UnityEngine.Debug.Log("<color=grey>[<b>Easy Login</b>]</color> " + message);
#endif
            WriteToLogFile("DBG", $"[{type?.Name ?? "ERR_NoType"}] {message}", exception);
        }
        private static void Internal_Info(Type type, string message, Exception exception)
        {
            UnityEngine.Debug.Log(UnityDebugPrefix + message);
            WriteToLogFile("INF", $"[{type?.Name ?? "ERR_NoType"}] {message}", exception);
        }
        private static void Internal_Warning(Type type, string message, Exception exception)
        {
            UnityEngine.Debug.Log(UnityDebugPrefix + message);
            WriteToLogFile("WRN", $"[{type?.Name ?? "ERR_NoType"}] {message}", exception);
        }
        private static void Internal_Error(Type type, string message, Exception exception)
        {
            UnityEngine.Debug.Log(UnityDebugPrefix + message);
            WriteToLogFile("ERR", $"[{type?.Name ?? "ERR_NoType"}] {message}", exception);
        }
        
        // Type-fetching calls
        public static void Debug(string message, Exception exception = null)
        {
            var callerType = new StackTrace()
                .GetFrame(1)
                .GetMethod()
                .DeclaringType;
            while (true == callerType?.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false))
                callerType = callerType.DeclaringType;
            Internal_Debug(callerType, message, exception);
        }
        public static void Info(string message, Exception exception = null)
        {
            var callerType = new StackTrace()
                .GetFrame(1)
                .GetMethod()
                .DeclaringType;
            while (true == callerType?.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false))
                callerType = callerType.DeclaringType;
            Internal_Info(callerType, message, exception);
        }
        public static void Warning(string message, Exception exception = null)
        {
            var callerType = new StackTrace()
                .GetFrame(1)
                .GetMethod()
                .DeclaringType;
            while (true == callerType?.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false))
                callerType = callerType.DeclaringType;
            Internal_Warning(callerType, message, exception);
        }
        public static void Error(string message, Exception exception = null)
        {
            var callerType = new StackTrace()
                .GetFrame(1)
                .GetMethod()
                .DeclaringType;
            while (true == callerType?.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false))
                callerType = callerType.DeclaringType;
            Internal_Error(callerType, message, exception);
        }
        
        // Typed calls
        public static void Debug<T>(string message, Exception exception = null) => Internal_Debug(typeof(T), message, exception);
        public static void Info<T>(string message, Exception exception = null) => Internal_Info(typeof(T), message, exception);
        public static void Warning<T>(string message, Exception exception = null) => Internal_Warning(typeof(T), message, exception);
        public static void Error<T>(string message, Exception exception = null) => Internal_Error(typeof(T), message, exception);
    }
}
