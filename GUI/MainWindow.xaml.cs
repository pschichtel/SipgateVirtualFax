using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Application = System.Windows.Application;

namespace SipGateVirtualFaxGui;

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
        var exit = new ToolStripMenuItem
        {
            Text = Properties.Resources.Tray_Exit
        };
        exit.Click += (_, _) => { Application.Current.Shutdown(0); };
        var open = new ToolStripMenuItem
        {
            Text = Properties.Resources.Tray_Open
        };
        open.Click += (_, _) =>
        {
            Show();
        };
        var menu = new ContextMenuStrip
        {
            Items =
            {
                open,
                exit,
            }

        };
        var notifyIcon = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = menu,
            Icon = icon
        };
        notifyIcon.DoubleClick += (_, _) =>
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

    private void LogoutCommandBinding_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            File.Delete(GuiOauthImplicitFlowHandler.AccessTokenPath());
        }
        catch (IOException)
        {}

        try
        {
            File.Delete(GuiOauthImplicitFlowHandler.CookieJarPath());
        }
        catch (IOException)
        {}
    }
}