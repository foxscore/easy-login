#if UNITY_EDITOR_WIN
using CredentialManagement;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin.KeyringManagers
{
    public class WindowsCredentialKeyringManager : IKeyringManager
    {
        private const string ServiceName = "Foxscore_EasyLogin";

        AuthCookies IKeyringManager.Get(string id)
        {
            using var c = new Credential();
            c.Target = $"{ServiceName}:{id}";
            if (!c.Exists()) return null;
            c.Load();
            var parts = c.Password.Split('\x1F');
            return new AuthCookies(parts[0], parts.Length == 2 ? parts[1] : null);
        }

        public void Set(string internalUserId, string authCookie, string twoFactorAuthCookie) => new Credential()
        {
            Target = $"{ServiceName}:{internalUserId}",
            Password = twoFactorAuthCookie is null
                ? $"{authCookie}"
                : $"{authCookie}\x1F{twoFactorAuthCookie}",
            PersistanceType = PersistanceType.LocalComputer
        }.Save();

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