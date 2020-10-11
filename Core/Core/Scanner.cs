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
        private const int PreSession = 1;
        private const int SourceManagerLoaded = 2;
        private const int SourceManagerOpened = 3;
        private const int SourceOpen = 4;
        private const int SourceEnabled = 5;
        private const int TransferReady = 6;
        private const int Transferring = 7;
        
        private readonly Logger _logger = Logging.GetLogger("scanner");
        private readonly TWIdentity _appId;
        public bool ShowUi { get; set; } = true;
        public IntPtr ParentWindow { get; set; } = IntPtr.Zero;
        public string ScanBasePath { get; set; } = Path.GetTempPath();

        public Scanner()
        {
            _appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
            PlatformInfo.Current.PreferNewDSM = false;
        }

        public Task<IList<string>> ScanWithDefault()
        {
            return DoScan(null);
        }

        public Task<IList<string>> Scan(Func<IDataSource, bool> sourceFilter)
        {
            return DoScan(sourceFilter);
        }

        private async Task<IList<string>> DoScan(Func<IDataSource, bool>? sourceFilter)
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
                return await EnableSourceAndCollectScans(session, source);
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
                PreSession => "Pre-Session",
                SourceManagerLoaded => "Source Manager Loaded",
                SourceManagerOpened => "Source Manager Opened",
                SourceOpen => "Source Open",
                SourceEnabled => "Source Enabled",
                TransferReady => "Transfer Ready",
                Transferring => "Transferring",
                _ => "Unknown State"
            };
            _logger.Info($"TWAIN session state: {stateName}");
        }

        private Task<IList<string>> EnableSourceAndCollectScans(ITwainSession session, IDataSource source)
        {
            TaskCompletionSource<IList<string>> completionSource = new TaskCompletionSource<IList<string>>();
            ConcurrentQueue<string> scannedFiles = new ConcurrentQueue<string>();
            Exception? error = null;

            void ProcessData(object o, DataTransferredEventArgs e)
            {
                _logger.Info($"Received data from source {e.DataSource.Name}");
                var img = GetTransferredImage(e);

                if (img == null)
                {
                    _logger.Error("Failed to create image from transferred data!");
                    completionSource.SetException(new Exception("Failed to create an image from data"));
                    return;
                }

                var date = DateTime.Now.ToString("yyyy-MM-dd-HHmmss-fff");
                var fileName = $"fax-scan-{date}";
                
                try
                {
                    string targetPath;
                    var codecInfo = FindCodecForFormat(ImageFormat.Jpeg);
                    if (codecInfo != null)
                    {
                        targetPath = Path.Combine(ScanBasePath, $"{fileName}.jpg");
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 75);
                        img.Save(targetPath, codecInfo, encoderParams);
                    }
                    else
                    {
                        targetPath = Path.Combine(ScanBasePath, $"{fileName}.png");
                        img.Save(targetPath, ImageFormat.Png);
                    }
                    _logger.Info($"Saved scan at {targetPath} for transmission!");
                    scannedFiles.Enqueue(targetPath);
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Failed to write image to disk!");
                    error = exception;
                }
            }

            void TransferError(object sender, TransferErrorEventArgs args)
            {
                _logger.Error(args.Exception, "TransferError");
            }

            void SourceDisabled(object sender, EventArgs args)
            {
                Cleanup();
                if (error != null)
                {
                    completionSource.SetException(error);
                }
                else
                {
                    IList<string> files = scannedFiles.ToList();
                    completionSource.SetResult(files);
                }
            }

            void Cleanup()
            {
                session.DataTransferred -= ProcessData;
                session.TransferError -= TransferError;
                session.SourceDisabled -= SourceDisabled;
            }

            // if (ShouldEnableFeeder(source))
            // {
            //     _logger.Info("Scanner capabilities suggest, that we should use the ADF, trying to enable...");
            //     if (EnableFeeder(source))
            //     {
            //         _logger.Warn("Enabling the ADF failed.");
            //     }
            // }
            
            SetQualityPreferences(source);

            session.DataTransferred += ProcessData;
            session.TransferError += TransferError;
            session.SourceDisabled += SourceDisabled;

            var uiMode = ShowUi ? SourceEnableMode.ShowUI : SourceEnableMode.NoUI;
            _logger.Info("Enabling source...");
            var result = source.Enable(uiMode, true, ParentWindow);
            if (result != ReturnCode.Success)
            {
                throw new Exception($"Failed to enable data source: {result}");
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

        private static void SetQualityPreferences(IDataSource source)
        {
            SetIfPossible(source.Capabilities.ICapPixelType, PixelType.BlackWhite);
            SetIfPossible(source.Capabilities.ICapXResolution, 150);
            SetIfPossible(source.Capabilities.ICapYResolution, 150);
        }

        private static bool SetIfPossible<T>(ICapWrapper<T> cap, T value)
        {
            if (cap.IsSupported && cap.CanSet)
            {
                cap.SetValue(value);
                return true;
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

        private static ImageCodecInfo? FindCodecForFormat(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}