

using Newtonsoft.Json;
// ReSharper disable once CheckNamespace

namespace Foxscore.EasyLogin.KeyringManagers
{
    public class AuthTokens
    {
        public readonly string Auth;
        public readonly string TwoFactorAuth;

        public AuthTokens(string auth, string twoFactorAuth)
        {
            Auth = auth;
            TwoFactorAuth = twoFactorAuth;
        }

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static AuthTokens FromJson(string json) => JsonConvert.DeserializeObject<AuthTokens>(json);
    };

    // ReSharper disable once UnusedTypeParameter
    public abstract class KeyringManager
    {
        public readonly IEncryptionLayer EncryptionLayer;

        public KeyringManager(IEncryptionLayer encryptionLayer)
        {
            EncryptionLayer = encryptionLayer;
        }
        
        public abstract AuthTokens Get(string id);
        public abstract void Set(string id, AuthTokens tokens);
        public abstract void Delete(string id);
    }
}