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
        
        private void OpenPdf_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (FaxListItemViewModel) DataContext;
            System.Diagnostics.Process.Start(vm.Fax.DocumentPath);
        }
    }

    public class FaxListItemViewModel : BaseViewModel
    {
        public TrackedFax Fax { get; private set; }
        public bool IsResendVisible => Fax.Status.IsComplete() && Fax.Status.CanResend();

        public FaxListItemViewModel(TrackedFax fax)
        {
            Fax = ConfigureFax(fax);
        }

        private TrackedFax ConfigureFax(TrackedFax fax)
        {
            fax.StatusChanged += (sender, status) =>
            {
                UpdateProperties();
            };
            return fax;
        }

        public void Resend()
        {
            Fax = ConfigureFax(Fax.Resend());
            UpdateProperties();
        }

        private void UpdateProperties()
        {
            OnPropertyChanged(nameof(Fax));
            OnPropertyChanged(nameof(IsResendVisible));
        }
    }
}