using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using NLog;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui;

public partial class NewFax
{
    private static readonly ScannerSelector DefaultScanner =
        new ScannerSelector(Properties.Resources.DefaultScanner, session => session.DefaultSource);
    private readonly Logger _logger = Logging.GetLogger("gui-new-fax");
    private readonly Scanner _scanner;

    public NewFax()
    {
        var window = GetWindow(this);
        IntPtr handle = IntPtr.Zero;
        if (window != null)
        {
            handle = new WindowInteropHelper(window).Handle;
        }

        _scanner = new Scanner()
        {
            ShowUi = true,
            ParentWindow = handle,
            ScanBasePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        InitializeComponent();

        var model = ((NewFaxViewModel) DataContext);
        var scanners = _scanner.GetScanners()
            .OrderBy(s => s.Name)
            .ToList();
        scanners.Insert(0, DefaultScanner);
        model.Scanners = scanners.ToArray();
        model.SelectedScanner = DefaultScanner;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // we manually fire the bindings so we get the validation initially
        FaxNumber.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void CloseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        Close();
    }

    private async void ScanDocumentAndSend(object sender, RoutedEventArgs e)
    {
        var vm = (NewFaxViewModel) DataContext;
        try
        {
            IList<string> paths;
            if (vm.SelectedScanner == null)
            {
                _logger.Info("Scanning with default scanner...");
                paths = await _scanner.ScanWithDefault();
            }
            else
            {
                _logger.Info($"Trying to scan with device '{vm.SelectedScanner.Name}'");
                paths = await _scanner.Scan(vm.SelectedScanner.Selector);
            }

            if (paths.Count > 0)
            {
                var pdfPath = Path.ChangeExtension(paths.First(), "pdf");
                ImageToPdfConverter.Convert(paths, pdfPath);
                vm.DocumentPath = pdfPath;
                Close();
            }
            else
            {
                MessageBox.Show(this, Properties.Resources.NoDocumentScanned);
            }
        }
        catch (ScanningException ex)
        {
            var message = ex.Error switch
            {
                ScanningError.FailedToCreateSession => Properties.Resources.Err_TwainFailedToCreateSession,
                ScanningError.Unknown => Properties.Resources.Err_TwainUnknown,
                ScanningError.FailedToReadScannedImage => Properties.Resources.Err_TwainFailedToReadScannedImage,
                ScanningError.FailedToEnableSource => Properties.Resources.Err_TwainFailedToEnableSource,
                ScanningError.FailedToCloseSource => Properties.Resources.Err_TwainFailedToCloseSource,
                ScanningError.FailedToCloseSession => Properties.Resources.Err_TwainFailedToCloseSession,
                ScanningError.FailedToOpenSource => Properties.Resources.Err_TwainFailedToOpenSource,
                _ => Properties.Resources.Err_TwainUnknown
            };
            MessageBox.Show(message);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scanning failed for a reason unrelated to scanning.");
            MessageBox.Show(this, Properties.Resources.Err_ScanningFailed);
        }
    }

    private void SelectPdfAndSend(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = $"{Properties.Resources.PdfDocuments} (*.pdf)|*.pdf"
        };
        if (openFileDialog.ShowDialog() ?? false)
        {
            ((NewFaxViewModel) DataContext).DocumentPath = openFileDialog.FileName;
            Close();
        }
    }
}

public class NewFaxViewModel : BaseViewModel
{
    private Faxline[] _faxlines = Array.Empty<Faxline>();
    private ScannerSelector[] _scanners = Array.Empty<ScannerSelector>();
    private Faxline? _selectedFaxline;
    private string _faxNumber = "";
    private ScannerSelector? _selectedScanner;

    public Faxline[] Faxlines
    {
        get => _faxlines;
        set
        {
            _faxlines = value;
            OnPropertyChanged(nameof(Faxlines));
            if (!_faxlines.Contains(SelectedFaxline))
            {
                SelectedFaxline = _faxlines.Length > 0 ? _faxlines[0] : null;
            }
        }
    }

    public ScannerSelector[] Scanners
    {
        get => _scanners;
        set
        {
            _scanners = value;
            OnPropertyChanged(nameof(Scanners));
        }
    }

    public Faxline? SelectedFaxline
    {
        get => _selectedFaxline;
        set
        {
            _selectedFaxline = value;
            OnPropertyChanged(nameof(SelectedFaxline));
        }
    }

    public string FaxNumber
    {
        get => _faxNumber;
        set
        {
            _faxNumber = value;
            OnPropertyChanged(nameof(FaxNumber));
        }
    }

    public ScannerSelector? SelectedScanner
    {
        get => _selectedScanner;
        set
        {
            _selectedScanner = value;
            OnPropertyChanged(nameof(SelectedScanner));
        }
    }

    public string? DocumentPath { get; internal set; }
}