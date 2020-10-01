using System;
using System.Collections.Concurrent;

namespace SipgateVirtualFax.Core.Sipgate
{
    public class FaxScheduler
    {
        private readonly SipgateFaxClient _client;
        private readonly ConcurrentQueue<TrackedFax> _pendingSend;
        private readonly ConcurrentQueue<TrackedFax> _pendingCompletion;
        
        public FaxScheduler(SipgateFaxClient client)
        {
            _client = client;
            _pendingSend = new ConcurrentQueue<TrackedFax>();
            _pendingCompletion = new ConcurrentQueue<TrackedFax>();
        }
        
        public TrackedFax ScheduleFax(Faxline faxline, string recipient, string documentPath)
        {
            if (faxline.Id == null)
            {
                throw new Exception("Invalid faxline given: The ID is required!");
            }

            var fax = new TrackedFax(faxline, recipient, documentPath);
            _pendingSend.Enqueue(fax);
            return fax;
        }
    }

    public class TrackedFax
    {
        public Faxline Faxline { get; }
        public string Recipient { get; }
        public string DocumentPath { get; }
        public string? Id { get; }
        public FaxStatus Status { get; }

        public TrackedFax(Faxline faxline, string recipient, string documentPath)
        {
            Faxline = faxline;
            Recipient = recipient;
            DocumentPath = documentPath;
            Id = null;
            Status = FaxStatus.Pending;
        }
    }

    public enum FaxStatus
    {
        Pending,
        Sending,
        SuccessfullySent,
        Failed
    }
}