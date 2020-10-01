using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace SipGateVirtualFaxGui
{
    public partial class FaxList : UserControl
    {
        public FaxList()
        {
            InitializeComponent();
        }
    }

    public class FaxListViewModel : BaseViewModel
    {
        public ObservableCollection<FaxListItemViewModel> Items { get; } = new ObservableCollection<FaxListItemViewModel>();
    }
}