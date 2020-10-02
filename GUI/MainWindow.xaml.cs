using System;
using System.Windows;
using CredentialManagement;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            Title = "SipgateVirtualFax";
        }

        protected override void OnClosed(EventArgs e)
        {
            FaxStuff.Instance.FaxScheduler.Dispose();
        }
    }
}