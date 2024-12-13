using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using Foxscore.EasyLogin.PopupWindows;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Compilation;
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

        private static readonly FieldInfo OnAuthenticationVerifiedActionFieldInfo;

        static AccountWindowGUIHook()
        {
            if (!PlatformUtils.IsPlatformSupported())
                return;
            
            OnAuthenticationVerifiedActionFieldInfo = typeof(VRCSdkControlPanel)
                .GetField("onAuthenticationVerifiedAction", BindingFlags.NonPublic | BindingFlags.Static);

            var accountMethod =
                typeof(VRCSdkControlPanel).GetMethod("OnAccountGUI", BindingFlags.NonPublic | BindingFlags.Static);
            var accountPrefix =
                typeof(AccountWindowGUIHook).GetMethod(nameof(AccountPrefix),
                    BindingFlags.NonPublic | BindingFlags.Static);

            var settingsMethod =
                typeof(VRCSdkControlPanel).GetMethod("ShowSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            var settingsPostfix =
                typeof(AccountWindowGUIHook).GetMethod(nameof(SettingPostfix),
                    BindingFlags.NonPublic | BindingFlags.Static);

            var harmony = new Harmony("dev.foxscore.easy-login.accountWindowGUI");
            harmony.Patch(accountMethod, new HarmonyMethod(accountPrefix));
            harmony.Patch(settingsMethod, null, new HarmonyMethod(settingsPostfix));
        }

        private static GUIStyle _warningLabelStyle;
        private static string _vaultPassword = "";
        private static bool _bestHttpIsSetup = false;

        // ReSharper disable once InconsistentNaming
        private static bool AccountPrefix()
        {
            if (!Config.Enabled)
            {
                if (!_bestHttpIsSetup)
                {
                    BestHTTP.HTTPManager.Setup();
                    _bestHttpIsSetup = true;
                }
                
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
            // Vault not unlocked
            else if (Accounts.CurrentAccount == null && !Accounts.KeyringManager.EncryptionLayer.IsUnlocked())
            {
                _vaultPassword = EditorGUILayout.PasswordField("Password", _vaultPassword);
                if (GUILayout.Button("Unlock Vault"))
                {
                    if (Accounts.KeyringManager.EncryptionLayer.Unlock(_vaultPassword))
                        Accounts.AttemptAutoLogin();
                    else
                        EditorUtility.DisplayDialog("Easy Login", "Failed to unlock vault. Please try again. If the problem persists, try different passwords or contact us.", "OK");
                    GUI.FocusControl(null);
                    _vaultPassword = string.Empty;
                }
            }
            // Vault unlocked, no account selected
            else if (Accounts.CurrentAccount == null)
            {
                Rect buttonRect;
                Rect iconRect;
                Rect labelRect;
                Texture icon;

                var accounts = Config.GetAccounts();
                foreach (var account in accounts.OrderBy(a => a.DisplayName))
                {
                    buttonRect = EditorGUILayout.GetControlRect(false, 64, GUILayout.Width(400 - 64 - 2));
                    iconRect = new Rect(buttonRect.x + 11, buttonRect.y + 11, 42, 42);
                    labelRect = new Rect(iconRect.xMax + 11, iconRect.y, 200, 42);

                    if (GUI.Button(buttonRect, "", "helpbox"))
                    {
                        new Task(() =>
                        {
                            try
                            {
                                API.VerifyTokens(Accounts.KeyringManager.Get(account.Id),
                                    () => { Accounts.SetCurrentAccount(account); }, () =>
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
                                    }, error => { Log.Error("Failed to verify credentials: " + error); });
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                            }
                        }).Start();
                    }

                    if ((icon = ProfilePictureCache.GetFor(account)) != null)
                    {
                        GUI.DrawTexture(iconRect, icon);
                        DrawMask(iconRect, EditorGUIUtility.isProSkin ? 0.2509803922f : 0.8117647059f);
                    }
                    else if ((icon = Icons.Profile) != null)
                        GUI.DrawTexture(iconRect, icon);

                    GUI.Label(labelRect,
                        new GUIContent(account.DisplayName, $"<b>{account.Username}</b>\n{account.Id}"), new GUIStyle
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

                if (Accounts.KeyringManager.EncryptionLayer is PasswordEncryption)
                {
                    buttonRect = EditorGUILayout.GetControlRect(false, 64, GUILayout.Width(400));
                    iconRect = new Rect(buttonRect.x + 11, buttonRect.y + 11, 42, 42);
                    labelRect = new Rect(iconRect.xMax + 11, iconRect.y, 200, 42);

                    if (GUI.Button(buttonRect, "", "helpbox"))
                    {
                        IEncryptionLayer.ClearSessionPassword();
                        CompilationPipeline.RequestScriptCompilation();
                    }

                    icon = Icons.Lock;
                    GUI.DrawTexture(iconRect, icon);

                    GUI.Label(labelRect, "Lock vault", new GUIStyle()
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
            }
            // Account selected
            else
            {
                EditorGUILayout.BeginHorizontal();
                {
                    const int height = 28;
                    var rect = EditorGUILayout.GetControlRect(false, height);
                    rect.x += 1;
                    rect.width -= 2;

                    var icon = ProfilePictureCache.GetFor(Accounts.CurrentAccount);
                    if (icon != null)
                    {
                        var iconRect = new Rect(rect.x, rect.y, height, height);
                        rect.x += height + 6;
                        rect.width -= height + 6;
                        GUI.DrawTexture(iconRect, icon);
                        DrawMask(iconRect, EditorGUIUtility.isProSkin ? 0.22f : 0.784f);
                    }

                    GUI.Label(rect, Accounts.CurrentAccount.DisplayName, "AM MixerHeader");
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

        private static StyleOption Preferences_IconStyle = Config.ProfilePictureStyle;
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

            var value = !Config.Enabled;
            var newValue = EditorGUILayout.ToggleLeft("Use original login system", value);
            if (value != newValue)
            {
                Config.Enabled = !newValue;
                if (newValue == false)
                {
                    ApiCredentials.Clear();
                    Accounts.ClearCurrentAccount();
                }
            }

            EditorGUILayout.Space();

            // * Creating this element manually instead of using EditorGUILayout.EnumField works more consistently on GNOME systems
            var styleValue = Config.ProfilePictureStyle;
            var rect = EditorGUILayout.GetControlRect(false, 18);
            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            var buttonRect = new Rect(labelRect.xMax, rect.y, rect.width - labelRect.width, rect.height);
            EditorGUI.LabelField(labelRect, "Profile picture style");
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(ObjectNames.NicifyVariableName(styleValue.ToString())), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                foreach (var @enum in Enum.GetNames(typeof(StyleOption)))
                    menu.AddItem(
                        new GUIContent(ObjectNames.NicifyVariableName(@enum)),
                        Config.ProfilePictureStyle.ToString() == @enum,
                        () => Config.ProfilePictureStyle = (StyleOption)Enum.Parse(typeof(StyleOption), @enum)
                    );
                menu.ShowAsContext();
            };

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledScope(styleValue != StyleOption.Rounded))
                {
                    var radiusValue = Config.ProfilePictureRadius;
                    var newRadiusValue = EditorGUILayout.Slider("Rounded radius", radiusValue, 0, 0.5f);
                    if (!Mathf.Approximately(radiusValue, newRadiusValue))
                        Config.ProfilePictureRadius = newRadiusValue;
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Master-Password Protection");
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledScope(Config.EncryptionLayerType is not EncryptionLayerType.Password))
                {
                    var newKeepVaultOpen = EditorGUILayout.ToggleLeft(
                        new GUIContent(
                            "Keep vault unlocked *",
                            "You will still have to unlock it whenever you open Unity, just not while this Unity instance is open.\n\nIf this options was previously disabled, then it will only take effect upon the next reload."
                        ),
                        Config.KeepVaultUnlockedForSession
                    );
                    if (newKeepVaultOpen != Config.KeepVaultUnlockedForSession)
                    {
                        Config.KeepVaultUnlockedForSession = newKeepVaultOpen;
                        if (!newKeepVaultOpen)
                            IEncryptionLayer.ClearSessionPassword();
                    }
                }
                
                switch (Config.EncryptionLayerType)
                {
                    case EncryptionLayerType.Basic:
                        buttonRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, 21));
                        if (GUI.Button(buttonRect, "Enable Master-Password Protection"))
                            PopupWindow.Show(buttonRect, new EnableMasterPasswordEncryptionPopup(buttonRect.width));
                        break;
                    case EncryptionLayerType.Password:
                        buttonRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, 21));
                        if (GUI.Button(buttonRect, "Disable Master-Password Protection"))
                            PopupWindow.Show(buttonRect, new EnableBasicEncryptionPopup(buttonRect.width));
                        break;
                    default:
                        EditorGUILayout.HelpBox(
                            $"UNKNOWN DATA ENCRYPTION TYPE ({Config.EncryptionLayerType})\nManual intervention required! Contact the developer if necessary.",
                            MessageType.Warning, true);
                        break;
                }
            }

            EditorGUILayout.EndVertical();
        }
        
        private static void DrawMask(Rect rect, float gradient)
        {
            switch (Config.ProfilePictureStyle)
            {
                case StyleOption.Square:
                    return;
                
                case StyleOption.Rounded:
                    DrawRoundedCornerMask(rect, gradient, Config.ProfilePictureRadius * rect.width);
                    break;
                
                case StyleOption.Circular:
                    DrawCircularMask(rect, gradient);
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void DrawCircularMask(Rect rect, float gradient)
        {
            var radius = Mathf.Floor(rect.width / 2f) - 0.5f;
            var mask = new Texture2D((int)rect.width, (int)rect.height);
            var pixels = new Color[mask.width * mask.height];
            for (var y = 0; y < mask.height; y++)
            {
                for (var x = 0; x < mask.width; x++)
                {
                    float dist = Mathf.Sqrt(Mathf.Pow(x - radius, 2) + Mathf.Pow(y - radius, 2));
                    float alpha = dist > radius ? 1f : 1 - Mathf.Clamp01(radius - dist);
                    pixels[y * mask.width + x] = new Color(gradient, gradient, gradient, alpha);
                }
            }

            mask.SetPixels(pixels);
            mask.Apply();

            GUI.DrawTexture(rect, mask);
        }

        private static void DrawRoundedCornerMask(Rect rect, float gradient, float cornerRadius)
        {
            var mask = new Texture2D((int)rect.width, (int)rect.height);
            var pixels = new Color[mask.width * mask.height];

            for (var y = 0; y < mask.height; y++)
            {
                for (var x = 0; x < mask.width; x++)
                {
                    // Calculate distance from the nearest corner
                    float cornerDist = 0;

                    // Adjust corner distance based on corner position to ensure correct rounding direction
                    if (x <= cornerRadius && y <= cornerRadius)
                    {
                        cornerDist = Mathf.Sqrt(Mathf.Pow(x + 1 - cornerRadius, 2) + Mathf.Pow(y + 1 - cornerRadius, 2));
                    }
                    else if (x >= rect.width - cornerRadius && y <= cornerRadius)
                    {
                        cornerDist = Mathf.Sqrt(Mathf.Pow(x - (rect.width - cornerRadius), 2) + Mathf.Pow(y + 1 - cornerRadius, 2));
                    }
                    else if (x <= cornerRadius && y >= rect.height - cornerRadius)
                    {
                        cornerDist = Mathf.Sqrt(Mathf.Pow(x + 1 - cornerRadius, 2) + Mathf.Pow(y - (rect.height - cornerRadius), 2));
                    }
                    else if (x >= rect.width - cornerRadius && y >= rect.height - cornerRadius)
                    {
                        cornerDist = Mathf.Sqrt(Mathf.Pow(x - (rect.width - cornerRadius), 2) + Mathf.Pow(y - (rect.height - cornerRadius), 2));
                    }

                    // Determine alpha based on distance from the nearest corner
                    var alpha = cornerDist >= cornerRadius ? 1f : 1 - Mathf.Clamp01(cornerRadius - cornerDist);

                    // Set pixel color based on alpha
                    pixels[y * mask.width + x] = new Color(gradient, gradient, gradient, alpha);
                }
            }

            mask.SetPixels(pixels);
            mask.Apply();

            GUI.DrawTexture(rect, mask);
        }
    }
}