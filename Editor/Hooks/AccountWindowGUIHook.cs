using System;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace Foxscore.EasyLogin.Hooks
{
    [InitializeOnLoad]
    public static class AccountWindowGUIHook
    {
        private static readonly FieldInfo OnAuthenticationVerifiedActionFieldInfo;

        static AccountWindowGUIHook()
        {
            OnAuthenticationVerifiedActionFieldInfo = typeof(VRCSdkControlPanel)
                .GetField("onAuthenticationVerifiedAction", BindingFlags.NonPublic | BindingFlags.Static);

            var method =
                typeof(VRCSdkControlPanel).GetMethod("AccountWindowGUI", BindingFlags.NonPublic | BindingFlags.Static);
            var prefix =
                typeof(AccountWindowGUIHook).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static);

            var harmony = new Harmony("dev.foxscore.easy-login.accountWindowGUI");
            harmony.Patch(method, new HarmonyMethod(prefix));
        }

        private static VRCSdkControlPanel _instance;

        internal static Action OnAuthenticationVerifiedAction =>
            OnAuthenticationVerifiedActionFieldInfo.GetValue(_instance) as Action;

        // ReSharper disable once InconsistentNaming
        private static bool Prefix(VRCSdkControlPanel __instance)
        {
            _instance = __instance;

            EditorGUILayout.BeginVertical();
            _ = EditorGUILayout.GetControlRect(false, 20);

            #region Title

            _ = EditorGUILayout.GetControlRect(false, 20);
            var titleRect = EditorGUILayout.GetControlRect(false, 21, GUILayout.Width(400));
            _ = EditorGUILayout.GetControlRect(false, 20);
            var titleStyle = new GUIStyle()
            {
                fontSize = 19,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState()
                {
                    textColor = Color.gray,
                }
            };
            Handles.color = Color.gray;
            var center = titleRect.y + (titleRect.height * 0.5f);
            Handles.DrawLine(new Vector3(titleRect.x, center), new Vector3(titleRect.x + 142, center));
            Handles.DrawLine(new Vector3(titleRect.xMax - 142, center), new Vector3(titleRect.xMax, center));
            GUI.Label(titleRect, "Easy Login", titleStyle);

            #endregion

            if (Accounts.CurrentAccount == null)
            {
                var accounts = Config.GetAccounts();
                foreach (var account in accounts)
                {
                    // ToDo: Account display
                    EditorGUILayout.LabelField(account.Username);
                }

                var addButtonRect = EditorGUILayout.GetControlRect(false, 64, GUILayout.Width(400));
                var addButtonTextureRect = new Rect(addButtonRect.x + 11, addButtonRect.y + 11, 42, 42);
                var addButtonLabelRect = new Rect(addButtonTextureRect.xMax + 11, addButtonTextureRect.y, 200, 42);

                if (GUI.Button(addButtonRect, "", "helpbox"))
                    EditorApplication.delayCall += () => AuthWindow.ShowAuthWindow();

                var addButtonTexture = EditorGUIUtility.IconContent("CreateAddNew@2x").image;
                GUI.DrawTexture(addButtonTextureRect, addButtonTexture);

                GUI.Label(addButtonLabelRect, "Add account", new GUIStyle()
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleLeft,
                    normal =
                    {
                        textColor = new Color(0.6862745098f, 0.6862745098f, 0.6862745098f),
                    }
                });

                EditorGUIUtility.AddCursorRect(addButtonRect, MouseCursor.Link);
            }
            else
            {
                // ToDo: Show account info
                // ToDo: Show logout button
            }

            EditorGUILayout.EndVertical();
            return false;
        }
    }
}