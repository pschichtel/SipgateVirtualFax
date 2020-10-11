using System;
using System.Windows;
using NLog;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;
using MessageBox = System.Windows.MessageBox;

namespace SipGateVirtualFaxGui
{
    public partial class FaxListItem
    {
        private readonly Logger _logger = Logging.GetLogger("gui-faxlist");
        
        public FaxListItem()
        {
            InitializeComponent();
        }

        private void Resend_OnClick(object sender, RoutedEventArgs e)
        {
            var vm = (FaxListItemViewModel) DataContext;
            vm.Resend();
        }
        
        private void OpenPdf_OnClick(object sender, RoutedEventArgs ev)
        {
            var vm = (FaxListItemViewModel) DataContext;
            try
            {
                System.Diagnostics.Process.Start(vm.Fax.DocumentPath);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to open a fax document!");
                MessageBox.Show(Properties.Resources.Err_FailedToOpenDocument);
            }
        }
    }

    public class FaxListItemViewModel : BaseViewModel
    {
        public TrackedFax Fax { get; private set; }
        public bool ShowResend => Fax.MayResend;

        public FaxListItemViewModel(TrackedFax fax)
        {
            Fax = ConfigureFax(fax);
        }

        public string Status
        {
            get
            {
                return Fax.Status switch
                {
                    FaxStatus.Pending => Properties.Resources.Status_Pending,
                    FaxStatus.Sending => Properties.Resources.Status_Sending,
                    FaxStatus.SuccessfullySent => Properties.Resources.Status_SuccessfullySent,
                    FaxStatus.Failed => Properties.Resources.Status_Failed,
                    FaxStatus.Unknown => Properties.Resources.Status_Unknown,
                    _ => "???"
                };
            }
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
            OnPropertyChanged(nameof(ShowResend));
            OnPropertyChanged(nameof(Status));
        }
    }
}