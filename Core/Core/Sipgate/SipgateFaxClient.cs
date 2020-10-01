using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SipgateVirtualFax.Core.Sipgate
{
    public class SipgateFaxClient
    {
        private const string DefaultBaseUrl = "https://api.sipgate.com/v2";

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

        private Task<HttpResponseMessage> SendBasicRequest(HttpMethod method, string path, HttpContent? content)
        {
            var message = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri($"{_baseUrl}{path}"),
                Headers =
                {
                    {"Authorization", _basicAuth}
                },
                Content = content
            };

            return _client.SendAsync(message);
        }

        private Task<HttpResponseMessage> SendRequestJson<TReq>(HttpMethod method, string path, TReq body)
        {
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            return SendBasicRequest(method, path, content);
        }

        private async Task<TRes> TryProcessJson<TRes>(HttpResponseMessage result)
        {
            var responseContent = await result.Content.ReadAsStringAsync();
            if (result.IsSuccessStatusCode)
            {
                return JsonConvert.DeserializeObject<TRes>(responseContent);
            }

            throw new Exception($"Result: status={result.StatusCode} content={responseContent}");
        }

        private async Task<TRes> SendRequest<TRes>(HttpMethod method, string path)
        {
            return await TryProcessJson<TRes>(await SendBasicRequest(method, path, null));
        }

        private async Task<TRes> SendRequestWithResponse<TReq, TRes>(HttpMethod method, string path, TReq body)
        {
            return await TryProcessJson<TRes>(await SendRequestJson(method, path, body));
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
            try
            {
                var response =
                    await SendRequestWithResponse<SendFaxRequest, SendFaxResponse>(HttpMethod.Post, "/sessions/fax",
                        request);
                return response.SessionId;
            }
            catch (Exception e)
            {
                throw new Exception("Failed to parse the response from sipgate!", e);
            }
        }

        public Task<bool> AttemptFaxResend(string faxId, string faxlineId)
        {
            var request = new ResendFaxRequest(faxId, faxlineId);
            return SendRequest(HttpMethod.Post, "/sessions/fax/resend", request);
        }

        public HistoryEntry GetHistoryEntry(string entryId)
        {
            return SendRequest<HistoryEntry>(HttpMethod.Get, $"/history/{entryId}").Result;
        }

        public async Task<Faxline[]> GetFaxLines()
        {
            var response = await SendRequest<FaxlinesResponse>(HttpMethod.Get, "/groupfaxlines");
            return response.Items;
        }
    }
}