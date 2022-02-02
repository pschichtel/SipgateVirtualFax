using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace SipgateVirtualFax.Core.Sipgate;

public class OAuth2ImplicitFlowException : Exception
{
    public OAuth2ImplicitFlowException(string message) : base(message)
    {
    }
}

public class OAuth2ImplicitFlowHeaderProvider : IAuthorizationHeaderProvider
{
    private static readonly Uri DefaultAuthorizationUri = new("https://login.sipgate.com/auth/realms/sipgate-apps/protocol/openid-connect/auth");
    private static readonly Uri DefaultRedirectUri = new("https://localhost:31337");
    // Looks like a secret, but it really isn't
    private const string DefaultClientId = "2678637-1-60b58b61-8106-11ec-9225-1fac1a8d5fca:sipgate-apps";
    private static readonly string[] DefaultScopes =
    {
        "sessions:write", "sessions:fax:write", "history:read", "faxlines:read", "groups:faxlines:read",
        "groups:read", "groups:users:read"
    };

    private readonly IOAuthImplicitFlowHandler _handler;
    private readonly Uri _authorizationUri;
    private readonly Uri _redirectUri;
    private readonly string _clientId;
    private readonly string _scope;

    public OAuth2ImplicitFlowHeaderProvider(IOAuthImplicitFlowHandler handler) :
        this(handler, DefaultAuthorizationUri, DefaultRedirectUri, DefaultClientId, DefaultScopes)
    {
            
    }

    public OAuth2ImplicitFlowHeaderProvider(IOAuthImplicitFlowHandler handler, Uri authorizationUri, Uri redirectUri, string clientId, IEnumerable<string> scopes)
    {
        _handler = handler;
        _authorizationUri = authorizationUri;
        _redirectUri = redirectUri;
        _clientId = clientId;
        _scope = "openid" + string.Join("", scopes.Select(s => " " + s));
    }

    public bool RetryOn401 => true;

    private string FormatAsBearer(string accessToken) => $"Bearer {accessToken}";

    public async Task<string> GetHeaderValue(bool retry)
    {
        if (!retry)
        {
            var existingToken = await _handler.GetAccessTokenFromStorage();
            if (existingToken != null)
            {
                return FormatAsBearer(existingToken);
            }
        }

        var state = Guid.NewGuid().ToString();
        var nonce = Guid.NewGuid().ToString();
        var authorizationUriParams = HttpUtility.ParseQueryString(_authorizationUri.Query);
        authorizationUriParams.Add("response_type", "id_token token");
        authorizationUriParams.Add("client_id", _clientId);
        authorizationUriParams.Add("redirect_uri", _redirectUri.ToString());
        authorizationUriParams.Add("scope", _scope);
        authorizationUriParams.Add("state", state);
        authorizationUriParams.Add("nonce", nonce);
            
        var finalAuthUri = new UriBuilder(_authorizationUri)
        {
            Query = authorizationUriParams.ToString()
        }.Uri;
        var redirectionTarget = await _handler.Authorize(finalAuthUri);

        if (_redirectUri.Host != redirectionTarget.Host)
        {
            throw new OAuth2ImplicitFlowException(
                $"Redirect Host mismatched: '{_redirectUri.Host}' vs '{redirectionTarget.Host}'");
        }
            
        var fragment = redirectionTarget.Fragment;
        if (fragment == null || !fragment.StartsWith("#"))
        {
            throw new OAuth2ImplicitFlowException("Redirect URI did not contain a fragment!");
        }

        var redirectParams = HttpUtility.ParseQueryString(fragment.Substring(1));
        var returnedState = redirectParams.Get("state");
        if (returnedState != state)
        {
            throw new OAuth2ImplicitFlowException("Returned state did not match!");
        }

        var error = redirectParams.Get("error");
        if (!string.IsNullOrEmpty(error))
        {
            var description = redirectParams.Get("error_description");
            var uri = redirectParams.Get("error_uri");
            throw new OAuth2ImplicitFlowException(
                $"authorization error: code={error}, description={description}, uri={uri}");
        }

        var accessToken = redirectParams.Get("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new OAuth2ImplicitFlowException("No error, but also not access_token received!");
        }

        await _handler.StoreAccessToken(accessToken);
                
        return FormatAsBearer(accessToken);
    }

    public static bool UriMatchesRedirectUri(Uri uri, Uri redirectUri)
    {
        if (uri.Scheme != redirectUri.Scheme)
        {
            return false;
        }

        if (uri.Host != redirectUri.Host)
        {
            return false;
        }

        if (uri.Port != redirectUri.Port)
        {
            return false;
        }

        if (uri.AbsolutePath != redirectUri.AbsolutePath)
        {
            return false;
        }

        return true;
    }
}