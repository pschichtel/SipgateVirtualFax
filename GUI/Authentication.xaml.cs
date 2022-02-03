using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using CefSharp;
using CefSharp.Handler;
using NLog;
using SipgateVirtualFax.Core;
using static SipgateVirtualFax.Core.Sipgate.OAuth2ImplicitFlowHeaderProvider;

namespace SipGateVirtualFaxGui
{
    public partial class Authentication
    {
        private readonly TaskCompletionSource<LoginResult?> _promise = new TaskCompletionSource<LoginResult?>();
        public Task<LoginResult?> Result => _promise.Task;
        public Authentication(Uri authorizationUri, Uri expectedRedirectUri)
        {
            InitializeComponent();
            
            WebBrowser.Address = authorizationUri.ToString();
            WebBrowser.RequestHandler = new NavigationTracker(authorizationUri, expectedRedirectUri, _promise, Dispatcher);
        }

        protected override void OnClosed(EventArgs e)
        {
            WebBrowser.Dispose();
            _promise.TrySetResult(null);
        }

        private void CloseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }
        private class NavigationTracker : RequestHandler
        {
            private readonly Uri _authorizationUri;
            private readonly Uri _expectedRedirectUri;
            private readonly TaskCompletionSource<LoginResult?> _promise;
            private readonly Dispatcher _dispatcher;
            private readonly Logger _logger = Logging.GetLogger("cef-navigation-tracker");
            
            public NavigationTracker(Uri authorizationUri, Uri expectedRedirectUri, TaskCompletionSource<LoginResult?> promise,
                Dispatcher dispatcher)
            {
                _authorizationUri = authorizationUri;
                _expectedRedirectUri = expectedRedirectUri;
                _promise = promise;
                _dispatcher = dispatcher;
            }
        
            protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture,
                bool isRedirect)
            {
                _logger.Info($"CEF request (userGesture={userGesture}, isRedirect={isRedirect}): {request.Method} {request.Url}");
                var uri = new Uri(request.Url);
                if (!IsValidRedirectionTarget(uri, _expectedRedirectUri))
                {
                    return false;
                }
                
                var cookieManager = chromiumWebBrowser.GetCookieManager();
                var cookies = cookieManager.VisitUrlCookiesAsync(_authorizationUri.ToString(), true);
                _dispatcher.Invoke(() =>
                {
                    _promise.SetResult(new LoginResult(uri, cookies));
                });
                return true;
            }
        }
    }

}