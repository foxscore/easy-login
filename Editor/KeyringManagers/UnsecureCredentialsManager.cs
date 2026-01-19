using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin.KeyringManagers
{
    public class UnsecureCredentialsManager : KeyringManager
    {
        private static readonly object Lock = new();
        
        private Dictionary<string, string> _creds = new();
        private readonly SafeFileHandler _fileHandler;

        public UnsecureCredentialsManager(IEncryptionLayer encryptionLayer) : base(encryptionLayer)
        {
            var elDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Fox_score", "EasyLogin");
            var credsPath = Path.Combine(elDir, "creds.json");
            if (!Directory.Exists(elDir))
                Directory.CreateDirectory(elDir);

            _fileHandler = new SafeFileHandler(credsPath, Load);
            Load(_fileHandler.ReadAllText());
        }

        private void Load(string fileContent)
        {
            if (string.IsNullOrEmpty(fileContent))
            {
                Thread.Sleep(10);
                fileContent = _fileHandler.ReadAllText();
                if (string.IsNullOrEmpty(fileContent))
                {
                    // Now we know for sure that the credentials file is either empty or non-existent
                    // * Write to log file(when implemented)
                    _creds = new();
                    Save();
                    return;
                }
            }

            try
            {
                _creds = JsonConvert.DeserializeObject<Dictionary<string, string>>(fileContent);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _fileHandler.MakeBackup();
                _creds = new();
                Save();
                var accounts = Config.GetAccounts().Select(a => a.Id).ToList();
                accounts.ForEach(Config.RemoveAccount);
                EditorApplication.delayCall += () =>
                {
                    if (!EditorUtility.DisplayDialog("Corrupted credentials file",
                            $"Your EasyLogin credentials file couldn't be loaded.\n\nWe made at backup and stored it at {_fileHandler.BackupPath}\n\nYour stored accounts have been reset.",
                            "Ok", "Open Folder"))
                    {
                        var folder = Path.GetDirectoryName(_fileHandler.BackupPath)!;
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
        }

        private void Save()
        {
            lock (Lock)
            {
                var json = JsonConvert.SerializeObject(_creds, Formatting.Indented);
                _fileHandler.WriteAllText(json);
            } 
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
