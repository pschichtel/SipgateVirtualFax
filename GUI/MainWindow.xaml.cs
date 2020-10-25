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
    }
}