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
        public TrackedFax Fax { get; }
        public bool IsResendVisible => Fax.Status.IsComplete() && Fax.Status.CanResend();

        public FaxListItemViewModel(TrackedFax fax)
        {
            Fax = fax;
            fax.StatusChanged += (sender, status) =>
            {
                OnPropertyChanged(nameof(Fax));
                OnPropertyChanged(nameof(IsResendVisible));
            };
        }

        public void Resend()
        {
            Fax.Resend();
        }
    }
}