using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CredentialManagement;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using NTwain;
using NTwain.Data;
using Image = System.Drawing.Image;

namespace SipgateVirtualFax
{
    internal enum TwainState
    {
        PreSession = 1,
        SourceManagerLoaded = 2,
        SourceManagerOpened = 3,
        SourceOpen = 4,
        SourceEnabled = 5,
        TransferReady = 6,
        Transferring = 7,
    }

    static class TwainSessionExtension
    {
        public static TwainState GetState(this TwainSession session)
        {
            return (TwainState) session.State;
        }
    }

    public static class Scanner
    {
        private static void PrintState(TwainSession session)
        {
            Console.WriteLine($"State changed: {session.GetState()}");
        }

        public static IList<string> Scan(bool showScannerUi)
        {
            PlatformInfo.Current.PreferNewDSM = false;
            var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
            var session = new TwainSession(appId) { StopOnTransferError = true };

            var programFinishedMonitor = new object();

            PrintState(session);

            IList<string> output = new List<string>();
            Exception cause = null;

            session.StateChanged += (sender, eventArgs) => { PrintState(session); };
            session.TransferError += (sender, eventArgs) =>
            {
                Console.WriteLine($"Transfer error: {eventArgs.ReturnCode} - {eventArgs.Exception}");
                cause = eventArgs.Exception;
                TriggerMonitor(ref programFinishedMonitor);
            };
            session.SourceDisabled += (sender, eventArgs) =>
            {
                session.CurrentSource.Close();
                Console.WriteLine($"Source disabled!");
                TriggerMonitor(ref programFinishedMonitor);
            };
            session.TransferReady += (sender, e) =>
            {
                Console.WriteLine($"Transfer ready from source {e.DataSource.Name}");
                Console.WriteLine($"End? -> {e.EndOfJob} -> {e.EndOfJobFlag}");
                PrintCapabilities(e.DataSource);
                //e.DataSource.Capabilities.CapFeederEnabled.SetValue(BoolType.True);
            };

            session.DataTransferred += (sender, e) => { ProcessReceivedData(e, ref programFinishedMonitor, ref output, ref cause); };

            try
            {
                if (session.Open() != ReturnCode.Success)
                {
                    throw new Exception("Failed to open TWAIN session!");
                }

                DataSource source = session.DefaultSource;
                if (source.Open() != ReturnCode.Success)
                {
                    throw new Exception("Failed to open data source!");
                }

                var uiMode = showScannerUi ? SourceEnableMode.ShowUI : SourceEnableMode.NoUI;
                PrintCapabilities(source);
                if (source.Enable(uiMode, false, IntPtr.Zero) != ReturnCode.Success)
                {
                    throw new Exception("Failed to enable data source!");
                }

                lock (programFinishedMonitor)
                {
                    Monitor.Wait(programFinishedMonitor);
                }

                if (output.Count == 0)
                {
                    throw new NoDocumentScannedException(cause);
                }

                Console.WriteLine($"Enabled device {source.Name} successfully!");
                return output;
            }
            finally
            {
                if (session.IsDsmOpen)
                {
                    Console.WriteLine("Closing...");
                    session.Close();
                }
            }
        }

        private static void SetCapability<T>(DataSource source, Func<ICapabilities, ICapWrapper<T>> cap, T value)
        {
            cap(source.Capabilities).SetValue(value);
            PrintCapabilities(source);
        }

        private static void PrintCapabilities(IDataSource source)
        {
            static string SupportedActions<T>(IReadOnlyCapWrapper<T> cap)
            {
                return $"GET={cap.CanGet} SET={cap.CanSet} GET_DEFAULT={cap.CanGetDefault} GET_CURRENT={cap.CanGetCurrent}";
            }

            void RoCap<T>(string name, IReadOnlyCapWrapper<T> cap)
            {
                Console.WriteLine($"Capability (read-only): {name}={cap.GetCurrent()} ({SupportedActions(cap)})");
            }

            void RwCap<T>(string name, ICapWrapper<T> cap)
            {
                Console.WriteLine($"Capability: {name}={cap.GetCurrent()} ({SupportedActions(cap)})");
            }

            RoCap("PaperDetectable", source.Capabilities.CapPaperDetectable);
            RoCap("FeederLoaded", source.Capabilities.CapFeederLoaded);
            RwCap("AutoFeed", source.Capabilities.CapAutoFeed);
            RwCap("AutomaticCapture", source.Capabilities.CapAutomaticCapture);
            RwCap("AutoScan", source.Capabilities.CapAutoScan);
            RwCap("FeederEnabled", source.Capabilities.CapFeederEnabled);
            RwCap("XferCount", source.Capabilities.CapXferCount);
            RoCap("UIControllable", source.Capabilities.CapUIControllable);
        }

