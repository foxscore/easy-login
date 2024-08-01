using UnityEditor;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin.KeyringManagers
{
    public class UnsecureCredentialsManager : IKeyringManager
    {
        private const string ServiceName = "Foxscore_EasyLogin";

        public AuthCookies Get(string id)
        {
            var target = $"{ServiceName}:{id}";
            if (!EditorPrefs.HasKey(target)) return null;
            var parts = EditorPrefs.GetString(target).Split('\x1F');
            return new AuthCookies(parts[0], parts.Length == 2 ? parts[1] : null);
        }

        public void Set(string id, string authCookie, string twoFactorAuthCookie)
        {
            EditorPrefs.SetString(
                $"{ServiceName}:{id}",
                twoFactorAuthCookie is null
                    ? $"{authCookie}"
                    : $"{authCookie}\x1F{twoFactorAuthCookie}"
            );
        }

        public void Delete(string id)
        {
            EditorPrefs.DeleteKey($"{ServiceName}:{id}");
        }
    }
}