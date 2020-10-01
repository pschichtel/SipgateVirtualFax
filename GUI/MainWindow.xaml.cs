using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CredentialManagement;
using Microsoft.Win32;
using PhoneNumbers;
using SipgateVirtualFax.Core;

namespace SipGateVirtualFaxGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // we manually fire the bindings so we get the validation initially
            FaxNumber.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
        
        private void ScanButton_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (ViewModel) DataContext;
            vm.ScanAndSend();
        }

        private void PdfButton_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (ViewModel) DataContext;
            vm.ChoosePdfAndSend();
        }
    }

    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ViewModel : BaseViewModel
    {
        private Faxline? _selectedFaxLine;
        private string _faxNumber = "";

        public IEnumerable<Faxline> FaxLines => Sipgate.GetFaxLinesSync(LookupCredential());

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
            var credential = LookupCredential();
            try
            {
                var faxLineId = _selectedFaxLine?.Id;
                if (faxLineId == null)
                {
                    MessageBox.Show("No fax line selected!");
                    return;
                }
                Sipgate.SendFax(faxLineId, _faxNumber, pdfPath, credential);
                MessageBox.Show("Successfully send the fax!");
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to send fax!");
                MessageBox.Show(e.Message);
            }
        }
    }

    public class PhoneNumberValidation : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (!(value is string phoneNumber))
            {
                return new ValidationResult(false, "String expected!");
            }
            
            var phoneNumberUtil = PhoneNumberUtil.GetInstance();
            try
            {
                phoneNumberUtil.Parse(phoneNumber, "DE");
                return ValidationResult.ValidResult;
            }
            catch (NumberParseException e)
            {
                return new ValidationResult(false, $"Not a valid phone number! {e.Message}");
            }
        }
    }

    public class BoolInverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value, targetType, parameter, culture);
        }
    }
}