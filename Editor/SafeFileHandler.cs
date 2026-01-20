using System;
using System.IO;
using System.Threading;
using System.Timers;
using JetBrains.Annotations;
using UnityEditor;
using Timer = System.Threading.Timer;

namespace Foxscore.EasyLogin
{
    public class SafeFileHandler : IDisposable
    {
        public int DebounceTimerDuration = 100;
        public bool IgnoreChangesOnEmptyFiles = true;
        public bool RunCallbackOnMainThread = true;

        private readonly string _path;
        private readonly object _lock = new();
        private readonly Action<string> _onFileChange;
        private readonly FileSystemWatcher _watcher;
        [CanBeNull] private Timer _debounceTimer;
        
        public string BackupPath => _path + ".bak";

        public SafeFileHandler(string path, Action<string> onFileChange)
        {
            _path = path;
            _onFileChange = onFileChange;

            // Setup file watcher
            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory!);
            _watcher = new FileSystemWatcher(directory!, fileName);
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.Changed += OnFileWatcherFoundChange;
            _watcher.Created += OnFileWatcherFoundChange;
            _watcher.Deleted += OnFileWatcherFoundChange;
            _watcher.Renamed += OnFileWatcherFoundChange;
            _watcher.EnableRaisingEvents = true;

            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private void OnFileWatcherFoundChange(object _, FileSystemEventArgs __)
        {
            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(OnDebounceTimerElapsed, null, DebounceTimerDuration, Timeout.Infinite);
            }
        }

        private void OnDebounceTimerElapsed(object _)
        {
            string content;
            try
            {
                content = File.ReadAllText(_path);
            }
            catch (IOException)
            {
                // File is probably temporarily locked.
                // It's best to just re-schedule the read.
                OnFileWatcherFoundChange(null, null);
                return;
            }

            if (string.IsNullOrEmpty(content) && IgnoreChangesOnEmptyFiles)
                return;
            if (RunCallbackOnMainThread)
                EditorApplication.delayCall += () => _onFileChange(content);
            else
                _onFileChange(content);
        }

        public bool Exists() => File.Exists(_path);

        [CanBeNull]
        public string ReadAllText()
        {
            lock (_lock)
            {
                if (Exists())
                    return File.ReadAllText(_path);
                return null;
            }
        }

        public void WriteAllText(string text)
        {
            lock (_lock)
            {
                if (!Directory.Exists(Path.GetDirectoryName(_path)))
                    Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

                if (!File.Exists(_path))
                {
                    File.WriteAllText(_path, text);
                    return;
                }

                var tempPath = _path + ".new";
                File.WriteAllText(tempPath, text);
                File.Replace(tempPath, _path, null);
            }
        }

        public void MakeBackup()
        {
            lock (_lock)
            {
                File.Copy(_path, _path + ".bak", true);
            }
        }

        public void Dispose()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }

            _debounceTimer?.Dispose();
        }
    }
}