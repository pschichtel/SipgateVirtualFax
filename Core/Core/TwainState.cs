namespace SipgateVirtualFax.Core
{
    enum TwainState
    {
        PreSession = 1,
        SourceManagerLoaded = 2,
        SourceManagerOpened = 3,
        SourceOpen = 4,
        SourceEnabled = 5,
        TransferReady = 6,
        Transferring = 7,
    }
}