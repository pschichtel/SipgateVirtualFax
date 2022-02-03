using System;
using System.Net;
using System.Windows;
using NLog;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;
using static SipGateVirtualFaxGui.Properties.Resources;
using MessageBox = System.Windows.MessageBox;

namespace SipGateVirtualFaxGui;

public partial class FaxListItem
{
    private readonly Logger _logger = Logging.GetLogger("gui-fax-list");
        
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
            MessageBox.Show(Err_FailedToOpenDocument);
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
                FaxStatus.Pending => Status_Pending,
                FaxStatus.Sending => Status_Sending,
                FaxStatus.SuccessfullySent => Status_SuccessfullySent,
                FaxStatus.Failed => FailedStatusText(Fax.FailureCause),
                FaxStatus.Unknown => Status_Unknown,
                _ => "???"
            };
        }
    }

    private static string FailedStatusText(Exception? cause)
    {
        if (cause != null)
        {
            switch (cause)
            {
                case FaxSendException {Status: HistoryEntry.EntryStatus.NoPickup}:
                    return string.Format(Status_FailedWithReason, Status_FailedNoPickup);
                case FaxSendException {Status: HistoryEntry.EntryStatus.Busy}:
                    return string.Format(Status_FailedWithReason, Status_FailedBusy);
                case SipgateApiHttpException {Status: HttpStatusCode.ProxyAuthenticationRequired}:
                    return string.Format(Status_FailedWithReason, Status_FailedInvalidDestination);
            }
        }
        return Status_Failed;
    }

    private TrackedFax ConfigureFax(TrackedFax fax)
    {
        fax.StatusChanged += (_, _) =>
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