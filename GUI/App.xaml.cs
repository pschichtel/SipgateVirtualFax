using System.Windows;

namespace SipGateVirtualFaxGui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
        }

        protected override void OnExit(ExitEventArgs e)
        {
            FaxStuff.Instance.FaxScheduler.Dispose();
        }
    }
}