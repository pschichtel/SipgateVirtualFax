using NTwain;

namespace SipgateVirtualFax.Core
{
    public static class TwainSessionExtensions
    {
        public static TwainState GetState(this ITwainSession session)
        {
            return (TwainState) session.State;
        }
    }
}