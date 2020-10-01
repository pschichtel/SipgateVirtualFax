using System.Windows;
using System.Windows.Controls;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    public partial class FaxListItem : UserControl
    {
        public FaxListItem()
        {
            InitializeComponent();
        }

        private void Resend_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (FaxListItemViewModel) DataContext;
            vm.Resend();
        }
    }

    public class FaxListItemViewModel : BaseViewModel
    {
        private FaxStatus _faxStatus;
        private bool _isResendVisible;

        public string Recipient { get; }

        public Faxline Faxline { get; }

        public FaxStatus FaxStatus
        {
            get => _faxStatus;
            set
            {
                _faxStatus = value;
                OnPropertyChanged(nameof(FaxStatus));
                IsResendVisible = _faxStatus == FaxStatus.Failed;
            }
        }

        public bool IsResendVisible
        {
            get => _isResendVisible;
            set
            {
                _isResendVisible = value;
                OnPropertyChanged(nameof(IsResendVisible));
            }
        }

        public FaxListItemViewModel(string recipient, Faxline faxline)
        {
            Recipient = recipient;
            Faxline = faxline;
        }

        public void Resend()
        {
            // TODO: Resend logic
        }
    }
}