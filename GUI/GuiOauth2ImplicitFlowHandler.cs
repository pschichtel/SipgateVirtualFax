using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NLog;
using SipgateVirtualFax.Core;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui
{
    public class LoginResult
    {
        public readonly Uri Uri;
        public readonly IDictionary<string, string> Cookies;

        public LoginResult(Uri uri, IDictionary<string, string> cookies)
        {
            Cookies = cookies;
            Uri = uri;
        }
    }
    class GuiOauthImplicitFlowHandler : IOAuthImplicitFlowHandler
    {
        private readonly Logger _logger = Logging.GetLogger("gui-oauth-handler");
        private const string CredentialName = "sipgate-fax-access-token";

        Task<string?> IOAuthImplicitFlowHandler.GetAccessTokenFromStorage()
        {
            try
            {
                var encrypted = File.ReadAllBytes(accessTokenPath());
                var data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Task.FromResult<string?>(Encoding.ASCII.GetString(data));
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult<string?>(null);
            }
            catch (DirectoryNotFoundException)
            {
                return Task.FromResult<string?>(null);
            }
            catch (IOException e)
            {
                _logger.Error(e, "Failed to read access token!");
                return Task.FromResult<string?>(null);
            }
        }

        async Task<Uri> IOAuthImplicitFlowHandler.Authorize(Uri authorizationUri)
        {
            var authentication = new Authentication(authorizationUri);
            authentication.ShowDialog();
            var result = await authentication.Result;
            if (result == null)
            {
                throw new ArgumentException("meh!"); // TODO fix exception
            }
            
            // TODO persist cookies

            return result.Uri;
        }

        private string accessTokenPath()
        {
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataFolder, "SipgateFaxApp");
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, "access-token.secret");
        }

        public Task StoreAccessToken(string accessToken)
        {
            var data = Encoding.ASCII.GetBytes(accessToken);
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(accessTokenPath(), encrypted);
            return Task.CompletedTask;
        }
    }
}
