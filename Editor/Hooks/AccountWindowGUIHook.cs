using System;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase.Editor;
using Color = UnityEngine.Color;
using FontStyle = UnityEngine.FontStyle;
using Task = System.Threading.Tasks.Task;

namespace Foxscore.EasyLogin.Hooks
{
    [InitializeOnLoad]
    public static class AccountWindowGUIHook
    {
        internal static AuthSession AuthSession;
        internal static Action Popup;

        private static readonly FieldInfo OnAuthenticationVerifiedActionFieldInfo;

        static AccountWindowGUIHook()
        {
            OnAuthenticationVerifiedActionFieldInfo = typeof(VRCSdkControlPanel)
                .GetField("onAuthenticationVerifiedAction", BindingFlags.NonPublic | BindingFlags.Static);

            var accountMethod =
                typeof(VRCSdkControlPanel).GetMethod("OnAccountGUI", BindingFlags.NonPublic | BindingFlags.Static);
            var accountPrefix =
                typeof(AccountWindowGUIHook).GetMethod(nameof(AccountPrefix), BindingFlags.NonPublic | BindingFlags.Static);

            var settingsMethod =
                typeof(VRCSdkControlPanel).GetMethod("ShowSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            var settingsPostfix =
                typeof(AccountWindowGUIHook).GetMethod(nameof(SettingPostfix), BindingFlags.NonPublic | BindingFlags.Static);

            var harmony = new Harmony("dev.foxscore.easy-login.accountWindowGUI");
            harmony.Patch(accountMethod, new HarmonyMethod(accountPrefix));
            harmony.Patch(settingsMethod, null, new HarmonyMethod(settingsPostfix));
        }

        private static GUIStyle _warningLabelStyle;
        
        // ReSharper disable once InconsistentNaming
        private static bool AccountPrefix()
        {
            if (Preferences.UserOriginalLoginSystem)
            {
                const int padding = 11;
                const int height = 42;
                
                var rect = EditorGUILayout.GetControlRect(false, height + padding + padding);
                rect.x -= 4;
                rect.width += 6;
                rect.y -= 9;
                GUI.DrawTexture(rect, EditorGUIUtility.IconContent("gameviewbackground@2x").image);

                var iconRect = new Rect(rect.x + padding, rect.y + padding, height, height);
                iconRect.y += 1;
                GUI.DrawTexture(iconRect, EditorGUIUtility.IconContent("d_console.warnicon@2x").image);

                var labelRect = new Rect(iconRect.xMax + padding, iconRect.y, rect.width - (padding * 3) - height,
                    height);
                labelRect.y -= 1;
                _warningLabelStyle ??= new GUIStyle("label")
                {
                    normal =
                    {
                        textColor = Color.white,
                    },
                    fontSize = 16,
                    wordWrap = true,
                };
                GUI.Label(
                    labelRect,
                    "Easy Login is not enabled. You can re-enable it in the settings tab.",
                    _warningLabelStyle
                );
                
                return true;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            
            _ = EditorGUILayout.GetControlRect(false, 12);

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

            if (AuthSession is not null)
            {
                AuthSession.Render();
                VRCSdkControlPanel.window.Repaint();
            }
            else if (Accounts.CurrentAccount == null)
            {
                Rect buttonRect;
                Rect iconRect;
                Rect labelRect;
                Texture icon;

                var accounts = Config.GetAccounts();
                foreach (var account in accounts)
                {
                    buttonRect = EditorGUILayout.GetControlRect(false, 64, GUILayout.Width(400 - 64 - 2));
                    iconRect = new Rect(buttonRect.x + 11, buttonRect.y + 11, 42, 42);
                    labelRect = new Rect(iconRect.xMax + 11, iconRect.y, 200, 42);

                    if (GUI.Button(buttonRect, "", "helpbox"))
                    {
                        new Task(() => API.VerifyTokens(Accounts.KeyringManager.Get(account.Id), () =>
                        {
                            Accounts.SetCurrentAccount(account);
                        }, () =>
                        {
                            EditorApplication.delayCall += () =>
                            {
                                // ToDo: Change to in-window popup instead of dialog
                                if (EditorUtility.DisplayDialog(
                                        "Easy Login",
                                        "Sessions expired. Please login again.",
                                        "Ok", "Not now"))
                                    AuthSession = new AuthSession(account);
                            };
                        }, error =>
                        {
                            Debug.LogError("Failed to verify credentials: " + error);
                        })).Start();
                    }

                    // TODO: Load profile picture
                    icon = account.ProfilePicture;
                    icon ??= Icons.Profile;
                    if (icon != null)
                        GUI.DrawTexture(iconRect, icon);

                    GUI.Label(labelRect, new GUIContent(account.Username, account.Id), new GUIStyle()
                    {
                        fontSize = 24,
                        alignment = TextAnchor.MiddleLeft,
                        normal =
                        {
                            textColor = EditorGUIUtility.isProSkin
                                ? new Color(0.6862745098f, 0.6862745098f, 0.6862745098f)
                                : new Color(0.008f, 0.008f, 0.008f),
                        }
                    });

                    EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);

                    buttonRect = new Rect(buttonRect.xMax + 2, buttonRect.y, 64, 64);
                    iconRect = new Rect(buttonRect.x + 11, buttonRect.y + 11, 42, 42);

                    if (GUI.Button(buttonRect, "", "helpbox"))
                        EditorApplication.delayCall += () =>
                        {
                            if (EditorUtility.DisplayDialog(
                                    "Easy Login",
                                    $"Are you sure you want to remove the [{account.Username}] account?",
                                    "Yes, log me out", "Cancel"))
                            {
                                Config.RemoveAccount(account.Id);
                                var credentials = Accounts.KeyringManager.Get(account.Id);
                                API.InvalidateSession(credentials);
                                Accounts.KeyringManager.Delete(account.Id);
                            }
                        };

                    icon = Icons.Logout;
                    GUI.DrawTexture(iconRect, icon);

                    GUI.Label(buttonRect, new GUIContent("", "Remove account"));
                    EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
                }

                buttonRect = EditorGUILayout.GetControlRect(false, 64, GUILayout.Width(400));
                iconRect = new Rect(buttonRect.x + 11, buttonRect.y + 11, 42, 42);
                labelRect = new Rect(iconRect.xMax + 11, iconRect.y, 200, 42);

                if (GUI.Button(buttonRect, "", "helpbox"))
                    AuthSession = new AuthSession();

                icon = Icons.Login;
                GUI.DrawTexture(iconRect, icon);

                GUI.Label(labelRect, "Add account", new GUIStyle()
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleLeft,
                    normal =
                    {
                        textColor = EditorGUIUtility.isProSkin
                            ? new Color(0.6862745098f, 0.6862745098f, 0.6862745098f)
                            : new Color(0.008f, 0.008f, 0.008f)
                    }
                });

                EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                {
                    const int height = 28;
                    var rect = EditorGUILayout.GetControlRect(false, height);
                    rect.x += 1;
                    rect.width -= 2;
                    
                    if (Accounts.CurrentAccount.ProfilePicture != null)
                    {
                        var iconRect = new Rect(rect.x, rect.y, height, height);
                        var radius = Mathf.Floor(iconRect.width / 2f) - 0.5f;
                        rect.x += height + 6;
                        rect.width -= height + 6;

                        var mask = new Texture2D((int)iconRect.width, (int)iconRect.height);
                        var pixels = new Color[mask.width * mask.height];
                        var bg = EditorGUIUtility.isProSkin ? 0.22f : 0.784f;
                        for (var y = 0; y < mask.height; y++)
                        {
                            for (var x = 0; x < mask.width; x++)
                            {
                                float dist = Mathf.Sqrt(Mathf.Pow(x - radius, 2) + Mathf.Pow(y - radius, 2));
                                float alpha = dist > radius ? 1f : Mathf.Clamp01((dist - radius) / 1f); // Adjust 1f for anti-aliasing strength
                                pixels[y * mask.width + x] = new Color(bg, bg, bg, alpha);
                            }
                        }
                        mask.SetPixels(pixels);
                        mask.Apply();
                        
                        GUI.DrawTexture(iconRect, Accounts.CurrentAccount.ProfilePicture);
                        GUI.DrawTexture(iconRect, mask);
                    }
                    
                    GUI.Label(rect, Accounts.CurrentAccount.Username, "AM MixerHeader");
                }
                EditorGUILayout.EndHorizontal();
                
                var canPublishAvatarsString = APIUser.CurrentUser == null
                    ? "Loading..."
                    : APIUser.CurrentUser.canPublishAvatars
                        ? "Yes"
                        : "No";
                var canPublishWorldsString = APIUser.CurrentUser == null
                    ? "Loading..."
                    : APIUser.CurrentUser.canPublishWorlds
                        ? "Yes"
                        : "No";

                EditorGUILayout.LabelField("Can publish Avatars", canPublishAvatarsString);
                EditorGUILayout.LabelField("Can publish Worlds", canPublishWorldsString);

                EditorGUILayout.Space();
                var buttonRect = EditorGUILayout.GetControlRect(true, 21, "button");
                if (GUI.Button(buttonRect, "Switch Account"))
                {
                    ApiCredentials.Clear();
                    Accounts.ClearCurrentAccount();
                }
                EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
            }

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            return false;
        }

        private static void SettingPostfix()
        {
            EditorGUILayout.Separator();
            EditorGUILayout.BeginVertical(VRCSdkControlPanel.boxGuiStyle);

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Easy Login", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Made with \u2665 by Fox_score");
            }
            EditorGUILayout.EndHorizontal();
            
            var value = Preferences.UserOriginalLoginSystem;
            var newValue = EditorGUILayout.ToggleLeft("Use original login system", value);
            if (value != newValue) Preferences.UserOriginalLoginSystem = newValue;
            
            EditorGUILayout.EndVertical();
        }
    }
}