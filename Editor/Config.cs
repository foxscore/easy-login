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
using VRC.SDKBase.Editor.Api;
using Debug = UnityEngine.Debug;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin
{
    [InitializeOnLoad]
    public class Config
    {
        [JsonProperty("accounts")] private readonly List<AccountStruct> _accounts = new();
        [JsonProperty("version")] private int _version = 0;
        [JsonProperty("encryption")] private EncryptionLayerType _encryptionLayerType = EncryptionLayerType.Basic;
        [JsonProperty("encryptionCompare")] private string _encryptionCompare = null;

        private static Config _instance;
        private static readonly string ConfigPath;
        private static readonly FileSystemWatcher _watcher;

        public static int Version
        {
            get => _instance._version;
            set
            {
                _instance._version = value;
                Save();
            }
        }

        public static EncryptionLayerType EncryptionLayerType => _instance._encryptionLayerType;
        public static string EncryptionCompare => _instance._encryptionCompare;

        static Config()
        {
            var elDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fox_score", "EasyLogin");
            ConfigPath = Path.Combine(elDir, "config.json");
            if (!Directory.Exists(elDir))
                Directory.CreateDirectory(elDir);

            Load();

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

        public static void ChangeEncryptionMethod(
            IEncryptionLayer oldEncryptionLayer,
            IEncryptionLayer newEncryptionLayer
        )
        {
            var oldKeyring = Accounts.GetKeyringManager(oldEncryptionLayer);
            var newKeyring = Accounts.GetKeyringManager(newEncryptionLayer);

            var ids = _instance._accounts.Select(a => a.Id).ToList();
            foreach (var id in ids)
            {
                var tokens = oldKeyring.Get(id);
                oldKeyring.Delete(id);
                newKeyring.Set(id, tokens);
            }

            _instance._encryptionLayerType = newKeyring.EncryptionLayer switch
            {
                BasicEncryption => EncryptionLayerType.Basic,
                PasswordEncryption => EncryptionLayerType.Password,
                _ => throw new ArgumentOutOfRangeException()
            };
            _instance._encryptionCompare = newEncryptionLayer.GetCompareString();
            
            Save();
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
            // Debug.Log("Detected change in EasyLogin config, reloading...");
            Load();
        }

        private static Config MakeDefault() => new()
        {
            _version = 1,
        };

        private static void Load()
        {
            if (!File.Exists(ConfigPath))
            {
                _instance = MakeDefault();
                Save();
                return;
            }

            var json = File.ReadAllText(ConfigPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _instance = MakeDefault();
                Save();
                return;
            }

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
                _instance = MakeDefault();
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

            #region Version upgrades

            switch (Version)
            {
                case 0:
#if UNITY_EDITOR_WIN
                    Log.Info("Updating encryption for EasyLogin credentials...");
                    ChangeEncryptionMethod(new NoEncryption(), new BasicEncryption());
                    Log.Info("DONE!");
#endif
                    break;
            }

            #endregion
        }

        private static void Save()
        {
            var json = JsonConvert.SerializeObject(_instance, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
    }
}