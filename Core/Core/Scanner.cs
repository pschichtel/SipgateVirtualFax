using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public Task<IList<string>> ScanWithDefault(bool showScannerUi)
        {
            return DoScan(null, showScannerUi);
        }

        public Task<IList<string>> Scan(Func<IDataSource, bool> sourceFilter, bool showScannerUi)
        {
            return DoScan(sourceFilter, showScannerUi);
        }

        private async Task<IList<string>> DoScan(Func<IDataSource, bool>? sourceFilter, bool showScannerUi)
        {
            var session = CreateSession();

            IDataSource? source;
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
                session.Close();
                throw new Exception("Source not found!");
            }

            if (source.Open() != ReturnCode.Success)
            {
                session.Close();
                throw new Exception("Failed to open source!");
            }
            
            _logger.Info($"Enabled source {source.Name} successfully!");
            try
            {
                return await EnableSourceAndCollectScans(session, source, showScannerUi);
            }
            finally
            {
                _logger.Info("Closing source...");
                if (source.Close() != ReturnCode.Success)
                {
                    session.Close();
                    throw new Exception("Failed to close source!");
                }

                _logger.Info("Closing session...");
                if (session.Close() != ReturnCode.Success)
                {
                    throw new Exception("Failed to close session!");
                }
            }

        }

        private ITwainSession CreateSession(MessageLoopHook? loopHook = null)
        {
            var session = new TwainSession(_appId)
            {
                StopOnTransferError = true
            };
            
            LogState(session);
            session.StateChanged += (s, e) => { LogState(session); };

            var returnCode = loopHook == null ? session.Open() : session.Open(loopHook);

            if (returnCode != ReturnCode.Success)
            {
                throw new Exception($"Failed to open session: {session}");
            }
            
            return session;
        }

        private void LogState(ITwainSession session)
        {
            var stateName = session.State switch
            {
                1 => "Pre-Session",
                2 => "Source Manager Loaded",
                3 => "Source Manager Opened",
                4 => "Source Open",
                5 => "Source Enabled",
                6 => "Transfer Ready",
                7 => "Transferring",
                _ => "Unknown State"
            };
            _logger.Info($"TWAIN session state: {stateName}");
        }

        enum ScanState
        {
            Start,
            Ready,
            Received,
        }

        private Task<IList<string>> EnableSourceAndCollectScans(ITwainSession session, IDataSource source, bool showScannerUi)
        {
            TaskCompletionSource<IList<string>> completionSource = new TaskCompletionSource<IList<string>>();
            var uiMode = showScannerUi ? SourceEnableMode.ShowUI : SourceEnableMode.NoUI;
            ConcurrentQueue<string> scannedFiles = new ConcurrentQueue<string>();

            var state = ScanState.Start;
            
            void TransferReady(object sender, TransferReadyEventArgs e)
            {
                _logger.Info($"Transfer ready from source {e.DataSource.Name}");
                _logger.Info($"End? -> {e.EndOfJob} -> {e.EndOfJobFlag}");
                //e.DataSource.Capabilities.CapFeederEnabled.SetValue(BoolType.True);
                switch (state)
                {
                    case ScanState.Start:
                        _logger.Info("Initially ready!");
                        PrintCapabilities(e.DataSource);
                        state = ScanState.Ready;
                        break;
                    case ScanState.Received:
                        _logger.Info("Ready after receiving a scan!");
                        break;
                    default:
                        Fail(new Exception($"Illegal state in TransferReady: {state}"));
                        break;
                }
            }

            void ProcessData(object o, DataTransferredEventArgs e)
            {
                switch (state)
                {
                    case ScanState.Ready:
                        _logger.Info($"Received data from source {e.DataSource.Name}");
                        var img = GetTransferredImage(e);

                        if (img == null)
                        {
                            _logger.Error("Failed to create image from transferred data!");
                            completionSource.SetException(new Exception("Failed to create an image from data"));
                            return;
                        }

                        var date = DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff");
                        var fileName = $"fax-scan-{date}.png";

                        string targetPath = Path.Combine(Path.GetTempPath(), fileName);
                        try
                        {
                            img.Save(targetPath, ImageFormat.Png);
                            _logger.Info($"Saved scan at {targetPath} for transmission!");
                            scannedFiles.Enqueue(targetPath);
                        }
                        catch (Exception exception)
                        {
                            _logger.Error(exception, "Failed to write image to disk!");
                            Fail(exception);
                        }
                        break;
                    default:
                        Fail(new Exception($"Illegal state in DataTransferred: {state}"));
                        break;
                }

            }

            void TransferError(object sender, TransferErrorEventArgs args)
            {
                _logger.Error(args.Exception, "TransferError");
                completionSource.SetException(args.Exception);
            }

            void SourceDisabled(object sender, EventArgs args)
            {
                switch (state)
                {
                    case ScanState.Ready:
                        Complete(scannedFiles.ToList());
                        break;
                    default:
                        Fail(new Exception($"Illegal state in DataTransferred: {state}"));
                        break;
                }
            }

            void Cleanup()
            {
                session.TransferReady -= TransferReady;
                session.DataTransferred -= ProcessData;
                session.TransferError -= TransferError;
                session.SourceDisabled -= SourceDisabled;
            }

            void Complete(IList<string> result)
            {
                Cleanup();
                completionSource.SetResult(result);
            }

            void Fail(Exception e)
            {
                Cleanup();
                completionSource.SetException(e);
            }
            
            // if (ShouldEnableFeeder(source))
            // {
            //     _logger.Info("Scanner capabilities suggest, that we should use the ADF, trying to enable...");
            //     if (EnableFeeder(source))
            //     {
            //         _logger.Warn("Enabling the ADF failed.");
            //     }
            // }

            session.TransferReady += TransferReady;
            session.DataTransferred += ProcessData;
            session.TransferError += TransferError;
            session.SourceDisabled += SourceDisabled;

            if (source.Enable(uiMode, false, IntPtr.Zero) != ReturnCode.Success)
            {
                throw new Exception("Failed to enable data source!");
            }

            return completionSource.Task;
        }

        private bool ShouldEnableFeeder(IDataSource source)
        {
            PrintCapabilities(source);
            var canEnableFeeder= source.Capabilities.CapFeederEnabled.CanSet;
            var canGetFeederLoaded = source.Capabilities.CapFeederLoaded.CanGetCurrent;
            var canGetPaperDetectSupport = source.Capabilities.CapPaperDetectable.CanGetCurrent;

            if (canEnableFeeder && canGetFeederLoaded && canGetPaperDetectSupport)
            {
                return source.Capabilities.CapFeederLoaded.GetCurrent() == BoolType.True;
            }

            return false;
        }

        private bool EnableFeeder(IDataSource source)
        {
            return source.Capabilities.CapFeederEnabled.SetValue(BoolType.True) == ReturnCode.Success;
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
    }
}