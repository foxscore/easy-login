using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin
{
    public enum StyleOption
    {
        Square = 0,
        Rounded = 1,
        Circular = 2,
    }
    
    public static class Preferences
    {
        // TODO Move to Config class
        
        public static bool UseOriginalLoginSystem
        {
            get => EditorPrefs.GetBool("Foxscore_EasyLogin::UseOriginalLoginSystem", false);
            set => EditorPrefs.SetBool("Foxscore_EasyLogin::UseOriginalLoginSystem", value);
        }

        public static StyleOption ProfilePictureStyle
        {
            get => (StyleOption) EditorPrefs.GetInt("Foxscore_EasyLogin::ProfilePictureStyle", (int)StyleOption.Rounded);
            set => EditorPrefs.SetInt("Foxscore_EasyLogin::ProfilePictureStyle", (int)value);
        }

        public static float ProfilePictureRadius
        {
            get => EditorPrefs.GetFloat("Foxscore_EasyLogin::ProfilePictureRadius", 0.25f);
            set => EditorPrefs.SetFloat("Foxscore_EasyLogin::ProfilePictureRadius", Mathf.Clamp(value, 0, 0.5f));
        }
    }
}