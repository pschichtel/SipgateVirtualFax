using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using NLog;

namespace SipgateVirtualFax.Core.Sipgate
{
    public interface IAuthorizationHeaderProvider
    {
        bool RetryOn401 { get; }
        Task<string> GetHeaderValue(bool retry);
    }

    public class BasicAuthHeaderProvider : IAuthorizationHeaderProvider
    {
        private readonly string _username;
        private readonly string _password;

        public BasicAuthHeaderProvider(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public bool RetryOn401 => false;

        public Task<string> GetHeaderValue(bool retry)
        {
            return Task.FromResult($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"))}");
        }
    }

    public interface IOAuthImplicitFlowHandler
    {
        /// <summary>
        /// Pull existing access_token out of a hat
        /// </summary>
        /// <returns>access_token or null</returns>
        Task<string?> GetAccessTokenFromStorage();
        
        /// <summary>
        /// Take the authorizationUri, present it to the user in some way, get it redirected to the redirect_uri
        /// and return the redirection target.
        /// </summary>
        /// <param name="authorizationUri">complete authorization URI</param>
        /// <returns>the redirection target</returns>
        Task<Uri> Authorize(Uri authorizationUri);
    }

    public class OAuth2ImplicitFlowException : Exception
    {
        public OAuth2ImplicitFlowException(string message) : base(message)
        {
        }
    }

    public class OAuth2ImplicitFlowHeaderProvider : IAuthorizationHeaderProvider
    {
        public static readonly Uri DefaultAuthorizationUri = new Uri("https://login.sipgate.com/auth/realms/sipgate-apps/protocol/openid-connect/auth");
        public static readonly Uri DefaultRedirectUri = new Uri("https://localhost:31337");
        // Looks like a secret, but it really isn't
        public const string DefaultClientId = "2678637-1-60b58b61-8106-11ec-9225-1fac1a8d5fca:sipgate-apps";
        public static readonly string[] DefaultScopes =
        {
            "sessions:write", "sessions:fax:write", "history:read", "faxlines:read", "groups:faxlines:read",
            "groups:read", "groups:users:read"
        };

        private IOAuthImplicitFlowHandler _handler;
        private readonly Uri _authorizationUri;
        private readonly Uri _redirectUri;
        private readonly string _clientId;
        private readonly string _scope;

        public OAuth2ImplicitFlowHeaderProvider(IOAuthImplicitFlowHandler handler) : this(handler, DefaultAuthorizationUri, DefaultRedirectUri, DefaultClientId, DefaultScopes)
        {
        }

        public OAuth2ImplicitFlowHeaderProvider(IOAuthImplicitFlowHandler handler, Uri authorizationUri, Uri redirectUri, string clientId, string[] scopes)
        {
            _handler = handler;
            _authorizationUri = authorizationUri;
            _redirectUri = redirectUri;
            _clientId = clientId;
            _scope = string.Join(" ", scopes.Prepend("openid"));;
        }

        public bool RetryOn401 => true;

        private string FormatAsBearer(string accessToken) => $"Bearer {accessToken}";

        public async Task<string> GetHeaderValue(bool retry)
        {
            var existingToken = await _handler.GetAccessTokenFromStorage();
            if (existingToken != null)
            {
                return FormatAsBearer(existingToken);
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
            
            var fragment = redirectionTarget.Fragment;
            if (fragment == null)
            {
                throw new OAuth2ImplicitFlowException("Redirect URI did not contain a fragment!");
            }

            var redirectParams = HttpUtility.ParseQueryString(fragment);
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
                
            return FormatAsBearer(accessToken);
        }
    }
    
    public class SipgateFaxClient
    {
        private readonly IAuthorizationHeaderProvider _authHeaderProvider;
        private const string DefaultBaseUrl = "https://api.sipgate.com/v2";

        private readonly Logger _logger = Logging.GetLogger("sipgate-client");
        private readonly string _baseUrl;
        private readonly HttpClient _client;

        public SipgateFaxClient(IAuthorizationHeaderProvider authHeaderProvider) : this(DefaultBaseUrl, authHeaderProvider)
        {
        }

        public SipgateFaxClient(string baseUrl, IAuthorizationHeaderProvider authHeaderProvider)
        {
            _baseUrl = baseUrl;
            _authHeaderProvider = authHeaderProvider;
            _client = new HttpClient();
        }

        private async Task<HttpResponseMessage> SendBasicRequest(HttpMethod method, string path, HttpContent? content)
        {
            async Task<HttpResponseMessage> DoSendRequest(bool retry)
            {
                if (_logger.IsTraceEnabled && content != null)
                {
                    var c = await content.ReadAsByteArrayAsync();
                    _logger.Trace(Encoding.UTF8.GetString(c));
                }
                var url = $"{_baseUrl}{path}";
                _logger.Info($"Request: {method} {url}");
                var message = new HttpRequestMessage
                {
                    Method = method,
                    RequestUri = new Uri(url),
                    Headers =
                    {
                        {"Authorization", await _authHeaderProvider.GetHeaderValue(retry)}
                    },
                    Content = content
                };

                return await _client.SendAsync(message);
            }

            var response = await DoSendRequest(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized && _authHeaderProvider.RetryOn401)
            {
                return await DoSendRequest(true);
            }
            return response;
        }

