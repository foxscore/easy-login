using UnityEditor;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin.KeyringManagers
{
    public class UnsecureCredentialsManager : IKeyringManager
    {
        private const string ServiceName = "Foxscore_EasyLogin";

        public AuthTokens Get(string id)
        {
            var target = $"{ServiceName}:{id}";
            return EditorPrefs.HasKey(target)
                ? AuthTokens.FromJson(EditorPrefs.GetString(target))
                : null;
        }
        
        public void Set(string id, AuthTokens tokens) => EditorPrefs.SetString($"{ServiceName}:{id}", tokens.ToJson());
        
        public void Delete(string id) => EditorPrefs.DeleteKey($"{ServiceName}:{id}");
    }
}