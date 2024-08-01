// ReSharper disable once CheckNamespace

namespace Foxscore.EasyLogin.KeyringManagers
{
    public class AuthCookies
    {
        public readonly string Auth;
        public readonly string TwoFactorAuthCookie;

        public AuthCookies(string auth, string twoFactorAuthCookie)
        {
            Auth = auth;
            TwoFactorAuthCookie = twoFactorAuthCookie;
        }
    };

    public interface IKeyringManager
    {
        public AuthCookies Get(string id);
        public void Set(string id, string authCookie, string twoFactorAuthCookie);
        public void Delete(string id);
    }
}