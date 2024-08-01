using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin
{
    public class SpinnerProvider
    {
        private Texture[] _spinners;

        private double _lastUpdate;
        private int _index;

        private void Init()
        {
            _spinners =
                new[]
                {
                    EditorGUIUtility.IconContent("WaitSpin00").image,
                    EditorGUIUtility.IconContent("WaitSpin01").image,
                    EditorGUIUtility.IconContent("WaitSpin02").image,
                    EditorGUIUtility.IconContent("WaitSpin03").image,
                    EditorGUIUtility.IconContent("WaitSpin04").image,
                    EditorGUIUtility.IconContent("WaitSpin05").image,
                    EditorGUIUtility.IconContent("WaitSpin06").image,
                    EditorGUIUtility.IconContent("WaitSpin07").image,
                    EditorGUIUtility.IconContent("WaitSpin08").image,
                    EditorGUIUtility.IconContent("WaitSpin09").image,
                    EditorGUIUtility.IconContent("WaitSpin10").image,
                    EditorGUIUtility.IconContent("WaitSpin11").image,
                };
        }

        public Texture Update()
        {
            if (_spinners == null)
                Init();

            const double updateInterval = 0.05; // Update every 100ms

            if (EditorApplication.timeSinceStartup - _lastUpdate >= updateInterval)
            {
                _lastUpdate = EditorApplication.timeSinceStartup;
                _index = (_index + 1) % _spinners!.Length;
            }

            return _spinners![_index];
        }
    }
}