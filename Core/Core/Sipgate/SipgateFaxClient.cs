using System;
using System.Diagnostics;
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

        private readonly EventLog _eventLog = Logging.CreateEventLog("sipgate-client");
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
            if (content != null)
            {
                var c = await content.ReadAsByteArrayAsync();
                File.WriteAllBytes(@"C:\Users\phill\Desktop\fax.json", c);
            }
            var url = $"{_baseUrl}{path}";
            _eventLog.Error($"Request: {method} {url}");
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

        private static async Task<TRes> TryProcessJson<TRes>(HttpResponseMessage result)
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
            var response = await SendRequestJson(method, path, body);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unsuccessful response: {response.StatusCode}");
            }
            return await TryProcessJson<TRes>(response);
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

        public Task<HistoryEntry> GetHistoryEntry(string entryId)
        {
            return SendRequest<HistoryEntry>(HttpMethod.Get, $"/history/{entryId}");
        }

        public async Task<Faxline[]> GetFaxLines()
        {
            var response = await SendRequest<FaxlinesResponse>(HttpMethod.Get, "/groupfaxlines");
            return response.Items;
        }
    }
}