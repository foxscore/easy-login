using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin
{
    public static class Icons
    {
        private static readonly Dictionary<string, Texture2D> _icons = new();

        private static Texture2D GetIcon(string name)
        {
            if (_icons.TryGetValue(name, out var icon))
                return icon;
            
            icon = Resources.Load<Texture2D>($"Fox_score/EasyLogin/{(EditorGUIUtility.isProSkin ? "d_" : "")}{name}");
            _icons.Add(name, icon);
            return icon;
        }

        public static Texture2D Profile => GetIcon("user");
        public static Texture2D Login => GetIcon("plus");
        public static Texture2D Logout => GetIcon("xmark");
    }
}