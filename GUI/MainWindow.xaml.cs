using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace SipGateVirtualFaxGui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly NotifyIcon? _icon;
        
        public MainWindow()
        {
            var manifestModuleName = System.Reflection.Assembly.GetEntryAssembly()?.ManifestModule?.Name;
            if (manifestModuleName != null)
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(manifestModuleName);
                if (icon != null)
                {
                    _icon = SetupIcon(icon);
                }
            }

            InitializeComponent();
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
            
            //notifyIcon.ShowBalloonTip(2000, "Fax erfolgreich", "Fax an ... erfolgreich gesendet!", ToolTipIcon.Info);

            return notifyIcon;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_icon != null)
            {
                e.Cancel = true;
                Hide();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _icon?.Dispose();
        }
    }
}