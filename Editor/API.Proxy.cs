using System;
using System.Text.RegularExpressions;
using BestHTTP;
using BestHTTP.Authentication;
using UnityEngine;

namespace Foxscore.EasyLogin
{
    public static partial class API
    {
        private static class Proxy
        {
            private static readonly string[] ProxyEnvironmentVariables =
            {
                // SOCKS
                "SOCKS_PROXY",
                "SOCKS5_PROXY",
                "SOCKS4_PROXY",
                
                // HTTP
                "HTTP_PROXY",
                "HTTPS_PROXY",
                "FTP_PROXY",
                
                // General
                "ALL_PROXY",
                "PROXY",
                "PROXY_SERVER",
            };

            public static void Install(HTTPRequest request)
            {
                var proxySettings = Config.Proxy;
                switch (proxySettings)
                {
                    case { source: ProxySource.Custom }:
                        Debug.Log(proxySettings.useAuthentication);
                        var credentials = proxySettings.useAuthentication
                            ? new Credentials(proxySettings.authenticationType, proxySettings.username,
                                proxySettings.password)
                            : null;
                        request.Proxy = proxySettings.type switch
                        {
                            ProxyType.Http => new HTTPProxy(
                                new Uri(proxySettings.address), credentials,
                                proxySettings.isTransparent, proxySettings.sendWholeUrl,
                                proxySettings.nonTransparentForHttps
                            ),
                            ProxyType.Socks => new SOCKSProxy(
                                new Uri(proxySettings.address), credentials
                            ),
                            _ => throw new ArgumentOutOfRangeException()
                        };
                        break;

                    case { source: ProxySource.System }:
                        foreach (var envVariable in ProxyEnvironmentVariables)
                        {
                            var envValue = Environment.GetEnvironmentVariable(envVariable);
                            if (!string.IsNullOrEmpty(envValue) && TryScanAndInstall(request, envVariable, envValue))
                                return;

                            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                                continue;

                            envValue = Environment.GetEnvironmentVariable(envVariable.ToLower());
                            if (!string.IsNullOrEmpty(envValue) &&
                                TryScanAndInstall(request, envVariable.ToLower(), envValue))
                                return;
                        }

                        break;
                }
            }

            private static bool TryScanAndInstall(HTTPRequest request, string envVarName, string proxyUrl)
            {
                try
                {
                    var parsedProxy = ParseProxyUrlManually(proxyUrl, envVarName);
                    if (parsedProxy == null)
                        return false;

                    // Create credentials if authentication is present
                    Credentials credentials = null;
                    if (parsedProxy.useAuthentication)
                    {
                        credentials = new Credentials(
                            parsedProxy.authenticationType,
                            parsedProxy.username,
                            parsedProxy.password
                        );
                    }

                    // Install the appropriate proxy type
                    request.Proxy = parsedProxy.type switch
                    {
                        ProxyType.Http => new HTTPProxy(
                            new Uri(parsedProxy.address), credentials,
                            parsedProxy.isTransparent, parsedProxy.sendWholeUrl,
                            parsedProxy.nonTransparentForHttps
                        ),
                        ProxyType.Socks => new SOCKSProxy(
                            new Uri(parsedProxy.address), credentials
                        ),
                        _ => null
                    };

                    return request.Proxy != null;
                }
                catch (Exception ex)
                {
                    // Log error if needed, but don't throw
                    Console.WriteLine($"Failed to parse proxy from {envVarName}: {ex.Message}");
                    return false;
                }
            }

            private static ProxySettings ParseProxyUrl(string proxyUrl, string source)
            {
                if (string.IsNullOrWhiteSpace(proxyUrl))
                    return null;

                proxyUrl = proxyUrl.Trim();

                var settings = new ProxySettings
                {
                    source = ProxySource.System,
                    // Set HTTP proxy defaults
                    isTransparent = false,
                    sendWholeUrl = true,
                    nonTransparentForHttps = true,
                    useAuthentication = false,
                    authenticationType = AuthenticationTypes.Basic
                };

                try
                {
                    // First try to parse with Uri class
                    bool uriParsed = false;

                    // Handle cases where scheme might be missing
                    if (!proxyUrl.Contains("://"))
                    {
                        string detectedScheme = DetectSchemeFromContext(proxyUrl, source);
                        proxyUrl = detectedScheme + "://" + proxyUrl;
                    }

                    if (!Uri.TryCreate(proxyUrl, UriKind.Absolute, out var uri))
                        goto Return;
                    
                    uriParsed = true;

                    // Extract type from scheme
                    settings.type = GetProxyTypeFromScheme(uri.Scheme);

                    // Build the address without user info
                    settings.address =
                        $"{uri.Scheme}://{uri.Host}:{(uri.Port > 0 ? uri.Port : GetDefaultPortForScheme(uri.Scheme))}";

                    // Extract authentication info
                    if (string.IsNullOrEmpty(uri.UserInfo))
                    {
                        settings.useAuthentication = true;
                        var userInfo = uri.UserInfo.Split(':');
                        settings.username = Uri.UnescapeDataString(userInfo[0]);

                        if (userInfo.Length > 1)
                        {
                            settings.password = Uri.UnescapeDataString(userInfo[1]);
                        }
                    }

                    Return:
                    return uriParsed
                        ? settings
                        : ParseProxyUrlManually(proxyUrl, source);
                }
                catch (Exception)
                {
                    return ParseProxyUrlManually(proxyUrl, source);
                }
            }

