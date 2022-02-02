using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using CefSharp;

namespace SipGateVirtualFaxGui
{
    public partial class Authentication
    {
        private readonly TaskCompletionSource<LoginResult?> _promise = new TaskCompletionSource<LoginResult?>();
        public Task<LoginResult?> Result => _promise.Task;
        public Authentication(Uri authorizationUri)
        {
            InitializeComponent();

            var nameValueCollection = HttpUtility.ParseQueryString(authorizationUri.Query);
            var returnUri = nameValueCollection.Get("redirect_uri");
            if (returnUri == null)
            {
                throw new ArgumentException("URI has no return URI!", nameof(authorizationUri));
            }

            var expectedReturnUri = new Uri(returnUri);

            WebBrowser.Address = authorizationUri.ToString();

            void TryComplete(string uriString)
            {
                if (uriString.StartsWith("about:"))
                {
                    return;
                }
                var uri = new Uri(uriString);
                if (uri.Host == expectedReturnUri.Host && uri.Fragment.Length > 1)
                {
                    WebBrowser.LoadError -= OnLoadErr;
                    WebBrowser.FrameLoadStart -= OnFrameLoadStart;
                    Dispatcher.Invoke(() =>
                    {
                        Close();
                        _promise.SetResult(new LoginResult(uri, new Dictionary<string, string>()));
                    });
                }
            }

            void OnFrameLoadStart(object browser, FrameLoadStartEventArgs args)
            {
                TryComplete(args.Url);
            }

            void OnLoadErr(object browser, LoadErrorEventArgs args)
            {
                TryComplete(args.FailedUrl);
            }

            WebBrowser.FrameLoadStart += OnFrameLoadStart;
            WebBrowser.LoadError += OnLoadErr;
        }

        private void CloseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _promise.SetResult(null);
        }
    }
}