using UnityEditor;

namespace Foxscore.EasyLogin
{
    public static class Preferences
    {
        public static bool UserOriginalLoginSystem
        {
            get => EditorPrefs.GetBool("Foxscore_EasyLogin::UseOriginalLoginSystem");
            set => EditorPrefs.SetBool("Foxscore_EasyLogin::UseOriginalLoginSystem", value);
        }
    }
}