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
    public partial class NewFax : UserControl
    {
        public NewFaxViewModel ViewModel { get; }
        public NewFax()
        {
            var window = Window.GetWindow(this);
            IntPtr handle = IntPtr.Zero;
            if (window != null)
            {
                handle = new WindowInteropHelper(window).Handle;
            }
            ViewModel = new NewFaxViewModel(handle);
            DataContext = ViewModel;
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
            await vm.ScanAndSend();
            Close();
        }

        private void PdfButton_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (NewFaxViewModel) DataContext;
            vm.ChoosePdfAndSend();
            Close();
        }

        private void Close()
        {
            Window.GetWindow(this)?.Close();
        }
    }

    public class NewFaxViewModel : BaseViewModel
    {
        private IEnumerable<Faxline>? _faxLines;
        private Faxline? _selectedFaxLine;
        private string _faxNumber = "";
        private readonly Scanner _scanner;
        private readonly Logger _logger = Logging.GetLogger("gui-newfax-vm");

        public NewFaxViewModel(IntPtr parentWindowHandle)
        {
            _scanner = new Scanner()
            {
                ShowUi = true,
                ParentWindow = parentWindowHandle
            };
        }

        public IEnumerable<Faxline>? FaxLines
        {
            get => _faxLines;
            set
            {
                _faxLines = value;
                OnPropertyChanged(nameof(FaxLines));
            }
        }

        public Faxline? SelectedFaxLine
        {
            get => _selectedFaxLine;
            set
            {
                _selectedFaxLine = value;
                OnPropertyChanged(nameof(SelectedFaxLine));
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

        public void Initialize(Faxline[] faxlines)
        {
            SelectedFaxLine = faxlines.FirstOrDefault();
            FaxLines = faxlines;
            FaxNumber = string.Empty;
            DocumentPath = null;
        }

        public async Task ScanAndSend()
        {
            try
            {
                var paths = await _scanner.ScanWithDefault();
                if (paths.Count > 0)
                {
                    var pdfPath = Path.ChangeExtension(paths.First(), "pdf");
                    ImageToPdfConverter.Convert(paths, pdfPath);
                    DocumentPath = pdfPath;
                }
                else
                {
                    MessageBox.Show("No document scanned!");
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Scanning failed");
                MessageBox.Show("Scanning failed!");
            }
        }

        public void ChoosePdfAndSend()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf"
            };
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            DocumentPath = openFileDialog.FileName;
        }
    }
}