        private Task<HttpResponseMessage> SendRequestJson<TReq>(HttpMethod method, string path, TReq body)
        {
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            return SendBasicRequest(method, path, content);
        }

        private static async Task<HttpResponseMessage> SuccessfulResponse(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new SipgateApiHttpException("Unsuccessful response!", response.StatusCode, await response.Content.ReadAsStringAsync());
            }

            return response;
        }

        private static async Task<TRes> TryProcessJson<TRes>(HttpResponseMessage result)
        {
            var responseContent = await result.Content.ReadAsStringAsync();
            try
            {
                return JsonConvert.DeserializeObject<TRes>(responseContent);
            }
            catch (Exception e)
            {
                throw new SipgateApiNonJsonException(responseContent, e);
            }
        }

        private async Task<TRes> SendRequest<TRes>(HttpMethod method, string path)
        {
            return await TryProcessJson<TRes>(await SuccessfulResponse(await SendBasicRequest(method, path, null)));
        }

        private async Task<TRes> SendRequest<TReq, TRes>(HttpMethod method, string path, TReq body)
        {
            return await TryProcessJson<TRes>(await SuccessfulResponse(await SendRequestJson(method, path, body)));
        }

        private async Task<bool> SendRequest<TReq>(HttpMethod method, string path, TReq body)
        {
            var result = await SendRequestJson(method, path, body);
            return result.IsSuccessStatusCode;
        }

        public async Task<string> SendFax(string faxLine, string recipient, string pdfPath)
        {
            var request = new SendFaxRequest(faxLine, recipient, Path.GetFileName(pdfPath),
                Convert.ToBase64String(File.ReadAllBytes(pdfPath)));

            var response =
                await SendRequest<SendFaxRequest, SendFaxResponse>(HttpMethod.Post, "/sessions/fax",
                    request);
            
            return response.SessionId;
        }

        public Task<bool> AttemptFaxResend(string faxId, string faxlineId)
        {
            var request = new ResendFaxRequest(faxId, faxlineId);
            return SendRequest(HttpMethod.Post, "/sessions/fax/resend", request);
        }

        public Task<HistoryEntry> GetHistoryEntry(string entryId)
        {
            return SendRequest<HistoryEntry>(HttpMethod.Get, $"/history/{entryId}");
        }

        public async Task<Faxline[]> GetGroupFaxLines()
        {
            var response = await SendRequest<FaxlinesResponse>(HttpMethod.Get, "/groupfaxlines");
            return response.Items;
        }

        public async Task<IEnumerable<Faxline>> GetUserFaxLines(string userId)
        {
            var response = await SendRequest<FaxlinesResponse>(HttpMethod.Get, $"/{userId}/faxlines");
            return response.Items;
        }

        public async Task<string> GetUserId()
        {
            var response = await SendRequest<UserInfoResponse>(HttpMethod.Get, "/authorization/userinfo");
            return response.Id;
        }

        public async Task<string[]> GetGroupMembers(string groupId)
        {
            var response = await SendRequest<GroupMembersResponse>(HttpMethod.Get, $"/groups/{groupId}/users");
            return response.Items.Select(i => i.Id).ToArray();
        }

        public async Task<IEnumerable<Faxline>> GetUsableGroupFaxlines(string userId)
        {
            var groupFaxlines = await GetGroupFaxLines();

            // This is necessary, because sipgate apparently only allows you to use group faxlines of groups you are a
            // member of. While e.g. admins can see all faxlines, they can't use them all.
            // The following logic reduces the list of visible faxlines to those that can actually be used by the user.
            var groups = groupFaxlines
                .Select(f => f.GroupId ?? "")
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct();
            
            var groupsOfUser = (await Task.WhenAll(groups.Select(async g => (g, await GetGroupMembers(g)))))
                .Where(e => e.Item2.Contains(userId))
                .Select(e => e.Item1)
                .ToHashSet();

            return groupFaxlines.Where(f => groupsOfUser.Contains(f.GroupId ?? ""));
        }

        public async Task<Faxline[]> GetAllUsableFaxlines()
        {
            var userId = await GetUserId();
            return (await Task.WhenAll(GetUserFaxLines(userId), GetUsableGroupFaxlines(userId)))
                .SelectMany(lines => lines)
                .ToArray();
        }
    }

    public class SipgateApiHttpException : Exception
    {
        public HttpStatusCode Status { get; }
        public string Body { get; }

        public SipgateApiHttpException(string message, HttpStatusCode status, string body) : base($"{message} - {status} {body}")
        {
            Status = status;
            Body = body;
        }
    }

    public class SipgateApiNonJsonException : Exception
    {
        public string Body { get; }

        public SipgateApiNonJsonException(string body, Exception cause) : base($"Invalid JSON: {body}", cause)
        {
            Body = body;
        }
    }
}