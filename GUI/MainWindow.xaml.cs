using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Application = System.Windows.Application;

namespace SipGateVirtualFaxGui
{
    public partial class MainWindow
    {
        public static readonly DependencyProperty NotifyIconProperty =
            DependencyProperty.Register(nameof(NotifyIcon), typeof(NotifyIcon), typeof(MainWindow), new UIPropertyMetadata(null));
        
        public NotifyIcon? NotifyIcon
        {
            get => (NotifyIcon?)GetValue(NotifyIconProperty);
            set => SetValue(NotifyIconProperty, value);
        }
        
        public MainWindow()
        {
            var manifestModuleName = System.Reflection.Assembly.GetEntryAssembly()?.ManifestModule?.Name;
            if (manifestModuleName != null)
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(manifestModuleName);
                if (icon != null)
                {
                    NotifyIcon = SetupIcon(icon);
                }
            }
            InitializeComponent();
        }

        private void CloseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Hide();
        }

        private NotifyIcon SetupIcon(Icon icon)
        {
            var exit = new MenuItem()
            {
                Index = 0,
                Text = Properties.Resources.Tray_Exit
            };
            exit.Click += (sender, args) => { Application.Current.Shutdown(0); };
            var open = new MenuItem
            {
                Index = 1,
                Text = Properties.Resources.Tray_Open
            };
            open.Click += (sender, args) =>
            {
                Show();
            };
            var menu = new ContextMenu(new[]
            {
                open,
                exit,
            });
            var notifyIcon = new NotifyIcon
            {
                Visible = true,
                ContextMenu = menu,
                Icon = icon
            };
            notifyIcon.DoubleClick += (sender, args) =>
            {
                Show();
            };
            
            return notifyIcon;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (NotifyIcon != null)
            {
                e.Cancel = true;
                Hide();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            NotifyIcon?.Dispose();
        }


        private void LoginCommandBinding_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri("https://login.sipgate.com/auth/realms/sipgate-apps/protocol/openid-connect/auth?response_type=code&client_id=2678637-1-60b58b61-8106-11ec-9225-1fac1a8d5fca%3Asipgate-apps&state=___&scope=sessions%3Afax%3Awrite%20history%3Aread%20faxlines%3Aread%20groups%3Afaxlines%3Aread%20groups%3Ausers%3Aread&redirect_uri=https%3A%2F%2Flocalhost%3A31337&code_challenge=5f7052d8aa78fd799dc80a3ef859f96c81b4d6cc53cc81f2b45ddd69421b7198&code_challenge_method=S256");

            void Callback(Uri? uri1)
            {
                
            }
            var authentication = new Authentication(uri, Callback);
            authentication.ShowDialog();
        }

        private void LogoutCommandBinding_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}