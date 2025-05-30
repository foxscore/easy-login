using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BestHTTP.Authentication;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Debug = UnityEngine.Debug;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin
{
    public enum StyleOption
    {
        Square = 0,
        Rounded = 1,
        Circular = 2,
    }

    public enum ProxyScope
    {
        Disabled,
        Internal,
        GlobalPatch,
    }

    public enum ProxySource
    {
        System = 1,
        Custom = 2,
    }

    public enum ProxyType
    {
        Http,
        Socks,
    }

    [Serializable]
    public sealed class ProxySettings
    {
        public ProxyScope scope = ProxyScope.Internal;
        public ProxySource source = ProxySource.System;
        public ProxyType type = ProxyType.Socks;
        public string address;

        #region Only for http proxies

        public bool isTransparent = false;
        public bool sendWholeUrl = true;
        public bool nonTransparentForHttps = true;

        #endregion

        public bool useAuthentication;
        public AuthenticationTypes authenticationType = AuthenticationTypes.Basic;
        public string username;
        public string password;
    }

    [InitializeOnLoad]
    public class Config
    {
        [JsonProperty("enabled")] private bool _enabled = true;
        [JsonProperty("version")] private int _version;
        [JsonProperty("profilePictureStyle")] private StyleOption _profilePictureStyle = StyleOption.Rounded;
        [JsonProperty("profilePictureRadius")] private float _profilePictureRadius = 0.25f;

        [JsonProperty("keepVaultUnlockedForSession")]
        private bool _keepVaultUnlockedForSession = true;

        [JsonProperty("encryption")] private EncryptionLayerType _encryptionLayerType = EncryptionLayerType.Basic;
        [JsonProperty("encryptionCompare")] private string _encryptionCompare = null;

        [JsonProperty("proxy")] private ProxySettings _proxy = new()
        {
            source = ProxySource.System,
        };

        [JsonProperty("accounts")] private readonly List<AccountStruct> _accounts = new();

        private static Config _instance;
        private static readonly string ConfigPath;
        private static readonly FileSystemWatcher Watcher;
        private static readonly object Lock = new();

        public static bool Enabled
        {
            get => _instance._enabled;
            set
            {
                _instance._enabled = value;
                Save();
            }
        }

        public static int Version => _instance._version;

        public static EncryptionLayerType EncryptionLayerType => _instance._encryptionLayerType;
        public static string EncryptionCompare => _instance._encryptionCompare;

        public static bool KeepVaultUnlockedForSession
        {
            get => _instance._keepVaultUnlockedForSession;
            set
            {
                _instance._keepVaultUnlockedForSession = value;
                Save();
            }
        }

        public static StyleOption ProfilePictureStyle
        {
            get => _instance._profilePictureStyle;
            set
            {
                _instance._profilePictureStyle = value;
                Save();
            }
        }

        public static float ProfilePictureRadius
        {
            get => _instance._profilePictureRadius;
            set
            {
                _instance._profilePictureRadius = Mathf.Clamp(value, 0, 0.5f);
                Save();
            }
        }

        public static ProxySettings Proxy
        {
            get => _instance._proxy;
            set
            {
                _instance._proxy = value;
                Save();
            }
        }

        static Config()
        {
            var elDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fox_score", "EasyLogin");
            ConfigPath = Path.Combine(elDir, "config.json");
            if (!Directory.Exists(elDir))
                Directory.CreateDirectory(elDir);

            Load();

            Watcher = new FileSystemWatcher(elDir, "config.json");
            Watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite |
                                   NotifyFilters.FileName | NotifyFilters.DirectoryName;
            Watcher.Changed += OnFileWatcherFoundChange;
            Watcher.Created += OnFileWatcherFoundChange;
            Watcher.Deleted += OnFileWatcherFoundChange;
            Watcher.Renamed += OnFileWatcherFoundChange;
            Watcher.EnableRaisingEvents = true;

            AssemblyReloadEvents.beforeAssemblyReload += () => Watcher.EnableRaisingEvents = false;
        }

        public static void ChangeEncryptionMethod(
            IEncryptionLayer oldEncryptionLayer,
            IEncryptionLayer newEncryptionLayer,
            bool reloadDomain = true
        )
        {
            var oldKeyring = Accounts.GetKeyringManager(oldEncryptionLayer);
            var newKeyring = Accounts.GetKeyringManager(newEncryptionLayer);

            var ids = _instance._accounts.Select(a => a.Id).ToList();
            foreach (var id in ids)
            {
                var tokens = oldKeyring.Get(id);
                if (tokens == null)
                    continue;
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
            IEncryptionLayer.ClearSessionPassword();

            Save();

            Accounts.KeyringManager = Accounts.GetKeyringManager(newEncryptionLayer);
            if (reloadDomain)
                CompilationPipeline.RequestScriptCompilation();
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

            string json;
            lock (Lock)
            {
                json = File.ReadAllText(ConfigPath);
            }

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

            var currentVersion = Version;

            if (Version == 0)
            {
#if UNITY_EDITOR_WIN
                Log.Info("Updating encryption for EasyLogin credentials...");
                ChangeEncryptionMethod(new NoEncryption(), new BasicEncryption(), false);
                CompilationPipeline.RequestScriptCompilation();
#endif
                _instance._version = 1;
                Save();
            }

            if (Version == 1)
            {
                if (EditorPrefs.HasKey("Foxscore_EasyLogin::UseOriginalLoginSystem"))
                {
                    _instance._enabled = !EditorPrefs.GetBool("Foxscore_EasyLogin::UseOriginalLoginSystem", false);
                }

                if (EditorPrefs.HasKey("Foxscore_EasyLogin::ProfilePictureStyle"))
                {
                    _instance._profilePictureStyle =
                        (StyleOption)EditorPrefs.GetInt("Foxscore_EasyLogin::ProfilePictureStyle",
                            (int)StyleOption.Rounded);
                }

                if (EditorPrefs.HasKey("Foxscore_EasyLogin::ProfilePictureRadius"))
                {
                    _instance._profilePictureRadius = Mathf.Clamp(
                        EditorPrefs.GetFloat("Foxscore_EasyLogin::ProfilePictureRadius", 0.25f),
                        0, 0.5f
                    );
                }

                _instance._version = 2;
                Save();
            }

            if (currentVersion != Version)
                CompilationPipeline.RequestScriptCompilation();

            #endregion
        }

        private static void Save()
        {
            lock (Lock)
            {
                var json = JsonConvert.SerializeObject(_instance, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
        }
    }
}