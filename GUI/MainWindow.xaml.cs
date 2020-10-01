using System.Windows;

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
        }

        private void New_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new Window()
            {
                Title = "New fax",
                Content = new NewFax(),
                SizeToContent = SizeToContent.Height,
                Width = 500
            };
            window.ShowDialog();
        }
    }
}