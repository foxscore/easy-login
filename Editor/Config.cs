using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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

    [InitializeOnLoad]
    public class Config
    {
        [JsonProperty("enabled")] private bool _enabled = true;
        [JsonProperty("version")] private int _version = 0;
        [JsonProperty("profilePictureStyle")] private StyleOption _profilePictureStyle = StyleOption.Rounded;
        [JsonProperty("profilePictureRadius")] private float _profilePictureRadius = 0.25f;

        [JsonProperty("keepVaultUnlockedForSession")]
        private bool _keepVaultUnlockedForSession = true;

        [JsonProperty("encryption")] private EncryptionLayerType _encryptionLayerType = EncryptionLayerType.Basic;
        [JsonProperty("encryptionCompare")] private string _encryptionCompare = null;

        [JsonProperty("accounts")] private readonly List<AccountStruct> _accounts = new();

        private static Config _instance;
        private static readonly SafeFileHandler FileHandler;

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

        static Config()
        {
            var elDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fox_score", "EasyLogin");
            var configPath = Path.Combine(elDir, "config.json");
            if (!Directory.Exists(elDir))
                Directory.CreateDirectory(elDir);

            FileHandler = new SafeFileHandler(configPath, Load);
            Load(FileHandler.ReadAllText());
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

        private static Config MakeDefault() => new()
        {
            _version = 2,
        };

        private static void Load(string fileContent)
        {
            if (string.IsNullOrEmpty(fileContent))
            {
                Thread.Sleep(10);
                fileContent = FileHandler.ReadAllText();
                if (string.IsNullOrEmpty(fileContent))
                {
                    // Now we know for sure that the config file is either empty or non-existent
                    // * Write to log file(when implemented)
                    _instance = MakeDefault();
                    Save();
                    return;
                }
            }

            try
            {
                _instance = JsonConvert.DeserializeObject<Config>(fileContent);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                FileHandler.MakeBackup();
                _instance = MakeDefault();
                Save();
                EditorApplication.delayCall += () =>
                {
                    if (!EditorUtility.DisplayDialog("Corrupted config file",
                            $"Your EasyLogin file couldn't be loaded.\n\nWe made at backup and stored it at {FileHandler.BackupPath}\n\nYour settings have been reset.",
                            "Ok", "Open Folder"))
                    {
                        var folder = Path.GetDirectoryName(FileHandler.BackupPath)!;
#if UNITY_EDITOR_WIN
                        Process.Start("explorer.exe", "/select," + folder.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX
                    Process.Start("open", "-R " + folder);
#elif UNITY_EDITOR_LINUX
                        Process.Start("xdg-open", folder);
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
            var json = JsonConvert.SerializeObject(_instance, Formatting.Indented);
            FileHandler.WriteAllText(json);
        }
    }
}