using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using NLog;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;
using Cookie = CefSharp.Cookie;
using static SipgateVirtualFax.Core.Sipgate.OAuth2ImplicitFlowHeaderProvider;

namespace SipGateVirtualFaxGui;

public class LoginResult
{
    public readonly Uri Uri;
    public readonly Task<List<Cookie>> Cookies;

    public LoginResult(Uri uri, Task<List<Cookie>> cookies)
    {
        Cookies = cookies;
        Uri = uri;
    }
}

public class SimpleCookie
{
    [JsonProperty("name")]
    public string Name { get; }
        
    [JsonProperty("value")]
    public string Value { get; }
        
    [JsonProperty("path")]
    public string Path { get; }
        
    [JsonProperty("domain")]
    public string Domain { get; }

    public SimpleCookie(string name, string value, string path, string domain)
    {
        Name = name;
        Value = value;
        Path = path;
        Domain = domain;
    }
}
    
public class CookieJar
{
    [JsonProperty("items")]
    public SimpleCookie[] Cookies { get; }

    [JsonConstructor]
    public CookieJar(SimpleCookie[] cookies)
    {
        Cookies = cookies;
    }

    public override string ToString()
    {
        return $"{nameof(Cookies)}: {Cookies}";
    }
}

class GuiOauthImplicitFlowHandler : IOAuthImplicitFlowHandler
{
    private readonly Logger _logger = Logging.GetLogger("gui-oauth-handler");

    Task<string?> IOAuthImplicitFlowHandler.GetAccessTokenFromStorage()
    {
        try
        {
            return Task.FromResult(Util.ReadEncryptedString(AccessTokenPath(), Encoding.ASCII));
        }
        catch (IOException e)
        {
            _logger.Error(e, "Failed to read access token!");
            return Task.FromResult<string?>(null);
        }
    }

    async Task<Uri> IOAuthImplicitFlowHandler.Authorize(Uri authorizationUri)
    {
        var nameValueCollection = HttpUtility.ParseQueryString(authorizationUri.Query);
        var expectedRedirectUri = new Uri(nameValueCollection.Get("redirect_uri")!);
            
        var silentResult = await AttemptSilentRefresh(authorizationUri, expectedRedirectUri);
        if (silentResult != null)
        {
            return silentResult.Uri;
        }
            
        var authentication = new Authentication(authorizationUri, expectedRedirectUri);
        authentication.Show();
        authentication.Focus();
        var result = await authentication.Result;
        authentication.Close();
        if (result == null)
        {
            throw new OAuth2ImplicitFlowException("Authentication did not yield a result!");
        }

        await PersistCookies(await result.Cookies);
            
        return result.Uri;
    }

    private static async Task<LoginResult?> AttemptSilentRefresh(Uri authorizationUri, Uri expectedRedirectUri)
    {
        var json = Util.ReadEncryptedString(CookieJarPath(), Encoding.UTF8);
        if (json == null)
        {
            return null;
        }

        var cookieJar = JsonConvert.DeserializeObject<CookieJar>(json);
        if (cookieJar == null)
        {
            return null;
        }

        var cookieContainer = new CookieContainer();
        foreach (var simpleCookie in cookieJar.Cookies)
        {
            cookieContainer.Add(new System.Net.Cookie(simpleCookie.Name, simpleCookie.Value, simpleCookie.Path, simpleCookie.Domain));
        }

        using var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false
        };
        using var client = new HttpClient(handler);

        var uri = authorizationUri;
        while (true)
        {
            var response = await client.GetAsync(uri);
                
            var redirectionTarget = response.Headers.Location;
            if (redirectionTarget == null)
            {
                return null;
            }

            if (IsValidRedirectionTarget(redirectionTarget, expectedRedirectUri))
            {
                return new LoginResult(redirectionTarget, Task.FromResult(new List<Cookie>()));
            }

            uri = redirectionTarget;
        }

    }

    private Task PersistCookies(List<Cookie> cookies)
    {
        List<SimpleCookie> simpleCookies = new List<SimpleCookie>(cookies.Count);
        foreach (var cookie in cookies)
        {
            simpleCookies.Add(new SimpleCookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
        }

        var cookieJar = new CookieJar(simpleCookies.ToArray());
        return WriteStringEncrypted(CookieJarPath(), JsonConvert.SerializeObject(cookieJar), Encoding.UTF8);
    }

    public static string AccessTokenPath() => Util.AppPath("access-token.secret");
    public static string CookieJarPath() => Util.AppPath("cookie-jar.secret");

    private Task WriteStringEncrypted(string path, string content, Encoding encoding)
    {
        var data = encoding.GetBytes(content);
        var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, encrypted);
        return Task.CompletedTask;
    }

    public Task StoreAccessToken(string accessToken) =>
        WriteStringEncrypted(AccessTokenPath(), accessToken, Encoding.ASCII);
}