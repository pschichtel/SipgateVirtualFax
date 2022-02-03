using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui;

public class FaxStuff
{
    public static readonly FaxStuff Instance = new FaxStuff();
        
    public SipgateFaxClient FaxClient { get; }
    public FaxScheduler FaxScheduler { get; }

    public FaxStuff()
    {
        FaxClient = new SipgateFaxClient(new OAuth2ImplicitFlowHeaderProvider(new GuiOauthImplicitFlowHandler()));
        FaxScheduler = new FaxScheduler(FaxClient);
    }
}