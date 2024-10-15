using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin.KeyringManagers
{
    public class UnsecureCredentialsManager : KeyringManager
    {
        private Dictionary<string, string> _creds = new();
        private readonly string _configPath;
        private readonly FileSystemWatcher _watcher;

        public UnsecureCredentialsManager(IEncryptionLayer encryptionLayer) : base(encryptionLayer)
        {
            var elDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fox_score", "EasyLogin");
            _configPath = Path.Combine(elDir, "creds.json");
            if (!Directory.Exists(elDir))
                Directory.CreateDirectory(elDir);

            Load();

            _watcher = new FileSystemWatcher(elDir, "creds.json");
            _watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite |
                                    NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.Changed += OnFileWatcherFoundChange;
            _watcher.Created += OnFileWatcherFoundChange;
            _watcher.Deleted += OnFileWatcherFoundChange;
            _watcher.Renamed += OnFileWatcherFoundChange;
            _watcher.EnableRaisingEvents = true;

            AssemblyReloadEvents.beforeAssemblyReload += () => _watcher.EnableRaisingEvents = false;
        }
        
        private void OnFileWatcherFoundChange(object sender, FileSystemEventArgs e) => Load();

        private void Load()
        {
            if (!File.Exists(_configPath))
            {
                _creds = new();
                Save();
                return;
            }

            var json = File.ReadAllText(_configPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _creds = new();
                Save();
                return;
            }

            try
            {
                _creds = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var backupPath = _configPath + ".old";
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Move(_configPath, backupPath);
                _creds = new();
                Save();
                EditorApplication.delayCall += () =>
                {
                    if (!EditorUtility.DisplayDialog("Corrupted config file",
                            $"Your EasyLogin file couldn't be loaded.\n\nWe made at backup and stored it at {backupPath}\n\nYour settings have been reset.",
                            "Ok", "Open Folder"))
                    {
#if UNITY_EDITOR_WIN
                        Process.Start("explorer.exe", "/select," + backupPath.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX
                        Process.Start("open", "-R " + ConfigPath);
#elif UNITY_EDITOR_LINUX
                        Process.Start("xdg-open", _configPath);
#endif
                    }
                };
            }
        }

        private void Save()
        {
            var json = JsonConvert.SerializeObject(_creds, Formatting.Indented);
            File.WriteAllText(_configPath, json);
        }

        public override AuthTokens Get(string id)
        {
            return _creds.TryGetValue(id, out var encryptedTokens)
                ? JsonConvert.DeserializeObject<AuthTokens>(
                    EncryptionLayer.Decrypt(encryptedTokens)
                )
                : null;
        }

        public override void Set(string id, AuthTokens tokens) {
            _creds[id] = EncryptionLayer.Encrypt(
                JsonConvert.SerializeObject(tokens)
            );
            Save();
        }

        public override void Delete(string id)
        {
            _creds.Remove(id);
            Save();
        }
    }
}