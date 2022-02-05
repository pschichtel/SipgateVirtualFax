using System.Threading.Tasks;
using CredentialManagement;
using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui;

public class AccessTokenThenOauthProvider : IAuthorizationHeaderProvider
{
    private static readonly OAuth2ImplicitFlowHeaderProvider Oauth = new(new GuiOauthImplicitFlowHandler());

    private readonly IAuthorizationHeaderProvider _provider;

    public AccessTokenThenOauthProvider()
    {
        var credential = new Credential { Target = "sipgate-fax" };
        if (credential.Load())
        {
            _provider = new BasicAuthHeaderProvider(credential.Username, credential.Password);
        }
        else
        {
            _provider = Oauth;
        }
    }

    public bool RetryOn401 => _provider.RetryOn401;

    public Task<string> GetHeaderValue(bool retry) => _provider.GetHeaderValue(retry);
}