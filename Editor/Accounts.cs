using System;
using System.IO;
using System.Reflection;
using Foxscore.EasyLogin.KeyringManagers;
using UnityEngine;
using UnityEditor;

// ReSharper disable once CheckNamespace
namespace Foxscore.EasyLogin
{
    public class AccountStruct
    {
        public string Id;
        public string Username;
        public string ProfilePictureUrl;
        
        private Texture2D _texture;

        public Texture2D ProfilePicture
        {
            get
            {
                if (_texture != null) return _texture;
                return null;
                // ToDo: Load image
            }
        }
    }
    
    [InitializeOnLoad]
    public static class Accounts
    {
        private const string KeystoreDllPathWindows =
            "Packages/dev.foxscore.easy-login/Managed/Windows/CredentialManagement.dll";

        internal static readonly IKeyringManager KeyringManager;

        public static AccountStruct CurrentAccount { get; private set; }

        static Accounts()
        {
#if UNITY_EDITOR_WIN
            KeyringManager = new WindowsCredentialKeyringManager();
#elif UNITY_EDITOR_OSX
            KeyringManager = new UnsecureCredentialsManager();
#elif UNITY_EDITOR_LINUX
            KeyringManager = new UnsecureCredentialsManager();
#else
            KeyringManager = new UnsecureCredentialsManager();
#endif
        }
    }
}