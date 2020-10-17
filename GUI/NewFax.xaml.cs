using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using NLog;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    public partial class NewFax
    {
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
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // we manually fire the bindings so we get the validation initially
            FaxNumber.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        private async void ScanButton_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (NewFaxViewModel) DataContext;
            await vm.ScanAndSend(_scanner);
            Close();
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
        private Faxline? _selectedFaxline;
        private string _faxNumber = "";
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

        public string? DocumentPath { get; private set; }

        public async Task ScanAndSend(Scanner scanner)
        {
            try
            {
                var device = Environment.GetEnvironmentVariable("SIPGATE_FAX_SCANNER");
                IList<string> paths;
                if (device != null)
                {
                    _logger.Info($"Trying to scan with device '{device}'");
                    paths = await scanner.Scan(source => source.Name.Equals(device, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    _logger.Info("Scanning with default scanner...");
                    paths = await scanner.ScanWithDefault();
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
            catch (Exception e)
            {
                _logger.Error(e, "Scanning failed");
                MessageBox.Show(Properties.Resources.Err_ScanningFailed);
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