        private static void ProcessReceivedData(DataTransferredEventArgs e, ref object programFinishedMonitor, ref IList<string> output, ref Exception cause)
        {
            Image img = GetTransferredImage(e);

            if (img != null)
            {
                var date = DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff");
                var fileName = $"fax-scan-{date}.png";

                string targetPath = Path.Combine(Path.GetTempPath(), fileName);
                try
                {
                    img.Save(targetPath, ImageFormat.Png);
                    Console.WriteLine($"Saved scan at {targetPath} for transmission!");
                    output.Add(targetPath);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Failed to write image to disk! - {exception}");
                    cause = exception;
                }

                TriggerMonitor(ref programFinishedMonitor);
            }
            else
            {
                Console.WriteLine("Failed to create image from transferred data!");
            }

            Console.WriteLine($"Received data from source {e.DataSource.Name}");
        }

        private static Image GetTransferredImage(DataTransferredEventArgs e)
        {
            if (e.NativeData != IntPtr.Zero)
            {
                var stream = e.GetNativeImageStream();
                if (stream != null)
                {
                    return Image.FromStream(stream);
                }
            }
            else if (!string.IsNullOrEmpty(e.FileDataPath))
            {
                return new Bitmap(e.FileDataPath);
            }

            return null;
        }

        static void TriggerMonitor(ref object monitor)
        {
            lock (monitor)
            {
                Monitor.PulseAll(monitor);
            }
        }
    }

    public static class ImageToPdfConverter
    {
        public static void Convert(string imagePath, string targetPath)
        {
            using var document = new Document();
            document.SetMargins(0, 0, 0, 0);
            PdfWriter.GetInstance(document, new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None));
            document.Open();

            var image = iTextSharp.text.Image.GetInstance(new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            document.Add(image);
        }
    }

    class Program
    {
        public static void Main(string[] args)
        {
            var pages = Scanner.Scan(false);
            var firstPage = pages.First();
            var targetPath = $"{firstPage}.pdf";
            ImageToPdfConverter.Convert(firstPage, targetPath);
            Console.WriteLine(firstPage);
            Console.WriteLine(targetPath);
        }
    }

    public class NoDocumentScannedException : Exception
    {
        public NoDocumentScannedException(Exception cause) : base("No document scanned!", cause) { }
    }

    public static class Sipgate
    {
        private const string BaseUrl = "https://api.sipgate.com/v2";

        private static Task<HttpResponseMessage> SendBasicRequest(HttpMethod method, string path, HttpContent content, Credential credential)
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

        public static string SendFax(string faxLine, string recipient, string pdfPath, Credential credential)
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
                    return lines.Items;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return new Faxline[0];
        }
    }

    public class FaxlinesResponse
    {
        [JsonProperty("items")]
        public IEnumerable<Faxline> Items { get; set; }
    }

    public class Faxline
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("groupId")]
        public string GroupId { get; set; }

        public override string ToString()
        {
            return $"Id: {Id}, Alias: {Alias}, GroupId: {GroupId}";
        }
    }

    public class SendFaxRequest
    {
        [JsonProperty("faxlineId")]
        public string FaxlineId { get; set; }

        [JsonProperty("recipient")]
        public string Recipient { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("base64Content")]
        public string Content { get; set; }
    }

    public class SendFaxResponse
    {
        [JsonProperty("sessionId")]
        public string SessionId { get; set; }
    }

    public class ResendFaxRequest
    {
        [JsonProperty("faxId")]
        public string FaxId { get; set; }

        [JsonProperty("faxlineId")]
        public string FaxlineId { get; set; }
    }

    public class HistoryEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }
}