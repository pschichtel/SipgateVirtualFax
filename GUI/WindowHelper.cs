using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

namespace SipGateVirtualFaxGui;

/**
     * Source: https://stackoverflow.com/a/21156860
     *
     * Customizations:
     * - Formatting
     * - Removed title matching (did not work and is not necessary)
     */
public static class WindowHelper
{
    private const int GwlExstyle = (-20);
    private const uint WsExAppwindow = 0x40000;

    private const uint WmShowwindow = 0x0018;
    private const int SwParentopening = 3;
        
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
        
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc ewp, int lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern uint GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowText(IntPtr hWnd, StringBuilder lpString, uint nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

    private static bool IsApplicationWindow(IntPtr hWnd)
    {
        return (GetWindowLong(hWnd, GwlExstyle) & WsExAppwindow) != 0;
    }

    private static IntPtr GetWindowHandle(int pid)
    {
        var result = IntPtr.Zero;

        bool EnumerateHandle(IntPtr hWnd, int lParam)
        {
            GetWindowThreadProcessId(hWnd, out var id);

            if (pid == id)
            {
                var clsName = new StringBuilder(256);
                var hasClass = GetClassName(hWnd, clsName, 256);
                if (hasClass)
                {
                    var maxLength = (int) GetWindowTextLength(hWnd);
                    var builder = new StringBuilder(maxLength + 1);
                    GetWindowText(hWnd, builder, (uint) builder.Capacity);

                    var className = clsName.ToString();

                    // There could be multiple handle associated with our pid, 
                    // so we return the first handle that satisfy:
                    // 1) the window class name starts with HwndWrapper (WPF specific)
                    // 2) the window has WS_EX_APPWINDOW style

                    if (className.StartsWith("HwndWrapper") && IsApplicationWindow(hWnd))
                    {
                        result = hWnd;
                        return false;
                    }
                }
            }

            return true;
        }

        EnumDesktopWindows(IntPtr.Zero, EnumerateHandle, 0);

        return result;
    }

    public static void BringWindowToForeground(Process runningProcess)
    {
        var windowHandle = runningProcess.MainWindowHandle;
            
        if (windowHandle == IntPtr.Zero) {
            var handle = GetWindowHandle(runningProcess.Id);
            if (handle != IntPtr.Zero) {
                // show window
                ShowWindow(handle, 5);
                // send WM_SHOWWINDOW message to toggle the visibility flag
                SendMessage(handle, WmShowwindow, IntPtr.Zero, new IntPtr(SwParentopening));
            }
        }
        else
        {
            SetForegroundWindow(windowHandle);
        }
    }
}