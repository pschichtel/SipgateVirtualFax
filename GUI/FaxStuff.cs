using SipgateVirtualFax.Core.Sipgate;

namespace SipGateVirtualFaxGui;

public class FaxStuff
{
    public static readonly FaxStuff Instance = new FaxStuff();
        
    public SipgateFaxClient FaxClient { get; }
    public FaxScheduler FaxScheduler { get; }

    private FaxStuff()
    {
        FaxClient = new SipgateFaxClient(new AccessTokenThenOauthProvider());
        FaxScheduler = new FaxScheduler(FaxClient);
    }
}