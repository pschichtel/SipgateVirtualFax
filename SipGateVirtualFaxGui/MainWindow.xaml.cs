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
using SipgateVirtualFax;

namespace SipGateVirtualFaxGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ViewModel : BaseViewModel
    {
        private Faxline _selectedFaxLine;
        private string _faxNumber;
        private string _pdfPath;

        public IEnumerable<Faxline> FaxLines => Sipgate.GetFaxLinesSync(LookupCredential());

        public Faxline SelectedFaxLine
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
            if (Scan())
            {
                Send();
            }
        }

        public void ChoosePdfAndSend()
        {
            if (ChoosePdf())
            {
                Send();
            }
        }

        private bool ChoosePdf()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "PDF Documents (*.pdf)|*.pdf";
            if (openFileDialog.ShowDialog() == true)
            {
                _pdfPath = openFileDialog.FileName;
                return true;
            }

            return false;
        }

        private bool Scan()
        {
            try
            {
                string imagePath = Scanner.Scan(true).First();
                string pdfPath = Path.ChangeExtension(imagePath, "pdf");
                ImageToPdfConverter.Convert(imagePath, pdfPath);
                _pdfPath = pdfPath;
                return true;
            }
            catch (NoDocumentScannedException)
            {
                MessageBox.Show("No document scanned!");
                return false;
            }
        }

        private void Send()
        {
            var credential = LookupCredential();
            try
            {
                Sipgate.SendFax(_selectedFaxLine.Id, _faxNumber, _pdfPath, credential);
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