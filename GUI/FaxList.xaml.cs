using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    public partial class FaxList
    {
        private readonly Logger _logger = Logging.GetLogger("gui-faxlist");
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
                var fax = FaxStuff.Instance.FaxScheduler
                    .ScheduleFax(newFaxModel.SelectedFaxline, newFaxModel.FaxNumber, newFaxModel.DocumentPath);

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
}