using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using BestHTTP.Cookies;
using Foxscore.EasyLogin.Hooks;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using VRC.Core;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin
{
    public class AuthWindow : EditorWindow
    {
        internal static void ShowAuthWindow(string username = null)
        {
            var window = CreateWindow<AuthWindow>();
            if (username != null)
            {
                window._isUsernameReadonly = true;
                window._username = username;
            }

            window.titleContent = new GUIContent("VRC Account");
            window.ShowUtility();
        }

        private readonly SpinnerProvider _spinner = new();

        private bool _isUsernameReadonly = false;
        private string _username;
        private string _password;
        private string _2faCode;
        private bool _wereCredentialsOr2AuthInvalid;
        private static State _state = State.EnterCredentials;
        private TwoFactorType _2FaType = TwoFactorType.None;

        private static string _errorTitle;
        private static string _errorMessage;

        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _nextStateButtonStyle;

        private enum State
        {
            EnterCredentials,
            VerifyingCredentials,
            Enter2Auth,
            Verifying2Auth,
            Error,
        }

        private void OnEnable()
        {
            minSize = maxSize = new Vector2(400, 400);
            _state = State.EnterCredentials;

            _titleStyle = new GUIStyle("label")
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle = new GUIStyle("label")
            {
                fontSize = 16,
            };
            _nextStateButtonStyle = new GUIStyle("button")
            {
                fontSize = 16,
            };

            ApiHook.Unhook();
            APIUser.Logout();
        }

        private void OnDisable()
        {
            ApiHook.Hook();
        }

        private void OnGUI()
        {
            Debug.Log(_state);
            switch (_state)
            {
                case State.EnterCredentials:
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        _isUsernameReadonly ? "Update Account" : "Add Account",
                        _titleStyle
                    );
                    // Username
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Username / Email", _labelStyle);
                    EditorGUILayout.GetControlRect(false, 1);
                    using (new EditorGUI.DisabledScope(_isUsernameReadonly))
                        _username = EditorGUILayout.TextField(_username);
                    // Password
                    EditorGUILayout.GetControlRect(false, 12);
                    EditorGUILayout.LabelField("Password", _labelStyle);
                    EditorGUILayout.GetControlRect(false, 1);
                    _password = EditorGUILayout.PasswordField(_password);
                    // Login Button
                    EditorGUILayout.GetControlRect(false, 12);
                    var buttonRect = EditorGUILayout.GetControlRect(true, 42);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_username) ||
                                                       string.IsNullOrWhiteSpace(_password)))
                        if (GUI.Button(buttonRect, "Login", _nextStateButtonStyle))
                        {
                            _wereCredentialsOr2AuthInvalid = false;
                            ValidateCredentials();
                            _state = State.VerifyingCredentials;
                        }

                    GUILayout.FlexibleSpace();
                    break;
                case State.VerifyingCredentials:
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Verifying Credentials", _titleStyle);
                    EditorGUILayout.GetControlRect(false, 12);
                    const float spinnerSize = 32;
                    var spinnerRect = EditorGUILayout.GetControlRect(false, spinnerSize);
                    spinnerRect.x += (spinnerRect.width - spinnerSize) / 2;
                    spinnerRect.width = spinnerSize;
                    GUI.DrawTexture(spinnerRect, _spinner.Update());
                    GUILayout.FlexibleSpace();
                    break;
                case State.Enter2Auth:
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        "Enter 2FA Code",
                        _titleStyle
                    );
                    // 2FA Code
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        "Enter the code " + (
                            _2FaType == TwoFactorType.Email
                                ? "you've received via email"
                                : "from your authenticator app"
                        ),
                        _labelStyle
                    );
                    EditorGUILayout.GetControlRect(false, 1);
                    _2faCode = EditorGUILayout.TextField(_2faCode);
                    // Verify Button
                    EditorGUILayout.GetControlRect(false, 12);
                    buttonRect = EditorGUILayout.GetControlRect(true, 42);
                    using (new EditorGUI.DisabledScope(_2faCode?.Length != 6))
                        if (GUI.Button(buttonRect, "Continue", _nextStateButtonStyle))
                        {
                            _wereCredentialsOr2AuthInvalid = false;
                            Validate2FA();
                            _state = State.Verifying2Auth;
                        }

                    GUILayout.FlexibleSpace();
                    break;
                case State.Verifying2Auth:
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Verifying 2FA Code", _titleStyle);
                    EditorGUILayout.GetControlRect(false, 12);
                    spinnerRect = EditorGUILayout.GetControlRect(false, spinnerSize);
                    spinnerRect.x += (spinnerRect.width - spinnerSize) / 2;
                    spinnerRect.width = spinnerSize;
                    GUI.DrawTexture(spinnerRect, _spinner.Update());
                    GUILayout.FlexibleSpace();
                    break;
                case State.Error:
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(_errorTitle, _titleStyle);
                    EditorGUILayout.GetControlRect(false, 12);
                    spinnerRect = EditorGUILayout.GetControlRect(false, spinnerSize);
                    spinnerRect.x += (spinnerRect.width - spinnerSize) / 2;
                    spinnerRect.width = spinnerSize;
                    var errorIcon = EditorGUIUtility.IconContent("console.erroricon").image;
                    GUI.DrawTexture(spinnerRect, errorIcon);
                    EditorGUILayout.GetControlRect(false, 12);
                    EditorGUILayout.HelpBox(_errorMessage, MessageType.None);
                    EditorGUILayout.GetControlRect(false, 12);
                    buttonRect = EditorGUILayout.GetControlRect(true, 42);
                    if (GUI.Button(buttonRect, "Close", _nextStateButtonStyle))
                    {
                        _password = "";
                        _2faCode = "";
                        _2FaType = TwoFactorType.None;
                        _state = State.EnterCredentials;
                    }

                    GUILayout.FlexibleSpace();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Repaint();
        }

        // ReSharper disable once ParameterHidesMember
        private void ShowError(string title, string message)
        {
            _errorTitle = title;
            _errorMessage = message;
            _state = State.Error;
        }

        #region Login

        private void ValidateCredentials()
        {
            VRCSdkControlPanel.InitAccount();
            // APIUser.Login(_username, _password, SuccessCallback, ErrorCallback, TwoFactorCallback);
            // ToDo: Switch to using custom API instead of VRChat's (cause their implementations are shite)
            API.Login(_username, _password,
                // Success
                (authCookie, id) => { },
                // Invalid Credentials
                () => { },
                // 2FA Required
                (cookie, type) => { },
                // Error
                error => { }
            );
        }

        private void SuccessCallback(ApiModelContainer<APIUser> c)
        {
            Debug.Log("CALLBACK: Success");

            var user = c.Model as APIUser;
            string authCookie;
            if (c.Cookies.TryGetValue("twoFactorAuth", out var twoFactorAuthCookie))
            {
                ApiCredentials.Set(user!.username, _username, "vrchat", c.Cookies["auth"], twoFactorAuthCookie);
                authCookie = c.Cookies["auth"];
            }
            else if (c.Cookies.TryGetValue("auth", out authCookie))
            {
                ApiCredentials.Set(user!.username, _username, "vrchat", authCookie);
            }
            else
            {
                APIUser.Logout();
                const string errorMessage = "Login was successful but VRChat did not return the 'auth' cookie!";
                ShowError("Error logging in", errorMessage);
                Debug.Log("Error logging in: " + errorMessage);
                _state = State.EnterCredentials;
                _password = "";
                return;
            }

            AnalyticsSDK.LoggedInUserChanged(user);

            if (!APIUser.CurrentUser.canPublishAllContent)
            {
                if (SessionState.GetString("HasShownContentPublishPermissionsDialogForUser", "") != user.id)
                {
                    SessionState.SetString("HasShownContentPublishPermissionsDialogForUser", user.id);
                    VRCSdkControlPanel.ShowContentPublishPermissionsDialog();
                }
            }

            // Fetch platforms that the user can publish to
            ApiUserPlatforms.Fetch(user.id, null, null);

            Complete(authCookie, twoFactorAuthCookie);
        }

        private void ErrorCallback(ApiModelContainer<APIUser> c)
        {
            Debug.Log("CALLBACK: Error");

            APIUser.Logout();
            var error = c.Error;
            ShowError("Error logging in", error);
            Debug.Log("Error logging in: " + error);
            _password = "";
        }

        private void TwoFactorCallback(ApiModelContainer<API2FA> c)
        {
            Debug.Log("CALLBACK: TwoFactor");

            if (c.Cookies.TryGetValue("auth", out var authCookie))
                ApiCredentials.Set(_username, _username, "vrchat", authCookie);
            var model2Fa = c.Model as API2FA;
            if (model2Fa!.requiresTwoFactorAuth.Contains(API2FA.TIME_BASED_ONE_TIME_PASSWORD_AUTHENTICATION))
            {
                _2FaType = TwoFactorType.TOTP;
                Debug.Log("OLD STATE: " + _state);
                _state = State.Enter2Auth;
                Debug.Log("NEW STATE: " + _state);
                Repaint();
                Debug.Log("TOTP");
            }
            else if (model2Fa.requiresTwoFactorAuth.Contains(API2FA.EMAIL_BASED_ONE_TIME_PASSWORD_AUTHENTICATION))
            {
                _2FaType = TwoFactorType.Email;
                _state = State.Enter2Auth;
                Debug.Log("Email");
            }
            else
            {
                _2FaType = TwoFactorType.None;
                Complete(authCookie, null);
                Debug.Log("Success");
            }
        }

        #endregion

        #region 2FA

        private void Validate2FA()
        {
            var type = _2FaType == TwoFactorType.Email
                ? API2FA.EMAIL_BASED_ONE_TIME_PASSWORD_AUTHENTICATION
                : API2FA.TIME_BASED_ONE_TIME_PASSWORD_AUTHENTICATION;
            APIUser.VerifyTwoFactorAuthCode(_2faCode, type, _username, _password,
                delegate(ApiDictContainer c)
                {
                    var user = c.Model as APIUser;
                    string authCookie;
                    if (c.Cookies.TryGetValue("twoFactorAuth", out var twoFactorAuthCookie))
                    {
                        ApiCredentials.Set(user!.username, _username, "vrchat", c.Cookies["auth"], twoFactorAuthCookie);
                        authCookie = c.Cookies["auth"];
                    }
                    else if (c.Cookies.TryGetValue("auth", out authCookie))
                    {
                        ApiCredentials.Set(user!.username, _username, "vrchat", authCookie);
                    }
                    else
                    {
                        _wereCredentialsOr2AuthInvalid = true;
                        APIUser.Logout();
                        const string errorMessage = "Login was successful but VRChat did not return the 'auth' cookie!";
                        ShowError("Error logging in", errorMessage);
                        Debug.Log("Error logging in: " + errorMessage);
                        _state = State.EnterCredentials;
                        _password = "";
                        return;
                    }

                    AnalyticsSDK.LoggedInUserChanged(user);

                    if (!APIUser.CurrentUser.canPublishAllContent)
                    {
                        if (SessionState.GetString("HasShownContentPublishPermissionsDialogForUser", "") != user.id)
                        {
                            SessionState.SetString("HasShownContentPublishPermissionsDialogForUser", user.id);
                            VRCSdkControlPanel.ShowContentPublishPermissionsDialog();
                        }
                    }

                    // Fetch platforms that the user can publish to
                    ApiUserPlatforms.Fetch(user.id, null, null);

                    Complete(authCookie, twoFactorAuthCookie);
                },
                delegate
                {
                    _wereCredentialsOr2AuthInvalid = true;
                    _2faCode = "";
                    _state = State.Enter2Auth;
                }
            );
        }

        #endregion

        private void Complete(Cookie authCookie, Cookie twoFactorAuthCookie)
        {
            // ToDo: Convert cookies from object to json
            
            var id = APIUser.CurrentUser.id;
            var acc = Config.GetAccounts().FirstOrDefault(a => a.Id == id);

            if (acc == null)
            {
                acc = new AccountStruct()
                {
                    Id = id,
                    Username = APIUser.CurrentUser.username,
                    ProfilePictureUrl = APIUser.CurrentUser.profilePicImageUrl,
                };
            }
            else
            {
                acc = new AccountStruct()
                {
                    ProfilePictureUrl = APIUser.CurrentUser.profilePicImageUrl,
                };
            }

            Config.UpdateAccount(acc);
            Accounts.KeyringManager.Set(id, authCookie, twoFactorAuthCookie);
            Close();

            var onAuthenticationVerifiedAction = AccountWindowGUIHook.OnAuthenticationVerifiedAction;
            if (onAuthenticationVerifiedAction != null)
                onAuthenticationVerifiedAction();
        }
    }
}