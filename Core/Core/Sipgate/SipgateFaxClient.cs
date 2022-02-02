using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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

        Task StoreAccessToken(string accessToken);
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
                return JsonConvert.DeserializeObject<TRes>(responseContent)
                       ?? throw new InvalidOperationException("Requested expected response, but did not get one!");
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

            var groupsOfUser = new HashSet<string>(
                (await Task.WhenAll(groups.Select(async g => (g, await GetGroupMembers(g)))))
                    .Where(e => e.Item2.Contains(userId))
                    .Select(e => e.Item1)
            );

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