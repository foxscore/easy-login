#if UNITY_EDITOR_WIN
using System;
using BestHTTP.Cookies;
using CredentialManagement;
using Newtonsoft.Json;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin.KeyringManagers
{
    public class WindowsCredentialKeyringManager : KeyringManager
    {
        private const string ServiceName = "Foxscore_EasyLogin";

        public WindowsCredentialKeyringManager(IEncryptionLayer encryptionLayer) : base(encryptionLayer) { }

        private string GetCred(string key)
        {
            var i = 1;
            var str = "";
            var credential = new Credential()
            {
                Target = $"{ServiceName}:{key}:{i}",
            };
            if (!credential.Exists()) return null;
            
            do
            {
                credential.Load();
                str += credential.Password;
                credential.Dispose();
                credential = new Credential()
                {
                    Target = $"{ServiceName}:{key}:{++i}",
                };
            } while (credential.Exists());
            
            return str;
        }

        private void SetCred(string key, string value)
        {
            const int splitPoint = 256;
            
            var i = 1;
            while (value.Length != 0)
            {
                var part = value;
                if (value.Length >= splitPoint)
                {
                    part = value[..splitPoint];
                    value = value[splitPoint..];
                }
                else
                    value = "";
                new Credential()
                {
                    Target = $"{ServiceName}:{key}:{i++}",
                    Password = part,
                    PersistanceType = PersistanceType.LocalComputer
                }.Save();
            }

            while (true)
            {
                var cred = new Credential()
                {
                    Target = $"{ServiceName}:{key}:{i++}",
                };
                if (cred.Exists())
                    cred.Delete();
                else
                    break;
            }
        }
        
        public override AuthTokens Get(string id)
        {
            var authToken = GetCred(id);
            if (authToken is null) return null;
            authToken = EncryptionLayer.Decrypt(authToken);
            
            var twoFactorAuthToken = GetCred($"{id}:2fa");
            if (twoFactorAuthToken is not null)
                twoFactorAuthToken = EncryptionLayer.Decrypt(twoFactorAuthToken);
            
            return new AuthTokens(authToken, twoFactorAuthToken);
        }

        public override void Set(string id, AuthTokens tokens)
        {
            SetCred(id, EncryptionLayer.Encrypt(tokens.Auth));
            
            if (tokens.TwoFactorAuth is not null)
                SetCred($"{id}:2fa", EncryptionLayer.Encrypt(tokens.TwoFactorAuth));
        }

        public override void Delete(string id)
        {
            for (var i = 0; i < 1000; i++)
            {
                var mainCred = new Credential
                {
                    Target = $"{ServiceName}:{id}:{i}",
                };
                var bothDontExist = !mainCred.Exists();
                if (mainCred.Exists())
                    mainCred.Delete();
                
                var twoFactorCred = new Credential
                {
                    Target = $"{ServiceName}:{id}:2fa:2",
                };
                bothDontExist = bothDontExist && !twoFactorCred.Exists();
                if (twoFactorCred.Exists())
                    twoFactorCred.Delete();
                
                if (bothDontExist)
                    break;
            }
        }
    }
}
#endif