using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NTwain;
using NTwain.Data;

namespace SipgateVirtualFax.Core
{
    public class Scanner
    {
        private readonly Logger _logger = Logging.GetLogger("scanner");
        private readonly TWIdentity _appId;

        public Scanner()
        {
            _appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
            PlatformInfo.Current.PreferNewDSM = false;
        }

        private void PrintState(TwainSession session)
        {
            _logger.Info($"State changed: {session.GetState()}");
        }

        private TwainSession CreateSession()
        {
            return new TwainSession(_appId) {StopOnTransferError = true};
        }

        public IList<string> ScanWithDefault(bool showScannerUi)
        {
            return DoScan(null, showScannerUi);
        }

        public IList<string> Scan(Func<IDataSource, bool> sourceFilter, bool showScannerUi)
        {

            return DoScan(sourceFilter, showScannerUi);
        }

        private IList<string> DoScan(Func<IDataSource, bool>? sourceFilter, bool showScannerUi)
        {
            var session = CreateSession();
            IDataSource? source = null;
            try
            {
                var programFinishedMonitor = new object();

                PrintState(session);

                IList<string> output = new List<string>();
                Exception? cause = null;

                session.StateChanged += (sender, eventArgs) => { PrintState(session); };
                session.TransferError += (sender, eventArgs) =>
                {
                    _logger.Error(eventArgs.Exception, $"Transfer error: {eventArgs.ReturnCode}");
                    cause = eventArgs.Exception;
                    TriggerMonitor(ref programFinishedMonitor);
                };
                session.SourceDisabled += (sender, eventArgs) =>
                {
                    session.CurrentSource.Close();
                    _logger.Info($"Source disabled!");
                    TriggerMonitor(ref programFinishedMonitor);
                };
                session.TransferReady += (sender, e) =>
                {
                    _logger.Info($"Transfer ready from source {e.DataSource.Name}");
                    _logger.Info($"End? -> {e.EndOfJob} -> {e.EndOfJobFlag}");
                    PrintCapabilities(e.DataSource);
                    //e.DataSource.Capabilities.CapFeederEnabled.SetValue(BoolType.True);
                };

                session.DataTransferred += (sender, e) =>
                {
                    ProcessReceivedData(e, ref programFinishedMonitor, ref output, ref cause);
                };

                if (session.Open() != ReturnCode.Success)
                {
                    throw new Exception("Failed to open TWAIN session!");
                }

                if (sourceFilter != null)
                {
                    source = session.GetSources()
                        .First(sourceFilter);
                }
                else
                {
                    source = session.DefaultSource;
                }
                
                if (source == null)
                {
                    throw new Exception("Source not found!");
                }
                
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

                _logger.Info($"Enabled device {source.Name} successfully!");
                return output;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to scan!");
                throw;
            }
            finally
            {
                _logger.Info("Closing...");
                if (source != null && source.IsOpen)
                {
                    source.Close();
                }

                if (session.IsSourceEnabled && session.CurrentSource.IsOpen)
                {
                    session.CurrentSource.Close();
                }
                
                session.Close();
            }
        }

        private void SetCapability<T>(IDataSource source, Func<ICapabilities, ICapWrapper<T>> cap, T value)
        {
            cap(source.Capabilities).SetValue(value);
            PrintCapabilities(source);
        }

        private void PrintCapabilities(IDataSource source)
        {
            static string SupportedActions<T>(IReadOnlyCapWrapper<T> cap)
            {
                return
                    $"GET={cap.CanGet} SET={cap.CanSet} GET_DEFAULT={cap.CanGetDefault} GET_CURRENT={cap.CanGetCurrent}";
            }

            void RoCap<T>(string name, IReadOnlyCapWrapper<T> cap)
            {
                _logger.Info($"Capability (read-only): {name}={cap.GetCurrent()} ({SupportedActions(cap)})");
            }

            void RwCap<T>(string name, ICapWrapper<T> cap)
            {
                _logger.Info($"Capability: {name}={cap.GetCurrent()} ({SupportedActions(cap)})");
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

        private void ProcessReceivedData(DataTransferredEventArgs e, ref object programFinishedMonitor,
            ref IList<string> output, ref Exception? cause)
        {
            Image? img = GetTransferredImage(e);

            if (img != null)
            {
                var date = DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff");
                var fileName = $"fax-scan-{date}.png";

                string targetPath = Path.Combine(Path.GetTempPath(), fileName);
                try
                {
                    img.Save(targetPath, ImageFormat.Png);
                    _logger.Info($"Saved scan at {targetPath} for transmission!");
                    output.Add(targetPath);
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Failed to write image to disk!");
                    cause = exception;
                }

                TriggerMonitor(ref programFinishedMonitor);
            }
            else
            {
                _logger.Error("Failed to create image from transferred data!");
            }

            _logger.Info($"Received data from source {e.DataSource.Name}");
        }

        private static Image? GetTransferredImage(DataTransferredEventArgs e)
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
        public NoDocumentScannedException(Exception? cause) : base("No document scanned!", cause)
        {
        }
    }
}