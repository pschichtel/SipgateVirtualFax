using System;
using System.Collections.Specialized;
using System.Web;
using System.Windows;
using System.Windows.Input;

namespace SipGateVirtualFaxGui
{
    public partial class Authentication
    {
        private readonly Uri _expectedReturnUri;
        private readonly Action<Uri?> _callback;
        
        public Authentication(Uri uri, Action<Uri?> callback)
        {
            InitializeComponent();

            _callback = callback;
            
            NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(uri.Query);
            string? returnUri = nameValueCollection.Get("redirect_uri");
            if (returnUri == null)
            {
                throw new ArgumentException("URI has no return URI!", nameof(uri));
            }

            _expectedReturnUri = new Uri(returnUri);

            WebBrowser.Address = uri.Query;
        }

        private void WebBrowser_OnAddressChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            NameValueCollection nameValueCollection = HttpUtility.ParseQueryString(e.NewValue.ToString());
            string? returnUri = nameValueCollection.Get("redirect_uri");
            var uri = new Uri(returnUri);
            if (uri.Host != _expectedReturnUri.Host)
            {
                return;
            }

            WindowCollection windows = Application.Current.Windows;
            windows[1]?.Close();
            _callback(uri);
        }

        private void CloseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _callback(null);
        }
    }
}