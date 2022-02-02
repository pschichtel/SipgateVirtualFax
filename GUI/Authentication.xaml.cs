using System;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;

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
            var returnUri = nameValueCollection.Get("redirect_uri")!;
            var expectedReturnUri = new Uri(returnUri);

            WebBrowser.Address = authorizationUri.ToString();

            void TryComplete(ChromiumWebBrowser browser, string uriString)
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
                    var cookieManager = browser.GetCookieManager();
                    var cookies = cookieManager.VisitUrlCookiesAsync(authorizationUri.ToString(), true);
                    Dispatcher.Invoke(() =>
                    {
                        _promise.SetResult(new LoginResult(uri, cookies));
                        Close();
                    });
                }
            }

            void OnFrameLoadStart(object browser, FrameLoadStartEventArgs args)
            {
                TryComplete((ChromiumWebBrowser) browser, args.Url);
            }

            void OnLoadErr(object browser, LoadErrorEventArgs args)
            {
                TryComplete((ChromiumWebBrowser) browser, args.FailedUrl);
            }

            WebBrowser.FrameLoadStart += OnFrameLoadStart;
            WebBrowser.LoadError += OnLoadErr;
        }

        protected override void OnClosed(EventArgs e)
        {
            _promise.TrySetResult(null);
        }

        private void CloseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }
    }
}