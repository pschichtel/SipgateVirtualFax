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

namespace SipgateVirtualFax.Core;

public class Scanner
{
    private readonly Logger _logger = Logging.GetLogger("scanner");
    private readonly TWIdentity _appId;
    public bool ShowUi { get; set; } = true;
    public IntPtr ParentWindow { get; set; } = IntPtr.Zero;
    public string ScanBasePath { get; set; } = Path.GetTempPath();
    public TimeSpan SourceEnabledTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public Scanner()
    {
        _appId = TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly());
        PlatformInfo.Current.PreferNewDSM = false;
    }

    public IList<ScannerSelector> GetScanners()
    {
        var session = CreateSession();
        try
        {
            return session.GetSources().Select(ScannerSelector.SelectById).ToArray();
        }
        finally
        {
            session.Close();
        }
    }

    public Task<IList<string>> ScanWithDefault()
    {
        return DoScan(session => session.DefaultSource);
    }

    public Task<IList<string>> Scan(Func<ITwainSession, IDataSource?> sourceFilter)
    {
        return DoScan(sourceFilter);
    }

    private async Task<IList<string>> DoScan(Func<ITwainSession, IDataSource?> sourceSelector)
    {
        var session = CreateSession();

        foreach (var availableSource in session.GetSources())
        {
            _logger.Info($"Available scanner: {availableSource.Name}");
        }
        var defaultSource = session.DefaultSource;
        if (defaultSource != null)
        {
            _logger.Info($"Default scanner: {defaultSource.Name}");
        }

        IDataSource? source = sourceSelector(session);
        if (source == null)
        {
            session.Close();
            _logger.Error("Source not found!");
            throw new ScanningException(ScanningError.FailedToCreateSession);
        }

        _logger.Info($"Scanner being used: {source.Name}");

        if (source.Open() != ReturnCode.Success)
        {
            session.Close();
            _logger.Error("Failed to open source!");
            throw new ScanningException(ScanningError.FailedToOpenSource);
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
                _logger.Error("Failed to close source!");
                throw new ScanningException(ScanningError.FailedToCloseSource);
            }

            _logger.Info("Closing session...");
            if (session.Close() != ReturnCode.Success)
            {
                _logger.Error("Failed to close session!");
                throw new ScanningException(ScanningError.FailedToCloseSession);
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
        session.StateChanged += (_, _) => { LogState(session); };

        var returnCode = loopHook == null ? session.Open() : session.Open(loopHook);

        if (returnCode != ReturnCode.Success)
        {
            _logger.Error($"Failed to open session: {returnCode}");
            throw new ScanningException(ScanningError.FailedToCreateSession);
        }
            
        return session;
    }

    private void LogState(ITwainSession session)
    {
        var stateName = session.StateEx switch
        {
            State.DsmUnloaded => "Pre-Session",
            State.DsmLoaded => "Source Manager Loaded",
            State.DsmOpened => "Source Manager Opened",
            State.SourceOpened => "Source Open",
            State.SourceEnabled => "Source Enabled",
            State.TransferReady => "Transfer Ready",
            State.Transferring => "Transferring",
            _ => "Invalid State"
        };
        _logger.Info($"TWAIN session state: {stateName}");
    }

    private Task<IList<string>> EnableSourceAndCollectScans(ITwainSession session, IDataSource source)
    {
        var completionSource = new TaskCompletionSource<IList<string>>();
        var scannedFiles = new ConcurrentQueue<string>();
        Exception? error = null;
            
        void ProcessData(object? o, DataTransferredEventArgs e)
        {
            _logger.Info($"Received data from source {e.DataSource.Name}");
            var img = GetTransferredImage(e);

            if (img == null)
            {
                _logger.Error("Failed to create image from transferred data!");
                completionSource.SetException(new ScanningException(ScanningError.FailedToReadScannedImage));
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
                    // needs to be a long
                    const long jpegQuality = 75L;
                    targetPath = Path.Combine(ScanBasePath, $"{fileName}.jpg");
                    var encoderParams = new EncoderParameters(1)
                    {
                        Param = {[0] = new EncoderParameter(Encoder.Quality, jpegQuality)}
                    };
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
                error = new ScanningException(ScanningError.Unknown, exception);
            }
        }

        void TransferReady(object? sender, TransferReadyEventArgs args)
        {
            _logger.Info($"EndOfJob={args.EndOfJob}");
            _logger.Info($"EndOfJobFlag={args.EndOfJobFlag}");
            _logger.Info($"PendingTransferCount={args.PendingTransferCount}");
            _logger.Info($"PendingImageInfo={args.PendingImageInfo}");
        }

        void TransferError(object? sender, TransferErrorEventArgs args)
        {
            _logger.Error(args.Exception, "TransferError");
        }

        void SourceDisabled(object? sender, EventArgs e)
        {
            CompleteScan();
        }

        void InitiallyEnabled(object? sender, EventArgs args)
        {
            if (session.StateEx == State.SourceEnabled)
            {
                _logger.Info("Source initially enabled...");
                session.StateChanged -= InitiallyEnabled;
                session.StateChanged += ReturnedToEnabled;
            }
        }

        async void ReturnedToEnabled(object? sender, EventArgs args)
        {
            // SourceDisabled apparently is unreliable, so we track StateChanged instead
            if (session.StateEx == State.SourceEnabled)
            {
                _logger.Info("Returned back to SourceEnabled, wait a bit and then finish up.");
                await Task.Delay(SourceEnabledTimeout);
                _logger.Info("Timed out, finishing up");
                CompleteScan();
            }
        }

        void CompleteScan()
        {
            if (completionSource.Task.IsCompleted)
            {
                _logger.Warn("Scan was already completed, apparently the scan properly finished while waiting for the SourceEnabled timeout");
                return;
            }
                
            session.DataTransferred -= ProcessData;
            session.TransferError -= TransferError;
            session.TransferReady -= TransferReady;
            session.SourceDisabled -= SourceDisabled;
            session.StateChanged -= InitiallyEnabled;
            session.StateChanged -= ReturnedToEnabled;

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

        // if (ShouldEnableFeeder(source))
        // {
        //     _logger.Info("Scanner capabilities suggest, that we should use the ADF, trying to enable...");
        //     if (EnableFeeder(source))
        //     {
        //         _logger.Warn("Enabling the ADF failed.");
        //     }
        // }
            
        SetQualityPreferences(source);

        session.TransferReady += TransferReady;
        session.DataTransferred += ProcessData;
        session.TransferError += TransferError;
        session.SourceDisabled += SourceDisabled;
        session.StateChanged += InitiallyEnabled;

        var uiMode = ShowUi ? SourceEnableMode.ShowUI : SourceEnableMode.NoUI;
        _logger.Info("Enabling source...");
        var result = source.Enable(uiMode, true, ParentWindow);
        if (result != ReturnCode.Success)
        {
            _logger.Error($"Failed to enable data source: {result}");
            throw new ScanningException(ScanningError.FailedToEnableSource);
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

    private void SetQualityPreferences(IDataSource source)
    {
        if (SetIfPossible(source.Capabilities.ICapPixelType, PixelType.BlackWhite) != ReturnCode.Success)
        {
            _logger.Info("Failed to set quality preference: black and white scan");
        }
        if (SetIfPossible(source.Capabilities.ICapXResolution, 150) != ReturnCode.Success)
        {
            _logger.Info("Failed to set quality preference: 150 dpi (x axis)");
        }
        if (SetIfPossible(source.Capabilities.ICapYResolution, 150) != ReturnCode.Success)
        {
            _logger.Info("Failed to set quality preference: 150 dpi (y axis)");
        }
    }

    private static ReturnCode? SetIfPossible<T>(ICapWrapper<T> cap, T value)
    {
        if (cap.IsSupported && cap.CanSet)
        {
            return cap.SetValue(value);
        }

        return null;
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

public enum ScanningError
{
    FailedToCreateSession,
    Unknown,
    FailedToReadScannedImage,
    FailedToEnableSource,
    FailedToCloseSource,
    FailedToCloseSession,
    FailedToOpenSource
}

public class ScanningException : Exception
{
    public ScanningError Error { get; }

    public ScanningException(ScanningError error, Exception? cause = null) : base($"Scanning failed: {error}", cause)
    {
        Error = error;
    }
}