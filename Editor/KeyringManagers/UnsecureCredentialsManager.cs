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
    public class UnsecureCredentialsManager : IKeyringManager
    {
        #region Simple Crypto

        private static readonly byte[] Key =
            { 123, 45, 67, 89, 101, 123, 45, 67, 89, 101, 123, 45, 67, 89, 101, 123 };

        // ReSharper disable once InconsistentNaming
        private static readonly byte[] IV = { 12, 34, 56, 78, 90, 12, 34, 56, 78, 90, 12, 34, 56, 78, 90, 12 };

        public static string EncryptString(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            using (var sw = new StreamWriter(cs))
                sw.Write(plainText);

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string DecryptString(string cipherText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        #endregion

        private Dictionary<string, string> _creds = new();
        private readonly string _configPath;
        private readonly FileSystemWatcher _watcher;

        public UnsecureCredentialsManager()
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

        public AuthTokens Get(string id)
        {
            return _creds.TryGetValue(id, out var encryptedTokens)
                ? JsonConvert.DeserializeObject<AuthTokens>(
                    DecryptString(encryptedTokens)
                )
                : null;
        }

        public void Set(string id, AuthTokens tokens) {
            _creds[id] = EncryptString(
                JsonConvert.SerializeObject(tokens)
            );
            Save();
        }

        public void Delete(string id)
        {
            _creds.Remove(id);
            Save();
        }
    }
}