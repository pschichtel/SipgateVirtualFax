using System;
using System.Collections.Generic;
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

        public SipgateFaxClient(string username, string password) : this(DefaultBaseUrl, username, password)
        {
        }

        public SipgateFaxClient(string baseUrl, string username, string password)
        {
            _baseUrl = baseUrl;
            _basicAuth = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))}";
        }

        private Task<HttpResponseMessage> SendBasicRequest(HttpMethod method, string path, HttpContent? content)
        {
            var client = new HttpClient();
            var message = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri($"{_baseUrl}{path}"),
                Headers =
                {
                    { "Authorization", _basicAuth }
                },
                Content = content
            };

            return client.SendAsync(message);
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

        public string? SendFax(string faxLine, string recipient, string pdfPath)
        {
            var request = new SendFaxRequest
            {
                FaxlineId = faxLine,
                Recipient = recipient,
                Filename = Path.GetFileName(pdfPath),
                Content = Convert.ToBase64String(File.ReadAllBytes(pdfPath))
            };
            try
            {
                var response = SendRequestWithResponse<SendFaxRequest, SendFaxResponse>(HttpMethod.Post, "/sessions/fax", request).Result;
                return response.SessionId;
            }
            catch (Exception e)
            {
                throw new Exception("Failed to parse the response from sipgate!", e);
            }
        }

        public bool AttemptFaxResend(string faxId, string faxLine)
        {
            var request = new ResendFaxRequest
            {
                FaxId = faxId,
                FaxlineId = faxLine
            };
            return SendRequest(HttpMethod.Post, "/sessions/fax/resend", request).Result;
        }

        public HistoryEntry GetHistoryEntry(string entryId)
        {
            return SendRequest<HistoryEntry>(HttpMethod.Get, $"/history/{entryId}").Result;
        }

        public IEnumerable<Faxline> GetFaxLinesSync()
        {
            return GetFaxLines().Result;
        }
        
        public Task<IEnumerable<Faxline>> GetFaxLines()
        {
            return SendRequest<IEnumerable<Faxline>>(HttpMethod.Get, "/groupfaxlines");
        }
    }
}