using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using BestHTTP;
using Foxscore.EasyLogin.Extensions;
using Foxscore.EasyLogin.KeyringManagers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

using VrcApi =  VRC.Core.API;

namespace Foxscore.EasyLogin
{
    public delegate void On2FaRequired(string auth, TwoFactorType type);

    public delegate void OnLoginSuccess(string auth, string userId, string username, string displayName, string profilePictureUrl);

    public delegate void On2FaSuccess(string twoFactorAuth);

    public delegate void OnCookieVerificationSuccess();

    public delegate void OnFetchProfileSuccess(string id, string username, string displayName, string profilePictureUrl);
    
    public delegate void OnAssetFetchSuccess(byte[] bytes);

    public delegate void OnInvalidCredentials();

    public delegate void OnError(string error);

    [InitializeOnLoad]
    public static class API
    {
        public const string Endpoint = "https://api.vrchat.cloud/api/1/";
        private const string UserAgent = "VRC.Core.BestHTTP";
        // private const string UserAgent = "Foxscore_EasyLogin/1.0";

        private static readonly string MacAddress;
        private static readonly string UnityVersion;
        
        static API()
        {
            MacAddress = SystemInfo.deviceUniqueIdentifier;
            UnityVersion = Application.unityVersion;
        }

        private static HTTPRequest CreateRequest(string apiEndpoint, HTTPMethods method, AuthTokens authTokens, bool hasBody = false)
        {
            var request = new HTTPRequest(new Uri(apiEndpoint), method);
            request.OnBeforeHeaderSend += req =>
            {
                req.RemoveHeader("Cookie");
                if (authTokens != null)
                {
                    var cookies = $"auth={authTokens.Auth}";
                    if (!string.IsNullOrEmpty(authTokens.TwoFactorAuth))
                        cookies += $";twoFactorAuth={authTokens.TwoFactorAuth}";
                    req.AddHeader("Cookie", cookies);
                }
            };
            request.DisableCache = true;
            request.Cookies.Clear();
            request.RemoveHeaders();
            
            request.AddHeader("X-MacAddress", MacAddress);
            request.AddHeader("X-SDK-Version", VRC.Tools.SdkVersion);
            request.AddHeader("X-Platform", "standalonewindows");
            request.AddHeader("X-GameServer-Version", "editor-non-play-mode");
            request.AddHeader("X-Unity-Version", UnityVersion);
            request.AddHeader("X-Store", "unknown");
            request.SetHeader("User-Agent", UserAgent);
            request.AddHeader("Content-Type", hasBody
                ? "application/json"
                : "application/x-www-form-urlencoded"
            );
            
            return request;
        }

        public static void Login(string username, string password,
            OnLoginSuccess onSuccess, OnInvalidCredentials onInvalidCredentials, On2FaRequired on2FaRequired,
            OnError onError)
        {
            try
            {
                var request = CreateRequest(Endpoint + "auth/user", HTTPMethods.Get, null);
                request.SetHeader("Authorization", "Basic " + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        Uri.EscapeDataString(username) + ':' + Uri.EscapeDataString(password)
                    )
                ));
                var response = request.SendAndAwait();

                switch (response.StatusCode)
                {
                    case 200:
                        var authCookie = (response.Cookies ?? new()).FirstOrDefault(c => c.Name == "auth");
                        if (authCookie == null)
                        {
                            onError("Login response is missing auth header. Please report this to the developers of Easy Login.");
                            return;
                        }
                        
                        JObject jObject;
                        try
                        {
                            jObject = JObject.Parse(response.DataAsText);
                        }
                        catch (Exception e)
                        {
                            onError("Failed to parse response body: " + e.Message);
                            return;
                        }

                        // Success
                        if (jObject.TryGetPropertyValue("id", out string idToken))
                        {
                            if (
                                !jObject.TryGetPropertyValue("username", out string usernameToken) ||
                                !jObject.TryGetPropertyValue("displayName", out string displayNameToken)
                            )
                            {
                                onError($"Unexpected response: {response.DataAsText}");
                                return;
                            }
                            
                            if (jObject.TryGetPropertyValue("currentAvatarImageUrl", out string profilePictureUrl))
                            {
                                if (string.IsNullOrWhiteSpace(profilePictureUrl))
                                    profilePictureUrl = null;
                            }
                            
                            onSuccess(authCookie.Value, idToken, usernameToken, displayNameToken, profilePictureUrl);
                        }
                        // 2FA
                        else if (jObject.TryGetPropertyValue("requiresTwoFactorAuth", out JArray validAuthsArrayToken))
                        {
                            on2FaRequired(authCookie.Value, validAuthsArrayToken.Values<string>().Contains("totp")
                                ? TwoFactorType.TOTP
                                : TwoFactorType.Email
                            );
                        }
                        // Invalid response
                        else
                        {
                            onError($"Unexpected response: {response.DataAsText}");
                            // onInvalidCredentials();
                        }

                        return;

                    case 401:
                        onInvalidCredentials();
                        return;

                    default:
                        onError($"Unexpected status code: {response.StatusCode}");
                        return;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onError("Internal error");
            }
        }

