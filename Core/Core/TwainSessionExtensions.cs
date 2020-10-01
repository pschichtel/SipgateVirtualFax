using NTwain;

namespace SipgateVirtualFax.Core
{
    static class TwainSessionExtensions
    {
        public static TwainState GetState(this TwainSession session)
        {
            return (TwainState) session.State;
        }
    }
}