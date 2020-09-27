using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
    enum TwainState
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
                if (session.Open() == ReturnCode.Success)
                {
                    DataSource source = session.DefaultSource;
                    if (source.Open() == ReturnCode.Success)
                    {
                        var uiMode = showScannerUi ? SourceEnableMode.ShowUI : SourceEnableMode.NoUI;
                        PrintCapabilities(source);
                        if (source.Enable(uiMode, false, IntPtr.Zero) == ReturnCode.Success)
                        {
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
                        else
                        {
                            throw new Exception("Failed to enable data source!");
                        }
                    }
                    else
                    {
                        throw new Exception("Failed to open data source!");
                    }
                }
                else
                {
                    throw new Exception("Failed to open TWAIN session!");
                }
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

        private static void PrintCapabilities(DataSource source)
        {
            string supportedActions<T>(IReadOnlyCapWrapper<T> cap)
            {
                return $"GET={cap.CanGet} SET={cap.CanSet} GET_DEFAULT={cap.CanGetDefault} GET_CURRENT={cap.CanGetCurrent}";
            }

            void roCap<T>(string name, IReadOnlyCapWrapper<T> cap)
            {
                Console.WriteLine($"Capability (read-only): {name}={cap.GetCurrent()} ({supportedActions(cap)})");
            }

            void rwCap<T>(string name, ICapWrapper<T> cap)
            {
                Console.WriteLine($"Capability: {name}={cap.GetCurrent()} ({supportedActions(cap)})");
            }
            
            roCap("PaperDetectable", source.Capabilities.CapPaperDetectable);
            roCap("FeederLoaded", source.Capabilities.CapFeederLoaded);
            rwCap("AutoFeed", source.Capabilities.CapAutoFeed);
            rwCap("AutomaticCapture", source.Capabilities.CapAutomaticCapture);
            rwCap("AutoScan", source.Capabilities.CapAutoScan);
            rwCap("FeederEnabled", source.Capabilities.CapFeederEnabled);
            rwCap("XferCount", source.Capabilities.CapXferCount);
            roCap("UIControllable", source.Capabilities.CapUIControllable);
        }

        private static void ProcessReceivedData(DataTransferredEventArgs e, ref object programFinishedMonitor, ref IList<string> output, ref Exception cause)
        {
            Image img = null;
            if (e.NativeData != IntPtr.Zero)
            {
                var stream = e.GetNativeImageStream();
                if (stream != null)
                {
                    img = Image.FromStream(stream);
                }
            }
            else if (!string.IsNullOrEmpty(e.FileDataPath))
            {
                img = new Bitmap(e.FileDataPath);
            }

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
            PdfWriter.GetInstance(document, new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None));
            document.Open();

            var image = iTextSharp.text.Image.GetInstance(new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            document.Add(image);
        } 
    }

    class Program
    {
        static async Task Main(string[] args)
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
        public NoDocumentScannedException(Exception cause) : base("No document scanned!", cause)
        {
        }
    }

    public static class Sipgate
    {
        public static void SendFax(string faxline, string recipient, string pdfPath, Credential credential)
        {
            var fileName = Path.GetFileName(pdfPath);
            var documentContent = Convert.ToBase64String(File.ReadAllBytes(pdfPath));
            var client = new HttpClient();
            var requestContent = JsonConvert.SerializeObject(new SendFaxRequest
            {
                FaxlineId = faxline,
                Recipient = recipient,
                Filename = fileName,
                Content = documentContent
            });
            HttpRequestHeaders headers;
            var message = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://api.sipgate.com/v2/sessions/fax"),
                Headers =
                {
                    {"Authorization", CredentialToBasicAuth(credential)}
                },
                Content = new StringContent(requestContent, Encoding.UTF8, "application/json")
            };
            
            File.WriteAllBytes($"{pdfPath}.request.json", Encoding.UTF8.GetBytes(requestContent));

            HttpResponseMessage response = client.SendAsync(message).Result;
            string responseContent = response.Content.ReadAsStringAsync().Result;
            File.WriteAllBytes($"{pdfPath}.response.txt", Encoding.UTF8.GetBytes(responseContent));
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception(response.Content.ReadAsStringAsync().Result);
            }
        }

        public static IEnumerable<Faxline> GetFaxLinesSync(Credential credential)
        {
            return GetFaxLines(credential).Result;
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
                    {"Authorization", CredentialToBasicAuth(credential)}
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
}