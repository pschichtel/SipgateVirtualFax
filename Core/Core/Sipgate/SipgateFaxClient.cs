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
using NTwain.Data;

namespace SipgateVirtualFax.Core.Sipgate
{
    public class SipgateFaxClient
    {
        private const string DefaultBaseUrl = "https://api.sipgate.com/v2";

        private readonly Logger _logger = Logging.GetLogger("sipgate-client");
        private readonly string _baseUrl;
        private readonly string _basicAuth;
        private readonly HttpClient _client;

        public SipgateFaxClient(string username, string password) : this(DefaultBaseUrl, username, password)
        {
        }

        public SipgateFaxClient(string baseUrl, string username, string password)
        {
            _baseUrl = baseUrl;
            _basicAuth = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))}";
            _client = new HttpClient();
        }

        private async Task<HttpResponseMessage> SendBasicRequest(HttpMethod method, string path, HttpContent? content)
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
                    {"Authorization", _basicAuth}
                },
                Content = content
            };

            return await _client.SendAsync(message);
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

        public async Task<Faxline[]> GetUserFaxLines(string userId)
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

        public async Task<Faxline[]> GetAllUsableFaxlines()
        {
            var userId = await GetUserId();
            var userFaxLines = await GetUserFaxLines(userId);
            var groupFaxlines = await GetGroupFaxLines();

            var groups = groupFaxlines
                .Select(f => f.GroupId ?? "")
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct();
            
            var groupsOfUser = (await Task.WhenAll(groups.Select(async g => (g, await GetGroupMembers(g)))))
                .Where(e => e.Item2.Contains(userId))
                .Select(e => e.Item1)
                .ToHashSet();

            var combined = new List<Faxline>();
            combined.AddRange(userFaxLines);
            combined.AddRange(groupFaxlines.Where(f => groupsOfUser.Contains(f.GroupId ?? "")));

            return combined.Distinct().ToArray();
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