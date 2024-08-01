using System;
using System.Buffers.Text;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using BestHTTP;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Cookie = BestHTTP.Cookies.Cookie;

namespace Foxscore.EasyLogin
{
    public delegate void On2FaRequired(Cookie authCookie, TwoFactorType type);

    public delegate void OnLoginSuccess(Cookie authCookie, string userId);

    public delegate void On2FaSuccess(Cookie twoFactorAuthCookie);

    public delegate void OnInvalidCredentials();

    public delegate void OnError(string error);

    public static class API
    {
        public const string Endpoint = "https://vrchat.com/api/1/";

        public static void Login(string username, string password,
            OnLoginSuccess onSuccess, OnInvalidCredentials onInvalidCredentials, On2FaRequired on2FaRequired,
            OnError onError)
        {
            try
            {
                var request = new HTTPRequest(new Uri(Endpoint + "auth/user"), HTTPMethods.Get);
                request.SetHeader("User-Agent", "Foxscore_EasyLogin/1.0");
                request.SetHeader("Authorization", "Basic " + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        WebUtility.UrlEncode(username) + ':' + WebUtility.UrlEncode(password)
                    )
                ));
                var response = request.Send().Response;

                switch (response.StatusCode)
                {
                    case 200:
                        var authCookie = response.Cookies.First(c => c.Name == "auth");
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
                        if (jObject.TryGetValue("id", out var idToken))
                            onSuccess(authCookie, idToken.Value<string>());
                        // 2FA
                        else if (jObject.TryGetValue("requiresTwoFactorAuth", out var validAuthsArrayToken))
                            on2FaRequired(authCookie, validAuthsArrayToken.Contains("totp")
                                ? TwoFactorType.TOTP
                                : TwoFactorType.Email
                            );
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
        
        // ToDo: VerifyCookie 

        public static void Verify2Fa(Cookie authCookie, string code, TwoFactorType type, On2FaSuccess on2FaSuccess,
            OnInvalidCredentials onInvalidCredentials, OnError onError)
        {
            try
            {
                var otpType = type == TwoFactorType.Email ? "emailotp" : "totp";
                var request = new HTTPRequest(new Uri($"{Endpoint}auth/twofactorauth/{otpType}/verify"),
                    HTTPMethods.Post);
                request.SetHeader("User-Agent", "Foxscore_EasyLogin/1.0");
                request.Cookies.Add(authCookie);
                request.RawData = Encoding.UTF8.GetBytes($"{{\"code\":\"{code}\"}}");
                var response = request.Send().Response;
                if (response.StatusCode != 200)
                    onInvalidCredentials();
                else
                    on2FaSuccess(response.Cookies.First(c => c.Name == "twoFactorAuth"));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                onError("Internal Error");
            }
        }

        public static void InvalidateSession(Cookie authCookie)
        {
            var request = new HTTPRequest(new Uri($"{Endpoint}logout"),
                HTTPMethods.Put);
            request.SetHeader("User-Agent", "Foxscore_EasyLogin/1.0");
            request.Cookies.Add(authCookie);
            request.Send();
        }
    }
}