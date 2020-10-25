using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using NLog;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    public partial class NewFax
    {
        private static readonly ScannerSelector DefaultScanner =
            new ScannerSelector(Properties.Resources.DefaultScanner, session => session.DefaultSource);
        private readonly Logger _logger = Logging.GetLogger("gui-newfax");
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
            model.Scanners = _scanner.GetScanners()
                .OrderBy(s => s.Name)
                .Prepend(DefaultScanner)
                .ToArray();
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

        private async void ScanButton_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (NewFaxViewModel) DataContext;
            try
            {
                await vm.ScanAndSend(_scanner);
                Close();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Scanning failed");
                MessageBox.Show(Properties.Resources.Err_ScanningFailed);
            }
        }

        private void PdfButton_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (NewFaxViewModel) DataContext;
            vm.ChoosePdfAndSend();
            Close();
        }
    }

    public class NewFaxViewModel : BaseViewModel
    {
        private Faxline[] _faxlines = new Faxline[0];
        private ScannerSelector[] _scanners = new ScannerSelector[0];
        private Faxline? _selectedFaxline;
        private string _faxNumber = "";
        private ScannerSelector? _selectedScanner;
        private readonly Logger _logger = Logging.GetLogger("gui-newfax-vm");

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

        public string? DocumentPath { get; private set; }

        public async Task ScanAndSend(Scanner scanner)
        {
            IList<string> paths;
            if (SelectedScanner == null)
            {
                _logger.Info("Scanning with default scanner...");
                paths = await scanner.ScanWithDefault();
            }
            else
            {
                _logger.Info($"Trying to scan with device '{SelectedScanner.Name}'");
                paths = await scanner.Scan(SelectedScanner.Selector);
            }

            if (paths.Count > 0)
            {
                var pdfPath = Path.ChangeExtension(paths.First(), "pdf");
                ImageToPdfConverter.Convert(paths, pdfPath);
                DocumentPath = pdfPath;
            }
            else
            {
                MessageBox.Show(Properties.Resources.NoDocumentScanned);
            }
        }

        public void ChoosePdfAndSend()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = $"{Properties.Resources.PdfDocuments} (*.pdf)|*.pdf"
            };
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            DocumentPath = openFileDialog.FileName;
        }
    }
}