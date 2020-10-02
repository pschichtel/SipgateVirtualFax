using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    public partial class FaxList : UserControl
    {
        private readonly NewFax _newFax = new NewFax();
        
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

            var fax = FaxStuff.Instance.FaxScheduler
                .ScheduleFax(newFaxModel.SelectedFaxLine, newFaxModel.FaxNumber, newFaxModel.DocumentPath);

            var faxListItemViewModel = new FaxListItemViewModel(fax);

            var viewModel = (FaxListViewModel) DataContext;
            viewModel.Items.Insert(0, faxListItemViewModel);
        }
    }

    public class FaxListViewModel : BaseViewModel
    {
        public ObservableCollection<FaxListItemViewModel> Items { get; }
            = new ObservableCollection<FaxListItemViewModel>();
    }
}