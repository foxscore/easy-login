using System;
using System.Buffers.Text;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;

namespace Foxscore.EasyLogin
{
    public enum EncryptionLayerType
    {
        // 'None' is excluded on purpose
        Basic = 0,
        Password = 1,
    }
    
    public interface IEncryptionLayer
    {
        public bool IsUnlocked();
        public bool Unlock(string password);
        
        public void Setup(string password);
        public string GetCompareString();
        
        public string Encrypt(string value);
        public string Decrypt(string value);

        private const string SessionPasswordKey = "Foxscore_EasyLogin::VaultPassword";
        
        public static void ClearSessionPassword() => SessionState.EraseString(SessionPasswordKey);
        
        protected static void SetSessionPassword(string password) {
            if (Config.KeepVaultUnlockedForSession)
                SessionState.SetString(
                    SessionPasswordKey,
                    new BasicEncryption().Encrypt(password)
                );
        }

        protected static string GetSessionPassword()
        {
            if (!Config.KeepVaultUnlockedForSession) return null;
            
            var passwd = SessionState.GetString(SessionPasswordKey, null);
            return string.IsNullOrWhiteSpace(passwd)
                ? null
                : new BasicEncryption().Decrypt(passwd);
        }
    }
    
    public static class EncryptionLayerExtensions {
        public static EncryptionLayerType GetEncryptionLayerType(this IEncryptionLayer encryptionLayer)
        {
            return encryptionLayer switch
            {
                BasicEncryption => EncryptionLayerType.Basic,
                PasswordEncryption => EncryptionLayerType.Password,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public class NoEncryption : IEncryptionLayer
    {
        public bool IsUnlocked() => true;
        public bool Unlock(string field) => true;

        public void Setup(string password) { }
        public string GetCompareString() => null;

        public string Encrypt(string value) => value;
        public string Decrypt(string value) => value;
    }

    public class BasicEncryption : IEncryptionLayer
    {
        private static readonly byte[] Key =
            { 123, 45, 67, 89, 101, 123, 45, 67, 89, 101, 123, 45, 67, 89, 101, 123 };

        // ReSharper disable once InconsistentNaming
        private static readonly byte[] IV = { 12, 34, 56, 78, 90, 12, 34, 56, 78, 90, 12, 34, 56, 78, 90, 12 };
        
        public bool IsUnlocked() => true;
        public bool Unlock(string field) => true;

        public void Setup(string password) { }
        public string GetCompareString() => null;

        public string Encrypt(string value)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            using (var sw = new StreamWriter(cs))
                sw.Write(value);

            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decrypt(string value)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(Convert.FromBase64String(value));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }

    public class PasswordEncryption : IEncryptionLayer
    {
        private string _password;

        public bool IsUnlocked()
        {
            if (_password is not null)
                return true;

            var vaultPassword = IEncryptionLayer.GetSessionPassword();
            return vaultPassword is not null && Unlock(vaultPassword);
        }

        public bool Unlock(string password)
        {
            Setup(password);
            if (GetCompareString() == Config.EncryptionCompare)
            {
                IEncryptionLayer.SetSessionPassword(password);
                return true;
            }
            IEncryptionLayer.ClearSessionPassword();
            _password = null;
            return false;
        }

        public void Setup(string password)
        {
            _password = password;
        }

        public string GetCompareString() => Convert.ToBase64String(SHA512.Create()
            .ComputeHash(Encoding.UTF8.GetBytes(Environment.UserName + '|' + _password)));

        static byte[] GenerateSalt(int length)
        {
            var salt = new byte[length];
            using var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(salt);
            return salt;
        }
        
        static byte[] DeriveKey(string password, byte[] salt, int iterations)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations);
            return pbkdf2.GetBytes(32);
        }
        
        public string Encrypt(string value)
        {
            if (!IsUnlocked())
                throw new ApplicationException("The encryption layer is not yet unlocked");
            
            
            var salt = GenerateSalt(16);
            var key = DeriveKey(_password, salt, 10000);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(value);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);


            return Convert.ToBase64String(cipherBytes) + "|" + Convert.ToBase64String(salt) + "|" + Convert.ToBase64String(aes.IV);
        }

        public string Decrypt(string value)
        {
            if (!IsUnlocked())
                throw new ApplicationException("The encryption layer is not yet unlocked");
            
            var parts = value.Split('|');
            var cipherBytes = Convert.FromBase64String(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);

            var key = DeriveKey(_password, salt, 10000);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = Convert.FromBase64String(parts[2]);

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}