        public static void VerifyTokens(AuthTokens tokens, OnCookieVerificationSuccess onSuccess,
            OnInvalidCredentials onInvalidCredentials, OnError onError)
        {
            try
            {
                var request = CreateRequest(Endpoint + "auth", HTTPMethods.Get, tokens);
                var response = request.SendAndAwait();
                if (response.StatusCode != 200)
                {
                    onInvalidCredentials();
                    return;
                }

                JObject jObject;
                try
                {
                    jObject = JObject.Parse(response.DataAsText);
                }
                catch (Exception e)
                {
                    onError("Failed to parse response body: " + e.Message);
                    return;
                }

                // Invalid response
                if (!jObject.TryGetPropertyValue("ok", out bool okToken))
                {
                    onError($"Unexpected response: {response.DataAsText}");
                    return;
                }

                // Success
                if (okToken)
                    onSuccess();
                // Token is no longer valid
                else
                    onInvalidCredentials();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onError("Internal Error");
            }
        }

        public static void FetchProfile(AuthTokens tokens, OnFetchProfileSuccess onSuccess,
            OnInvalidCredentials onInvalidCredentials, OnError onError)
        {
            try
            {
                var request = CreateRequest(Endpoint + "auth/user", HTTPMethods.Get, tokens);
                var response = request.SendAndAwait();

                switch (response.StatusCode)
                {
                    case 200:
                        JObject jObject;
                        try
                        {
                            jObject = JObject.Parse(response.DataAsText);
                        }
                        catch (Exception e)
                        {
                            onError("Failed to parse response body: " + e.Message);
                            return;
                        }

                        // Get the profile image
                        if (jObject.TryGetPropertyValue("userIcon", out string profilePictureUrl))
                        {
                            if (string.IsNullOrWhiteSpace(profilePictureUrl))
                                profilePictureUrl = null;
                        }
                        // Fallback to avatar image
                        if (
                            profilePictureUrl == null &&
                            jObject.TryGetPropertyValue("currentAvatarImageUrl", out profilePictureUrl))
                        {
                            if (string.IsNullOrWhiteSpace(profilePictureUrl))
                                profilePictureUrl = null;
                        }

                        // Success
                        if (jObject.TryGetPropertyValue("id", out string idToken) &&
                            jObject.TryGetPropertyValue("username", out string usernameToken) &&
                            jObject.TryGetPropertyValue("displayName", out string displayNameToken))
                            onSuccess(idToken, usernameToken, displayNameToken, profilePictureUrl);
                        // Invalid response
                        else
                            onError($"Unexpected response: {response.DataAsText}");

                        return;

                    case 401:
                        onInvalidCredentials();
                        return;

                    default:
                        onError($"Unexpected status code: {response.StatusCode}");
                        return;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onError("Internal error");
            }
        }

        public static void FetchAsset(string url, OnAssetFetchSuccess onSuccess, OnError onError)
        {
            try
            {
                var request = CreateRequest(url, HTTPMethods.Get, null);
                var response = request.SendAndAwait();

                if (response.StatusCode is 200)
                    onSuccess(response.Data);
                else
                    onError($"Unexpected status code: {response.StatusCode}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onError("Internal error");
            }
        }

        public static void Verify2Fa(string auth, string code, TwoFactorType type, On2FaSuccess onSuccess,
            OnInvalidCredentials onInvalidCredentials, OnError onError)
        {
            try
            {
                var otpType = type == TwoFactorType.Email ? "emailotp" : "totp";
                var request = CreateRequest(
                    $"{Endpoint}auth/twofactorauth/{otpType}/verify",
                    HTTPMethods.Post,
                    new AuthTokens(auth, null),
                    true
                );
                request.RawData = Encoding.UTF8.GetBytes($"{{\"code\":\"{code}\"}}");
                var response = request.SendAndAwait();
                if (response.StatusCode != 200)
                    onInvalidCredentials();
                else
                    onSuccess(response.Cookies.First(c => c.Name == "twoFactorAuth").Value);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onError("Internal Error");
            }
        }

        public static void InvalidateSession(AuthTokens tokens)
        {
            var request = CreateRequest($"{Endpoint}logout", HTTPMethods.Put, tokens);
            request.Cookies.Add(new("auth", tokens.Auth));
            if (!string.IsNullOrWhiteSpace(tokens.TwoFactorAuth))
                request.Cookies.Add(new("twoFactorAuth", tokens.TwoFactorAuth));
            request.Send();
        }
    }
}