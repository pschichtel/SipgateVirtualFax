using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace SipGateVirtualFaxGui
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private readonly Mutex _mutex;


        public App()
        {
            // Try to grab mutex
            _mutex = new Mutex(true, "tel-schich-sipgate-virtual-fax", out var createdNew);

            if (!createdNew)
            {
                // Bring other instance to front and exit.
                Process current = Process.GetCurrentProcess();
                foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id)
                    {
                        WindowHelper.BringWindowToForeground(process);
                        break;
                    }
                }

                Shutdown();
            }
            else
            {
                Exit += HandleExit;
            }
        }

        protected virtual void HandleExit(object sender, EventArgs e)
        {
            FaxStuff.Instance.FaxScheduler.Dispose();
            _mutex.Close();
        }
    }
}