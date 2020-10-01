using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using NTwain;
using NTwain.Data;

namespace SipgateVirtualFax.Core
{
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
    
    public class NoDocumentScannedException : Exception
    {
        public NoDocumentScannedException(Exception cause) : base("No document scanned!", cause) { }
    }
}