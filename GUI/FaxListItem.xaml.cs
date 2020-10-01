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
    }

    public class FaxListItemViewModel : BaseViewModel
    {
        private FaxStatus _faxStatus;

        public string Recipient { get; }

        public Faxline Faxline { get; }

        public FaxStatus FaxStatus
        {
            get => _faxStatus;
            set
            {
                _faxStatus = value;
                OnPropertyChanged(nameof(FaxStatus));
            }
        }

        public FaxListItemViewModel(string recipient, Faxline faxline)
        {
            Recipient = recipient;
            Faxline = faxline;
        }
    }
}