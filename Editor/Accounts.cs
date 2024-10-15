using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Foxscore.EasyLogin.Hooks;
using Foxscore.EasyLogin.KeyringManagers;
using Newtonsoft.Json;
using UnityEngine;
using UnityEditor;
using VRC.Core;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin
{
    public class AccountStruct
    {
        public string Id;
        public string Username;
        public string DisplayName;
        public string ProfilePictureUrl;
    }

    [InitializeOnLoad]
    public static class Accounts
    {
        private const string KeystoreDllPathWindows =
            "Packages/dev.foxscore.easy-login/Managed/Windows/CredentialManagement.dll";

        // ReSharper disable once InconsistentNaming
        private const string SessionKey_CurrentUserId = "Foxscore_EasyLogin::currentUserId";

        internal static readonly IKeyringManager KeyringManager;
        private static readonly PropertyInfo CurrentUserProperty;

        public static AccountStruct CurrentAccount { get; private set; }
        public static bool? CanCurrentAccountPublishAvatars { get; private set; }
        public static bool? CanCurrentAccountPublishWorlds { get; private set; }

        public static void SetCurrentAccount(AccountStruct account)
        {
            if (account == null)
                throw new ArgumentNullException("account");
            
            EditorApplication.delayCall += () => SessionState.SetString(SessionKey_CurrentUserId, account.Id);
            CurrentAccount = account;
            CanCurrentAccountPublishAvatars = null;
            CanCurrentAccountPublishWorlds = null;
            var credentials = KeyringManager.Get(account.Id);
            EditorApplication.delayCall += () =>
            {
                VRCSdkControlPanel.window?.Repaint();
                ApiCredentials.Set(
                    account.Username, account.Username,
                    "vrchat",
                    credentials.Auth, credentials.TwoFactorAuth
                );
                APIUser.InitialFetchCurrentUser(user =>
                {
                    // CanCurrentAccountPublishAvatars = (user.Model as APIUser).canPublishAvatars;
                    // CanCurrentAccountPublishWorlds = (user.Model as APIUser).canPublishWorlds;
                    
                    //region Update profile icon
                    var apiUser = user.Model as APIUser;

                    var displayName = apiUser!.displayName;
                    var iconUrl = apiUser!.userIcon;
                    if (string.IsNullOrEmpty(iconUrl)) iconUrl = apiUser.currentAvatarImageUrl;
                    
                    if (displayName != account.DisplayName || account.ProfilePictureUrl != iconUrl)
                    {
                        account.DisplayName = displayName;
                        account.ProfilePictureUrl = iconUrl;
                        Config.UpdateAccount(account);
                        ProfilePictureCache.ForceRedownload(account);
                    }
                    //endregion
                }, error =>
                {
                    if (error == null)
                        Debug.LogError("Error while logging in");
                    else
                        Debug.LogError("Failed to login: " + error.Error);
                });
            };
        }

        public static void ClearCurrentAccount()
        {
            EditorApplication.delayCall += () => SessionState.EraseString(SessionKey_CurrentUserId);
            CurrentAccount = null;
            CanCurrentAccountPublishAvatars = false;
            CanCurrentAccountPublishWorlds = false;
            EditorApplication.delayCall += () =>
            {
                ApiCredentials.Clear();
                CurrentUserProperty.SetValue(null, null);
            };
        }

        static Accounts()
        {
            if (Preferences.UseOriginalLoginSystem)
                ApiCredentials.Clear();
            
            CurrentUserProperty =
                typeof(APIUser).GetProperty(nameof(APIUser.CurrentUser), BindingFlags.Public | BindingFlags.Static);

#if UNITY_EDITOR_WIN
            KeyringManager = new WindowsCredentialKeyringManager();
#elif UNITY_EDITOR_OSX
            KeyringManager = new UnsecureCredentialsManager();
#elif UNITY_EDITOR_LINUX
            KeyringManager = new UnsecureCredentialsManager();
#else
            KeyringManager = new UnsecureCredentialsManager();
#endif

            var currentUserId = SessionState.GetString(SessionKey_CurrentUserId, null);
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                var acc = Config.GetAccounts().FirstOrDefault(a => a.Id == currentUserId);
                if (acc != null)
                    SetCurrentAccount(acc);
            }
        }
    }
}