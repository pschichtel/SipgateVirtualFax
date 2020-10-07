using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NLog;
using SipgateVirtualFax.Core;

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

        private async void New_OnClick(object sender, RoutedEventArgs @event)
        {
            try
            {
                var faxlines = await FaxStuff.Instance.FaxClient.GetFaxLines();

                _newFax.ViewModel.Initialize(faxlines);
                var window = new Window
                {
                    Title = "New fax",
                    Content = _newFax,
                    SizeToContent = SizeToContent.Height,
                    Width = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this)
                };
                window.ShowDialog();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to load faxlines from sipgate!");
                MessageBox.Show("Failed to load the fax lines!");
            }

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
            catch (Exception e)
            {
                _logger.Error(e,
                    $"Failed to schedule the fax: faxline={newFaxModel.SelectedFaxLine}; recipient={newFaxModel.FaxNumber}; document={newFaxModel.DocumentPath}");
                MessageBox.Show("Failed to send the fax!");
            }
        }
    }

    public class FaxListViewModel : BaseViewModel
    {
        public ObservableCollection<FaxListItemViewModel> Items { get; }
            = new ObservableCollection<FaxListItemViewModel>();
    }
}