using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CredentialManagement;
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

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (ViewModel) DataContext;
            vm.ScanAndSend();
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
        private string _telNumber;

        public IEnumerable<Faxline> FaxLines => Sipgate.GetFaxLinesSync(LookupCredential());

        public Faxline SelectedFaxLine
        {
            get => _selectedFaxLine;
            set
            {
                _selectedFaxLine = value; 
                OnPropertyChanged(nameof(_selectedFaxLine));
            }
        }

        public string TelNumber
        {
            get => _telNumber;
            set
            {
                _telNumber = value; 
                OnPropertyChanged(nameof(TelNumber));
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
            try
            {
                string imagePath = Scanner.Scan(true).First();
                string pdfPath = System.IO.Path.ChangeExtension(imagePath, "pdf");
                ImageToPdfConverter.Convert(imagePath, pdfPath);
                var credential = LookupCredential();
                try
                {
                    Sipgate.SendFax(_selectedFaxLine.Id, _telNumber, pdfPath, credential);
                    MessageBox.Show("Successfully send the fax!");
                }
                catch (Exception e)
                {
                    MessageBox.Show("Failed to send fax!");
                    MessageBox.Show(e.Message);
                }
            }
            catch (NoDocumentScannedException e)
            {
                MessageBox.Show("No document scanned!");
            }

        }
    }
}