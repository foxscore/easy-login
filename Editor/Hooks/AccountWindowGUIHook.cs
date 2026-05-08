using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Foxscore.EasyLogin.PopupWindows;
using Foxscore.EasyLogin.Services;
using HarmonyLib;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using VRC.Core;
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
            if (
                !Is.FirstRun() ||
                !PlatformUtils.IsPlatformSupported()
            )
                return;
            
            OnAuthenticationVerifiedActionFieldInfo = typeof(VRCSdkControlPanel)
                .GetField("onAuthenticationVerifiedAction", BindingFlags.NonPublic | BindingFlags.Static);

            var accountMethod = AccessTools.Method(typeof(VRCSdkControlPanel), "OnAccountGUI");
            var accountPrefix = AccessTools.Method(typeof(AccountWindowGUIHook), nameof(AccountPrefix));

            var settingsMethod = AccessTools.Method(typeof(VRCSdkControlPanel), "ShowSettings");
            var settingsPostfix = AccessTools.Method(typeof(AccountWindowGUIHook), nameof(SettingPostfix)); 
            
            var harmony = new Harmony("dev.foxscore.easy-login.accountWindowGUI");
            harmony.Patch(accountMethod, new HarmonyMethod(accountPrefix));
            harmony.Patch(settingsMethod, null, new HarmonyMethod(settingsPostfix));
        }

        private static GUIStyle _warningLabelStyle;
        private static GUIStyle _motdMessageStyle;
        private static string _vaultPassword = "";
        private static Vector2 _scrollPosition;
        private static GUIStyle _updateAvailableTitleStyle;

        private static void DrawMotd()
        {
            var motdMessages = MotdService.MotdMessages;
            if (motdMessages.Any(m => m.ShouldShow()))
            {
                _motdMessageStyle ??= new("label")
                {
                    richText = true,
                    wordWrap = true,
                };
                using (new GUILayout.VerticalScope("helpbox", GUILayout.MaxWidth(400)))
                {
                    EditorGUILayout.LabelField("Server Message", EditorStyles.boldLabel);
                    foreach (var message in motdMessages)
                    {
                        if (!message.ShouldShow())
                            continue;
                        
                        EditorGUILayout.BeginHorizontal(GUI.skin.box);
                        EditorGUILayout.LabelField(message.Message, _motdMessageStyle);
                        if (
                            message.AllowHiding &&
                            GUILayout.Button("Hide", GUILayout.Width(40))
                        )
                        {
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("For this session"), false, message.HideMessage);
                            menu.AddItem(new GUIContent("Permanently"), false, message.HideMessageForever);
                            menu.ShowAsContext();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.Space();   
            }
        }
        
        // ReSharper disable once InconsistentNaming
        private static bool AccountPrefix()
        {
            if (!Config.Enabled)
            {
                BestHTTPSetup.Setup();
                
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

            if (Config.IsReadOnly)
            {
                EditorGUILayout.HelpBox(
                    "Read-Only mode has been engaged.\nSee the settings page for more information.",
                    MessageType.Warning
                );
                EditorGUILayout.Space();
            }

            if (UpdateService.IsUpdateAvailable)
            {
                using (new GUILayout.VerticalScope("helpbox"))
                {
                    _updateAvailableTitleStyle ??= new GUIStyle("label")
                    {
                        fontSize = 16,
                    };
                    GUILayout.Label("Update available", _updateAvailableTitleStyle);
                    GUILayout.Label("See the Easy Login section in the settings tab for more information.");
                    if (GUILayout.Button("Go to settings"))
                        Utils.SelectControlPanelTab(Utils.VRCSdkPanelTab.Settings);
                }
                EditorGUILayout.Space();
            }

            if (AuthSession is not null)
            {
                if (Config.IsReadOnly)
                {
                    EditorGUILayout.HelpBox(
                        "Accounts cannot be added / modified due to Read-Only mode being enabled.",
                        MessageType.Error
                    );
                    if (GUILayout.Button("Go Back"))
                        AuthSession = null;
                }
                else
                {
                    AuthSession.Render();
                    VRCSdkControlPanel.window.Repaint();   
                }
            }
            // Vault not unlocked
            else if (Accounts.CurrentAccount == null && !Accounts.KeyringManager.EncryptionLayer.IsUnlocked())
            {
                DrawMotd();
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
                DrawMotd();
                using var scrollScope = new ScopedVerticalOnlyScrollView(_scrollPosition);
                _scrollPosition = scrollScope.ScrollPosition;
                
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
                            // TODO Show the user a loading screen
                            try
                            {
                                API.VerifyTokens(
                                    Accounts.KeyringManager.Get(account.Id),
                                    () => { Accounts.SetCurrentAccount(account); },
                                    () =>
                                    {
                                        EditorApplication.delayCall += () =>
                                        {
                                            if (Config.IsReadOnly)
                                            {
                                                EditorUtility.DisplayDialog(
                                                    "Easy Login",
                                                    "Session expired.\n\n" +
                                                    "Usually, you'd have to re-enter your password, but this is currently not possible due to Read-Only mode being enabled.\n\n" +
                                                    "See the settings page for more information.",
                                                    "Ok"
                                                );
                                            }
                                            else
                                            {
                                                // ToDo: Change to in-window popup instead of dialog
                                                if (EditorUtility.DisplayDialog(
                                                        "Easy Login",
                                                        "Sessions expired. Please login again.",
                                                        "Ok", "Not now"))
                                                    AuthSession = new AuthSession(account);
                                            }
                                        };
                                    },
                                    error => { Log.Error("Failed to verify credentials: " + error); }
                                );
                            }
                            catch (Exception e)
                            {
                                Log.Error($"There was an error while trying to sign you in as `{account.Username}`", e);
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

                    using (new EditorGUI.DisabledGroupScope(Config.IsReadOnly))
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
                    if (!Config.IsReadOnly)
                        EditorGUIUtility.AddCursorRect(buttonRect, MouseCursor.Link);
                }

                buttonRect = EditorGUILayout.GetControlRect(false, 64, GUILayout.Width(400));
                iconRect = new Rect(buttonRect.x + 11, buttonRect.y + 11, 42, 42);
                labelRect = new Rect(iconRect.xMax + 11, iconRect.y, 200, 42);

                
                using (new EditorGUI.DisabledGroupScope(Config.IsReadOnly))
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
                        textColor = (
                            EditorGUIUtility.isProSkin
                                ? new Color(0.6862745098f, 0.6862745098f, 0.6862745098f)
                                : new Color(0.008f, 0.008f, 0.008f)
                            ) * (Config.IsReadOnly ? 0.8f : 1f)
                    }
                });

                if (!Config.IsReadOnly)
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
                DrawMotd();
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

            
            using (new EditorGUILayout.VerticalScope(GUI.skin.textArea))
            {
                const string unknownLatestString = "...";
                
                var currentVersion = UpdateService.LastUpdateCheckResult?.InstalledVersion?.ToString()
                                     ?? Utils.GetPackageJson().VersionString;
                var availableVersionText = UpdateService.LastUpdateCheckResult?.LatestVersionAvailable?.ToString() ?? unknownLatestString;
                var isUpdateAvailable = UpdateService.LastUpdateCheckResult?.IsUpdateAvailable ?? false;

                if (UpdateService.IsChecking)
                {
                    availableVersionText = unknownLatestString;
                    VRCSdkControlPanel.window.Repaint();
                }
             
                // EditorGUILayout.LabelField("Updates");
                // using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField("Installed", currentVersion);
                    EditorGUILayout.LabelField("Available", availableVersionText);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var updateButtonRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, 21));
                        if (GUI.Button(updateButtonRect, "Check for Updates"))
                            _ = UpdateService.CheckForUpdates();

                        using (new EditorGUI.DisabledGroupScope(!isUpdateAvailable || availableVersionText == unknownLatestString))
                        {
                            updateButtonRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, 21));
                            if (GUI.Button(updateButtonRect, $"Install Update"))
                                UpdateService.InstallUpdate();
                        }
                    }
                }
            }
            EditorGUILayout.Space();
            
            // ReadOnly Protection
            if (Config.IsReadOnly)
            {
                var message = Config.ReadOnlyReason switch
                {
                    ReadOnlyReason.NewerConfig => "Your config file is of a newer version than what is currently supported.\n" + 
                                                  "Read-Only mode has been engaged to protect your data.\n" +
                                                  "Please update to the latest available version of Easy Login as soon as possible.",
                    
                    ReadOnlyReason.None or _ => "Read-Only mode has been engaged to protect your data.\n" +
                                                "The system has not reported a valid reason for doing so.\n" +
                                                "Please contact the developer if this issue persists.",
                };
                var type = Config.ReadOnlyReason switch
                {
                    ReadOnlyReason.NewerConfig => MessageType.Error,
                    ReadOnlyReason.None => MessageType.Warning,
                    _ => MessageType.Error,
                };
                EditorGUILayout.HelpBox(message, type);
                
                // * Don't forget to draw the links!
                EditorGUILayout.Space();
                DrawLinks();
                EditorGUILayout.Space();
                
                EditorGUILayout.EndVertical();
                return;
            }

            // Actual settings
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
            EditorGUILayout.Space();
            
            // Links
            DrawLinks();
            EditorGUILayout.Space();
            
            // End box
            EditorGUILayout.EndVertical();
        }

        private static GUIStyle _linkBorderStyle;
        private static void DrawLinks()
        {
            _linkBorderStyle = new GUIStyle("ScriptText")
            {
                padding = new RectOffset(10, 10, 8, 6),
            };
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                #region Functions
                void DrawButton(Texture2D icon, string tooltip, string url)
                {
                    const int size = 24;
                    var rect = EditorGUILayout.GetControlRect(false, size, GUILayout.Width(size));

                    // Calculate imageRect to respect the aspect ratio of the icon, centered within the button rect
                    float aspect = icon.width / (float)icon.height;
                    float newWidth, newHeight;
                    if (aspect > 1f) // Wider than tall
                    {
                        newWidth = size;
                        newHeight = size / aspect;
                    }
                    else // Taller than wide or square
                    {
                        newHeight = size;
                        newWidth = size * aspect;
                    }

                    var imageRect = new Rect(rect.x + (rect.width - newWidth) / 2f,
                        rect.y + (rect.height - newHeight) / 2f, newWidth, newHeight);

                    GUI.DrawTexture(imageRect, icon);
                    if (GUI.Button(rect, new GUIContent("", tooltip), GUI.skin.label))
                        Application.OpenURL(url);
                    EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                }

                void DrawSeparator(float leftSpacing, float rightSpacing)
                {
                    GUILayout.Space(leftSpacing);
                    var rect = EditorGUILayout.GetControlRect(false, 20f, GUILayout.Width(1), GUILayout.Height(24));
                    const float gradient = 86 / 255f;
                    Handles.color = new Color(gradient, gradient, gradient, 1f);
                    Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.x, rect.y + rect.height), 1);
                    GUILayout.Space(rightSpacing);
                }
                #endregion

                using (new EditorGUILayout.HorizontalScope(_linkBorderStyle))
                {
                    DrawButton(Icons.GitHub, "GitHub", "https://short.easy-vrc.com/easy-login-github");
                    DrawSeparator(7, 6);
                    DrawButton(Icons.Discord, "Discord", "https://short.easy-vrc.com/discord");
                }

                GUILayout.FlexibleSpace();
            }
        }

        private static class MaskCache
        {
            private static Texture2D _mask;

            private static StyleOption _style = StyleOption.Square;
            private static float _gradient = -1;
            private static float? _cornerRadius;

            public static Texture2D GetMask(StyleOption style, float gradient) => GetMask(style, gradient, null);
            public static Texture2D GetMask(StyleOption style, float gradient, float? cornerRadius)
            {
                // ReSharper disable once PossibleInvalidOperationException
                if (
                    _mask is null ||
                    _style != style ||
                    !Mathf.Approximately(_gradient, gradient) ||
                    _cornerRadius.HasValue != cornerRadius.HasValue ||
                    (
                        _cornerRadius.HasValue && !Mathf.Approximately(_cornerRadius.Value, cornerRadius.Value)
                    )
                )
                {
                    _mask = null;
                    _style = style;
                    _gradient = gradient;
                    _cornerRadius = cornerRadius;
                }
                return _mask;
            }

            public static bool TryGetMask(StyleOption style, float gradient, float? cornerRadius, [MaybeNullWhen(false)] out Texture2D mask)
            {
                mask = GetMask(style, gradient, cornerRadius);
                return mask is not null;
            }

            public static void SetMask(Texture2D mask)
            {
                _mask = mask;
            }
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
            if (!MaskCache.TryGetMask(StyleOption.Circular, gradient, null, out var mask))
            {
                var radius = Mathf.Floor(rect.width / 2f) - 0.5f;
                mask = new Texture2D((int)rect.width, (int)rect.height);
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

                MaskCache.SetMask(mask);
            }

            GUI.DrawTexture(rect, mask);
        }

        private static void DrawRoundedCornerMask(Rect rect, float gradient, float cornerRadius)
        {
            if (!MaskCache.TryGetMask(StyleOption.Rounded, gradient, cornerRadius, out var mask))
            {
                mask = new Texture2D((int)rect.width, (int)rect.height);
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
                        if (cornerDist >= 1)
                            pixels[y * mask.width + x] = new Color(gradient, gradient, gradient, alpha);
                    }
                }

                mask.SetPixels(pixels);
                mask.Apply();
                
                MaskCache.SetMask(mask);
            }

            GUI.DrawTexture(rect, mask);
        }
    }
}