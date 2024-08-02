

using Newtonsoft.Json;
// ReSharper disable once CheckNamespace
using BestHTTP.Cookies;

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

    public interface IKeyringManager
    {
        public AuthTokens Get(string id);
        public void Set(string id, AuthTokens tokens);
        public void Delete(string id);
    }
}