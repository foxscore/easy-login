using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Foxscore.EasyLogin.Hooks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Directory = UnityEngine.Windows.Directory;

namespace Foxscore.EasyLogin
{
    public static class ProfilePictureCache
    {
        private const string ProfilePictureCacheDirectory = "Assets/Cache~/EasyLogin/ProfilePictures";
        private static readonly Dictionary<string, Texture2D> TextureCache = new();

        static ProfilePictureCache()
        {
            if (Directory.Exists(ProfilePictureCacheDirectory)) return;
            var parts = Path.GetFullPath(ProfilePictureCacheDirectory).Replace('\\', '/').Split('/');
            var currentPath = parts[0];
            for (var i = 1; i < parts.Length; i++)
                Directory.CreateDirectory(currentPath += "/" + parts[i]);
        }
        
        public static void ForceRedownload(AccountStruct account) {
            TextureCache.Remove(account.Id);
            new Task(() => DownloadImage(account, true)).Start();
        }

        private static string GetCachedImagePath(AccountStruct account) => $"{ProfilePictureCacheDirectory}/{account.Id}.png";

        private static Texture2D LoadTextureFromDisk(string path)
        {
            var texture = new Texture2D(1, 1);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        public static Texture2D GetFor(AccountStruct account)
        {
            if (TextureCache.TryGetValue(GetCachedImagePath(account), out var texture))
                return texture;

            var path = GetCachedImagePath(account);
            var fileInfo = new FileInfo(path);
            if (fileInfo.Exists && fileInfo.LastWriteTime >= DateTime.Now.AddHours(-1))
                return TextureCache[account.Id] = LoadTextureFromDisk(path);
            
            TextureCache[GetCachedImagePath(account)] = null;
            new Task(() => DownloadImage(account)).Start();
            return null;
        }

        private static void DownloadImage(AccountStruct account, bool ignoreCache = false)
        {
            try
            {
                var imagePath = GetCachedImagePath(account);
                API.FetchAsset(
                    account.ProfilePictureUrl,
                    bytes =>
                    {
                        // Cancel if we found a new icon during the download
                        if (!ignoreCache && !TextureCache.ContainsKey(account.Id)) return;
                        
                        using var fileStream = new FileStream(
                            imagePath,
                            FileMode.Create, FileAccess.Write, FileShare.None
                        );
                        fileStream.Seek(0, SeekOrigin.Begin);
                        fileStream.Write(bytes, 0, bytes.Length);
                        fileStream.Close();
                        EditorApplication.delayCall += () =>
                        {
                            TextureCache[GetCachedImagePath(account)] = LoadTextureFromDisk(imagePath);
                            VRCSdkControlPanel.window.Repaint();
                        };
                    },
                    error =>
                    {
                        Debug.LogError($"Error downloading image: {error}\n{account.ProfilePictureUrl}");
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}