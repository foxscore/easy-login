using System;
using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin
{
    public class ScopedVerticalOnlyScrollView : IDisposable
    {
        public Vector2 ScrollPosition { get; private set; }
        
        public ScopedVerticalOnlyScrollView(Vector2 scrollPosition)
        {
            ScrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);
        }
        
        public void Dispose()
        {
            EditorGUILayout.EndScrollView();
        }
    }
}