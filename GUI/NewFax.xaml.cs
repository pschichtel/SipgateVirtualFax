using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CredentialManagement;
using Microsoft.Win32;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    public partial class NewFax : UserControl
    {
        public NewFaxViewModel ViewModel { get; }
        public NewFax()
        {
            ViewModel = new NewFaxViewModel();
            DataContext = ViewModel;
            InitializeComponent();
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // we manually fire the bindings so we get the validation initially
            FaxNumber.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        private void ScanButton_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (NewFaxViewModel) DataContext;
            vm.ScanAndSend();
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
        private readonly SipgateFaxClient _faxClient;

        public NewFaxViewModel()
        {
            var credential = LookupCredential();
            _faxClient = new SipgateFaxClient(credential.Username, credential.Password);
        }

        public IEnumerable<Faxline> FaxLines
        {
            get
            {
                if (_faxLines == null)
                {
                    _faxLines = _faxClient.GetFaxLines().Result;
                }
                return _faxLines;
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

        public void Initialize()
        {
            SelectedFaxLine = FaxLines.FirstOrDefault();
            FaxNumber = string.Empty;
        }
        
        private static Credential LookupCredential()
        {
            var credential = new Credential { Target = "sipgate-fax" };
            if (!credential.Load())
            {
                MessageBox.Show("Failed to load sipgate credentials!");
                throw new Exception("Missing credential!");
            }

            return credential;
        }

        public void ScanAndSend()
        {
            var scannedPdfPath = Scan();
            if (scannedPdfPath != null)
            {
                Send(scannedPdfPath);
            }
        }

        public void ChoosePdfAndSend()
        {
            var existingPdfPath = ChoosePdf();
            if (existingPdfPath != null)
            {
                Send(existingPdfPath);
            }
        }

        private string? ChoosePdf()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PDF Documents (*.pdf)|*.pdf"
            };
            if (openFileDialog.ShowDialog() != true)
            {
                return null;
            }

            return openFileDialog.FileName;
        }

        private string? Scan()
        {
            try
            {
                var paths = Scanner.Scan(true);
                if (paths.Count > 0)
                {
                    var pdfPath = Path.ChangeExtension(paths.First(), "pdf");
                    ImageToPdfConverter.Convert(paths, pdfPath);
                    return pdfPath;
                }
            }
            catch (NoDocumentScannedException)
            {
                MessageBox.Show("No document scanned!");
            }

            return null;
        }

        private void Send(string pdfPath)
        {
            try
            {
                var faxLineId = _selectedFaxLine?.Id;
                if (faxLineId == null)
                {
                    MessageBox.Show("No fax line selected!");
                    return;
                }

                _faxClient.SendFax(faxLineId, _faxNumber, pdfPath);
                MessageBox.Show("Successfully send the fax!");
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to send fax!");
                MessageBox.Show(e.Message);
            }
        }
    }
}