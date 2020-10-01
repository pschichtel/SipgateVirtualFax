using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CredentialManagement;
using Newtonsoft.Json;

namespace SipgateVirtualFax.Core.Sipgate
{
    public static class Sipgate
    {
        private const string BaseUrl = "https://api.sipgate.com/v2";

        private static Task<HttpResponseMessage> SendBasicRequest(HttpMethod method, string path, HttpContent? content, Credential credential)
        {
            var client = new HttpClient();
            var message = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri($"{BaseUrl}{path}"),
                Headers =
                {
                    { "Authorization", CredentialToBasicAuth(credential) }
                },
                Content = content
            };

            return client.SendAsync(message);
        }

        private static Task<HttpResponseMessage> SendRequestJson<TReq>(HttpMethod method, string path, TReq body, Credential credential)
        {
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            return SendBasicRequest(method, path, content, credential);
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

        private static async Task<TRes> SendRequest<TRes>(HttpMethod method, string path, Credential credential)
        {
            return await TryProcessJson<TRes>(await SendBasicRequest(method, path, null, credential));
        }

        private static async Task<TRes> SendRequestWithResponse<TReq, TRes>(HttpMethod method, string path, TReq body, Credential credential)
        {
            return await TryProcessJson<TRes>(await SendRequestJson(method, path, body, credential));
        }

        private static async Task<bool> SendRequest<TReq>(HttpMethod method, string path, TReq body, Credential credential)
        {
            var result = await SendRequestJson(method, path, body, credential);
            return result.IsSuccessStatusCode;
        }

        public static string? SendFax(string faxLine, string recipient, string pdfPath, Credential credential)
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
                var response = SendRequestWithResponse<SendFaxRequest, SendFaxResponse>(HttpMethod.Post, "/sessions/fax", request, credential).Result;
                return response.SessionId;
            }
            catch (Exception e)
            {
                throw new Exception("Failed to parse the response from sipgate!", e);
            }
        }

        public static bool AttemptFaxResend(string faxId, string faxLine, Credential credential)
        {
            var request = new ResendFaxRequest
            {
                FaxId = faxId,
                FaxlineId = faxLine
            };
            return SendRequest(HttpMethod.Post, "/sessions/fax/resend", request, credential).Result;
        }

        public static IEnumerable<Faxline> GetFaxLinesSync(Credential credential)
        {
            return GetFaxLines(credential).Result;
        }

        public static HistoryEntry GetHistoryEntry(string entryId, Credential credential)
        {
            return SendRequest<HistoryEntry>(HttpMethod.Get, $"/history/{entryId}", credential).Result;
        }

        private static string CredentialToBasicAuth(Credential credential)
        {
            return $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Password}"))}";
        }

        public static async Task<IEnumerable<Faxline>> GetFaxLines(Credential credential)
        {
            var client = new HttpClient();
            var message = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://api.sipgate.com/v2/groupfaxlines"),
                Headers =
                {
                    { "Authorization", CredentialToBasicAuth(credential) }
                }
            };

            try
            {
                var result = await client.SendAsync(message);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    var body = await result.Content.ReadAsStringAsync();
                    var lines = JsonConvert.DeserializeObject<FaxlinesResponse>(body);
                    return lines.Items ?? new List<Faxline>();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return new Faxline[0];
        }
    }
}