#if UNITY_EDITOR_WIN
using System;
using BestHTTP.Cookies;
using CredentialManagement;
using Newtonsoft.Json;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin.KeyringManagers
{
    public class WindowsCredentialKeyringManager : KeyringManager
    {
        private const string ServiceName = "Foxscore_EasyLogin";

        public WindowsCredentialKeyringManager(IEncryptionLayer encryptionLayer) : base(encryptionLayer) { }
        
        public override AuthTokens Get(string id)
        {
            using var c = new Credential();
            c.Target = $"{ServiceName}:{id}";
            if (!c.Exists()) return null;
            c.Load();

            string twoFactorAuthToken = null;
            using var c2 = new Credential();
            c2.Target = $"{ServiceName}:{id}:2fa:1";
            if (c2.Exists())
            {
                c2.Load();
                using var c3 = new Credential();
                c3.Target = $"{ServiceName}:{id}:2fa:2";
                c3.Load();
                
                twoFactorAuthToken = EncryptionLayer.Decrypt(c2.Password + c3.Password);
            }

            return new AuthTokens(
                EncryptionLayer.Decrypt(c.Password),
                twoFactorAuthToken
            );
        }

        public override void Set(string id, AuthTokens tokens) {
            new Credential()
            {
                Target = $"{ServiceName}:{id}",
                Password = EncryptionLayer.Encrypt(tokens.Auth),
                PersistanceType = PersistanceType.LocalComputer
            }.Save();

            const int splitPoint = 256;

            if (tokens.TwoFactorAuth != null)
            {
                var encrypted = EncryptionLayer.Encrypt(tokens.TwoFactorAuth);
                var part1 = encrypted[..splitPoint];
                var part2 = encrypted[splitPoint..];
                
                new Credential()
                {
                    Target = $"{ServiceName}:{id}:2fa:1",
                    Password = part1,
                    PersistanceType = PersistanceType.LocalComputer
                }.Save();
                new Credential()
                {
                    Target = $"{ServiceName}:{id}:2fa:2",
                    Password = part2,
                    PersistanceType = PersistanceType.LocalComputer
                }.Save();
            }
            else
            {
                var c = new Credential()
                {
                    Target = $"{ServiceName}:{id}:2fa:1"
                };
                if (c.Exists())
                {
                    c.Delete();
                    new Credential()
                    {
                        Target = $"{ServiceName}:{id}:2fa:2"
                    }.Delete();
                }
            }
        }

        public override void Delete(string id)
        {
            using var c = new Credential();
            c.Target = $"{ServiceName}:{id}";
            if (c.Exists())
                c.Delete();
        }
    }
}
#endif