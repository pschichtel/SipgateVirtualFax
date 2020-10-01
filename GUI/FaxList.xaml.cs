using System.Collections.ObjectModel;
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

        private void New_OnClick(object sender, RoutedEventArgs e)
        {
            _newFax.ViewModel.Initialize();
            var window = new Window()
            {
                Title = "New fax",
                Content = _newFax,
                SizeToContent = SizeToContent.Height,
                Width = 500
            };
            window.ShowDialog();

            var newFaxViewModel = _newFax.ViewModel;
            if (newFaxViewModel.SelectedFaxLine == null)
            {
                return;
            }

            var viewmodel = (FaxListViewModel) DataContext;
            viewmodel.Items.Add(new FaxListItemViewModel(newFaxViewModel.FaxNumber, newFaxViewModel.SelectedFaxLine));
        }
    }

    public class FaxListViewModel : BaseViewModel
    {
        public ObservableCollection<FaxListItemViewModel> Items { get; } = new ObservableCollection<FaxListItemViewModel>()
        {
            new FaxListItemViewModel("123465", new Faxline("123", "Dort", "456", true)),
            new FaxListItemViewModel("456798", new Faxline("456", "Hier steht ein ganz langer Text", "789", true)),
            new FaxListItemViewModel("789132", new Faxline("789", "Irgendwo", "123", true))
            {
                FaxStatus = FaxStatus.Failed
            }
        };
    }
}