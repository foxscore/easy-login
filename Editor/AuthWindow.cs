using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using BestHTTP.Cookies;
using Foxscore.EasyLogin.Hooks;
using Foxscore.EasyLogin.KeyringManagers;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using VRC.Core;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin
{
    public class AuthWindow : EditorWindow
    {
        internal static void ShowAuthWindow(AccountStruct account = null)
        {
            var window = CreateWindow<AuthWindow>();
            if (account != null)
            {
                window._isUsernameReadonly = true;
                window._username = account.Username;
                // TODO: User 2FA Token if one is stored
                // TODO: On logout, checkbox for if we want to keep the 2auth token 
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
        private string _authToken;
        private bool _closeRequested;

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

            VRCSdkControlPanel.InitAccount();
        }

        private void OnGUI()
        {
            if (_closeRequested)
                Close();
            
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
                    if (_wereCredentialsOr2AuthInvalid)
                        EditorGUILayout.LabelField("Incorrect username and/or password"); // ToDo: Make red
                    // Login Button
                    EditorGUILayout.GetControlRect(false, 12);
                    var buttonRect = EditorGUILayout.GetControlRect(true, 42);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_username) ||
                                                       string.IsNullOrWhiteSpace(_password)))
                        if (GUI.Button(buttonRect, "Login", _nextStateButtonStyle))
                        {
                            _wereCredentialsOr2AuthInvalid = false;
                            new Task(ValidateCredentials).Start();
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
                    if (_wereCredentialsOr2AuthInvalid)
                        EditorGUILayout.LabelField("Incorrect 2FA code"); // ToDo: Make red
                    // Verify Button
                    EditorGUILayout.GetControlRect(false, 12);
                    buttonRect = EditorGUILayout.GetControlRect(true, 42);
                    using (new EditorGUI.DisabledScope(_2faCode?.Length != 6))
                        if (GUI.Button(buttonRect, "Continue", _nextStateButtonStyle))
                        {
                            _wereCredentialsOr2AuthInvalid = false;
                            new Task(Validate2FA).Start();
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

        private void ValidateCredentials()
        {
            API.Login(_username, _password,
                // Success
                (authCookie, id, username, profilePictureUrl) =>
                {
                    Complete(authCookie, null, id, username, profilePictureUrl);
                },
                // Invalid Credentials
                () =>
                {
                    _wereCredentialsOr2AuthInvalid = true;
                    _password = "";
                    EditorApplication.delayCall += () => GUI.FocusControl("");  
                    _state = State.EnterCredentials;
                },
                // 2FA Required
                (cookie, type) =>
                {
                    _authToken = cookie;
                    _2FaType = type;
                    _2faCode = "";
                    _wereCredentialsOr2AuthInvalid = false;
                    _state = State.Enter2Auth;
                },
                // Error
                error =>
                {
                    ShowError("Login Error", error);
                    Debug.LogError($"An error occured while trying to login: {error}");
                }
            );
        }

        private void Validate2FA()
        {
            var type = _2FaType == TwoFactorType.Email
                ? API2FA.EMAIL_BASED_ONE_TIME_PASSWORD_AUTHENTICATION
                : API2FA.TIME_BASED_ONE_TIME_PASSWORD_AUTHENTICATION;

            API.Verify2Fa(_authToken, _2faCode, _2FaType,
                // Success
                twoFactorAuthCookie =>
                {
                    API.FetchProfile(new AuthTokens(_authToken, twoFactorAuthCookie),
                        // Success 
                        (id, username, profilePictureUrl) =>
                        {
                            Complete(_authToken, twoFactorAuthCookie, id, username, profilePictureUrl);
                        },
                        // Invalid credentials - SHOULD NEVER OCCUR
                        () =>
                        {
                            ShowError("Failed to fetch profile", "The credentials have already expired! This should never happen! Please contact us on the Discord as soon as possible.");
                            Debug.LogError($"The credentials have already expired! This should never happen! Please contact us on the Discord as soon as possible.");
                        },
                        // Error
                        error =>
                        {
                            ShowError("Failed to fetch profile", error);
                            Debug.LogError($"An error occured while trying to fetch the profile during login: {error}");
                        }
                    );
                },
                // Invalid code
                () =>
                {
                    _wereCredentialsOr2AuthInvalid = true;
                    _2faCode = "";
                    GUI.FocusControl("");
                    _state = State.Enter2Auth;
                },
                // Error
                error =>
                {
                    ShowError("Login Error", error);
                    Debug.LogError($"An error occured while trying to login: {error}");
                }
            );
        }

        private void Complete(string authToken, string twoFactorAuthToken, string id, string username,
            string profilePictureUrl)
        {
            var acc = Config.GetAccounts().FirstOrDefault(a => a.Id == id);

            if (acc == null)
            {
                acc = new AccountStruct()
                {
                    Id = id,
                    Username = username,
                    ProfilePictureUrl = profilePictureUrl,
                };
            }
            else
            {
                acc.Username = username;
                acc.ProfilePictureUrl = profilePictureUrl;
            }

            Config.UpdateAccount(acc);
            Accounts.KeyringManager.Set(id, new AuthTokens(authToken, twoFactorAuthToken));
            Accounts.SetCurrentAccount(acc);

            _closeRequested = true;
        }
    }
}