            private static ProxySettings ParseProxyUrlManually(string proxyUrl, string source)
            {
                // Comprehensive regex patterns for different proxy URL formats
                var patterns = new[]
                {
                    // With scheme and full auth: protocol://username:password@host:port
                    @"^(?<scheme>https?|socks[45]?|ftp)://(?<user>[^:]+):(?<pass>[^@]+)@(?<host>[^:]+):(?<port>\d+)$",

                    // With scheme and username only: protocol://username@host:port  
                    @"^(?<scheme>https?|socks[45]?|ftp)://(?<user>[^@]+)@(?<host>[^:]+):(?<port>\d+)$",

                    // With scheme: protocol://host:port
                    @"^(?<scheme>https?|socks[45]?|ftp)://(?<host>[^:]+):(?<port>\d+)$",

                    // Host:port with auth embedded: username:password@host:port
                    @"^(?<user>[^:]+):(?<pass>[^@]+)@(?<host>[^:]+):(?<port>\d+)$",

                    // Host:port with username: username@host:port
                    @"^(?<user>[^@]+)@(?<host>[^:]+):(?<port>\d+)$",

                    // Simple host:port
                    @"^(?<host>[^:]+):(?<port>\d+)$",

                    // Just hostname (use default port)
                    @"^(?<host>[a-zA-Z0-9\-\.]+)$"
                };

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(proxyUrl, pattern, RegexOptions.IgnoreCase);
                    if (!match.Success) continue;
                    
                    var settings = new ProxySettings
                    {
                        source = ProxySource.System,
                        isTransparent = false,
                        sendWholeUrl = true,
                        nonTransparentForHttps = true,
                        useAuthentication = false,
                        authenticationType = AuthenticationTypes.Basic
                    };

                    var host = match.Groups["host"].Value;
                    var port = 0;

                    // Parse port
                    if (match.Groups["port"].Success && int.TryParse(match.Groups["port"].Value, out port))
                    {
                        // Port parsed successfully
                    }

                    // Determine type from scheme or context
                    string scheme;
                    if (match.Groups["scheme"].Success)
                    {
                        settings.type = GetProxyTypeFromScheme(match.Groups["scheme"].Value);
                        scheme = match.Groups["scheme"].Value.ToLowerInvariant();
                        if (port == 0)
                            port = GetDefaultPortForScheme(scheme);
                    }
                    else
                    {
                        // Infer type from environment variable name and port
                        settings.type = InferProxyTypeFromContext(source, port);
                        if (port == 0)
                            port = GetDefaultPortForType(settings.type);
                    }

                    // Build the address
                    scheme = settings.type == ProxyType.Socks ? "socks5" : "http";
                    settings.address = $"{scheme}://{host}:{port}";

                    // Extract authentication
                    if (match.Groups["user"].Success)
                    {
                        settings.useAuthentication = true;
                        settings.username = Uri.UnescapeDataString(match.Groups["user"].Value);

                        if (match.Groups["pass"].Success)
                        {
                            settings.password = Uri.UnescapeDataString(match.Groups["pass"].Value);
                        }
                    }
                    
                    var envUser = Environment.GetEnvironmentVariable("PROXY_USER");
                    var envPass = Environment.GetEnvironmentVariable("PROXY_PASS");
                    if (!string.IsNullOrEmpty(envUser))
                    {
                        settings.useAuthentication = true;
                        settings.username = envUser;
                        if (string.IsNullOrEmpty(envPass))
                            envPass = Environment.GetEnvironmentVariable("PROXY_PASSWORD");
                        if (!string.IsNullOrEmpty(envPass))
                            settings.password = envPass;
                    }

                    return settings;
                }

                return null;
            }

            private static string DetectSchemeFromContext(string url, string source)
            {
                var lowerSource = source.ToLowerInvariant();

                // Check for SOCKS indicators
                if (lowerSource.Contains("socks") || IsCommonSocksPort(url))
                {
                    return lowerSource.Contains("socks4")
                        ? "socks4"
                        : "socks5";
                }
                
                return lowerSource.Contains("https")
                    ? "https"
                    : "http";
            }

            private static bool IsCommonSocksPort(string url)
            {
                // Common SOCKS ports
                string[] socksPortIndicators = { ":1080", ":9050", ":9150" };
                return Array.Exists(socksPortIndicators, url.Contains);
            }

            private static ProxyType GetProxyTypeFromScheme(string scheme) => scheme?.ToLowerInvariant() switch
            {
                "http" or "https" or "ftp" => ProxyType.Http,
                "socks4" or "socks5" or "socks" => ProxyType.Socks,
                _ => ProxyType.Http
            };

            private static ProxyType InferProxyTypeFromContext(string source, int port)
            {
                var lowerSource = source.ToLowerInvariant();

                // Check environment variable name for hints
                if (lowerSource.Contains("socks"))
                {
                    return ProxyType.Socks;
                }

                // Check port numbers for common proxy types
                return port is 1080 or 9050 or 9150
                    ? ProxyType.Socks
                    : ProxyType.Http;
            }

            private static int GetDefaultPortForScheme(string scheme) => scheme?.ToLowerInvariant() switch
            {
                "http" => 8080,
                "https" => 443,
                "socks4" or "socks5" or "socks" => 1080,
                _ => 8080
            };

            private static int GetDefaultPortForType(ProxyType type) => type switch
            {
                ProxyType.Http => 8080,
                ProxyType.Socks => 1080,
                _ => 8080
            };
        }
    }
}