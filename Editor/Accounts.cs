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

        private static readonly PropertyInfo CurrentUserProperty;
        internal static KeyringManager KeyringManager;

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
            if (credentials == null)
            {
                Log.Warning($"Attempted to login with {account.Username}, but no valid credentials were found.");
                return;
            }
            
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
                        Log.Error("Error while logging in");
                    else
                        Log.Error("Failed to login: " + error.Error);
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

        public static KeyringManager GetKeyringManager(IEncryptionLayer encryptionLayer)
        {
#if UNITY_EDITOR_WIN
            return new WindowsCredentialKeyringManager(encryptionLayer);
#elif UNITY_EDITOR_OSX
            return new UnsecureCredentialsManager(encryptionLayer);
#elif UNITY_EDITOR_LINUX
            return new UnsecureCredentialsManager(encryptionLayer);
#else
            return new UnsecureCredentialsManager(encryptionLayer);
#endif
        }

        static Accounts()
        {
            if (!Config.Enabled)
                ApiCredentials.Clear();
            
            CurrentUserProperty =
                typeof(APIUser).GetProperty(nameof(APIUser.CurrentUser), BindingFlags.Public | BindingFlags.Static);

            IEncryptionLayer encryptionLayer = Config.EncryptionLayerType switch
            {
                EncryptionLayerType.Basic => new BasicEncryption(),
                EncryptionLayerType.Password => new PasswordEncryption(),
                _ => throw new ArgumentOutOfRangeException()
            };
            KeyringManager = GetKeyringManager(encryptionLayer);
            
            if (encryptionLayer.IsUnlocked())
                AttemptAutoLogin();
        }

        public static void AttemptAutoLogin()
        {
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