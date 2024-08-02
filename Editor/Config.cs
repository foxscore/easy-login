using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin
{
    [InitializeOnLoad]
    public class Config
    {
        [JsonProperty("accounts")]
        private readonly List<AccountStruct> _accounts = new();

        private static Config _instance;
        private static readonly string ConfigPath;
        private static readonly FileSystemWatcher _watcher;

        static Config()
        {
            var elDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fox_score", "EasyLogin");
            ConfigPath = Path.Combine(elDir, "config.json");
            if (!Directory.Exists(elDir))
                Directory.CreateDirectory(elDir);

            if (File.Exists(ConfigPath))
            {
                Load();
            }
            else
            {
                _instance = new();
                Save();
            }

            _watcher = new FileSystemWatcher(elDir, "config.json");
            _watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite |
                                    NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.Changed += OnFileWatcherFoundChange;
            _watcher.Created += OnFileWatcherFoundChange;
            _watcher.Deleted += OnFileWatcherFoundChange;
            _watcher.Renamed += OnFileWatcherFoundChange;
            _watcher.EnableRaisingEvents = true;

            AssemblyReloadEvents.beforeAssemblyReload += () => _watcher.EnableRaisingEvents = false;
        }

        public static IReadOnlyList<AccountStruct> GetAccounts() => _instance._accounts;

        internal static void AddAccount(AccountStruct account)
        {
            _instance._accounts.Add(account);
            Save();
        }

        internal static void UpdateAccount(AccountStruct account)
        {
            var acc = _instance._accounts.FirstOrDefault(a => a.Id == account.Id);
            if (acc == null)
            {
                AddAccount(account);
                return;
            }
            _instance._accounts.Remove(acc);
            _instance._accounts.Add(account);
            Save();
        }

        internal static void RemoveAccount(string internalId)
        {
            var acc = _instance._accounts.FirstOrDefault(a => a.Id == internalId);
            if (acc == null) return;
            _instance._accounts.Remove(acc);
            Save();
        }

        private static void OnFileWatcherFoundChange(object sender, FileSystemEventArgs e)
        {
            Debug.Log("Detected change in EasyLogin config, reloading...");
            Load();
        }

        private static void Load()
        {
            if (!File.Exists(ConfigPath))
            {
                _instance = new();
                Save();
                return;
            }

            var json = File.ReadAllText(ConfigPath);
            try
            {
                _instance = JsonConvert.DeserializeObject<Config>(json);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var backupPath = ConfigPath + ".old";
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Move(ConfigPath, backupPath);
                _instance = new Config();
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
                    Process.Start("xdg-open", ConfigPath);
#endif
                    }
                };
            }
        }

        private static void Save()
        {
            var json = JsonConvert.SerializeObject(_instance, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
    }
}