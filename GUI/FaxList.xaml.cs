using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using PhoneNumbers;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;
using MessageBox = System.Windows.MessageBox;

namespace SipGateVirtualFaxGui;

public partial class FaxList
{
    private readonly Logger _logger = Logging.GetLogger("gui-fax-list");
    private Faxline[]? _faxlines;
        
    public FaxList()
    {
        InitializeComponent();
    }

    private async Task<Faxline[]> LazilyLoadFaxlines()
    {
        if (_faxlines == null)
        {
            var faxlines = await FaxStuff.Instance.FaxClient.GetAllUsableFaxlines();
            _faxlines = faxlines;
            return faxlines;
        }

        return _faxlines;
    }

    private async void New_OnClick(object sender, RoutedEventArgs @event)
    {
        Faxline[] faxlines;
        try
        {
            faxlines = await LazilyLoadFaxlines();
        }
        catch (OAuth2ImplicitFlowException e)
        {
            _logger.Warn(e, "Auth failed: " + e.Message);
            MessageBox.Show(Properties.Resources.Err_OAuthFailed);
            return;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to load faxlines from sipgate!");
            MessageBox.Show(Properties.Resources.Err_FailedToLoadFaxlines);
            return;
        }

        var window = new NewFax()
        {
            Title = Properties.Resources.NewFax,
            SizeToContent = SizeToContent.Height,
            Width = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this)
        };
        var newFaxModel = (NewFaxViewModel) window.DataContext;
        newFaxModel.Faxlines = faxlines;
            
        window.ShowDialog();

        if (newFaxModel.SelectedFaxline == null || newFaxModel.DocumentPath == null)
        {
            return;
        }

        try
        {
            var phoneNumberUtil = PhoneNumberUtil.GetInstance();
            var phoneNumber = phoneNumberUtil.Parse(newFaxModel.FaxNumber, "DE");
            var formattedFaxNumber = phoneNumberUtil.Format(phoneNumber, PhoneNumberFormat.E164);

            var fax = FaxStuff.Instance.FaxScheduler
                .ScheduleFax(newFaxModel.SelectedFaxline, formattedFaxNumber, newFaxModel.DocumentPath);

            var syncContext = SynchronizationContext.Current;
            fax.StatusChanged += (_, status) =>
            {
                syncContext.Post(f =>
                {
                    switch (status)
                    {
                        case FaxStatus.SuccessfullySent:
                            _logger.Info("Fax send successfully!");
                            break;
                        case FaxStatus.Failed:
                            _logger.Error(fax.FailureCause, "Fax failed to send!");
                            break;
                        case FaxStatus.Unknown:
                            _logger.Warn(fax.FailureCause, "Fax sending entered an unknown state!");
                            break;
                    }
                }, fax);
            };

            var faxListItemViewModel = new FaxListItemViewModel(fax);

            var viewModel = (FaxListViewModel) DataContext;
            viewModel.Items.Insert(0, faxListItemViewModel);
        }
        catch (Exception e)
        {
            _logger.Error(e,
                $"Failed to schedule the fax: faxline={newFaxModel.SelectedFaxline}; recipient={newFaxModel.FaxNumber}; document={newFaxModel.DocumentPath}");
            MessageBox.Show(Properties.Resources.Err_FailedToSendFax);
        }
    }
}

public class FaxListViewModel : BaseViewModel
{
    public ObservableCollection<FaxListItemViewModel> Items { get; }
        = new ObservableCollection<FaxListItemViewModel>();
}