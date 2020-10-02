using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NLog;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    public partial class FaxList : UserControl
    {
        private readonly NewFax _newFax = new NewFax();
        private readonly Logger _logger = Logging.GetLogger("gui-faxlist");

        public FaxList()
        {
            InitializeComponent();
        }

        private async void New_OnClick(object sender, RoutedEventArgs e)
        {
            var faxlines = await FaxStuff.Instance.FaxClient.GetFaxLines();

            _newFax.ViewModel.Initialize(faxlines);
            var window = new Window()
            {
                Title = "New fax",
                Content = _newFax,
                SizeToContent = SizeToContent.Height,
                Width = 500
            };
            window.ShowDialog();

            var newFaxModel = _newFax.ViewModel;
            if (newFaxModel.SelectedFaxLine == null || newFaxModel.DocumentPath == null)
            {
                return;
            }

            try
            {
                var fax = FaxStuff.Instance.FaxScheduler
                    .ScheduleFax(newFaxModel.SelectedFaxLine, newFaxModel.FaxNumber, newFaxModel.DocumentPath);

                var faxListItemViewModel = new FaxListItemViewModel(fax);

                var viewModel = (FaxListViewModel) DataContext;
                viewModel.Items.Insert(0, faxListItemViewModel);
            }
            catch (Exception ex)
            {
                _logger.Error(ex,
                    $"Failed to schedule the fax: faxline={newFaxModel.SelectedFaxLine}; recipient={newFaxModel.FaxNumber}; document={newFaxModel.DocumentPath}");
            }
        }
    }

    public class FaxListViewModel : BaseViewModel
    {
        public ObservableCollection<FaxListItemViewModel> Items { get; }
            = new ObservableCollection<FaxListItemViewModel>();
    }
}