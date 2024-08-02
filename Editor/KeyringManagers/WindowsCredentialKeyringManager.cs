#if UNITY_EDITOR_WIN
using System;
using BestHTTP.Cookies;
using CredentialManagement;
using Newtonsoft.Json;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin.KeyringManagers
{
    public class WindowsCredentialKeyringManager : IKeyringManager
    {
        private const string ServiceName = "Foxscore_EasyLogin";

        AuthTokens IKeyringManager.Get(string id)
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
                
                twoFactorAuthToken = c2.Password + c3.Password;
            }

            return new AuthTokens(c.Password, twoFactorAuthToken);
        }

        public void Set(string id, AuthTokens tokens) {
            new Credential()
            {
                Target = $"{ServiceName}:{id}",
                Password = tokens.Auth,
                PersistanceType = PersistanceType.LocalComputer
            }.Save();

            const int splitPoint = 256;

            if (tokens.TwoFactorAuth != null)
            {
                var part1 = tokens.TwoFactorAuth[..splitPoint];
                var part2 = tokens.TwoFactorAuth[splitPoint..];
                
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

        public void Delete(string id)
        {
            using var c = new Credential();
            c.Target = $"{ServiceName}:{id}";
            if (c.Exists())
                c.Delete();
        }
    }
}
#endif