using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SipgateVirtualFax.Core.Sipgate
{
    public class FaxScheduler : IDisposable
    {
        private readonly SipgateFaxClient _client;
        private readonly BlockingCollection<TrackedFax> _pendingSend;
        private readonly BlockingCollection<TrackedFax> _pendingCompletion;

        private volatile bool _shutdown = false;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        
        public FaxScheduler(SipgateFaxClient client)
        {
            _client = client;
            _pendingSend = new BlockingCollection<TrackedFax>(new ConcurrentQueue<TrackedFax>());
            _pendingCompletion = new BlockingCollection<TrackedFax>(new ConcurrentQueue<TrackedFax>());

            var sender = new Thread(_sendThemAll)
            {
                Name = "Fax Sender"
            };
            sender.Start();
            
            var tracker = new Thread(_trackThemAll)
            {
                Name = "Fax Tracker"
            };
            tracker.Start();
        }

        public void Dispose()
        {
            _shutdown = true;
            _cancellation.Cancel();
        }

        private void _sendThemAll()
        {
            while (!_shutdown)
            {
                var fax = _pendingSend.Take(_cancellation.Token);
                try
                {
                    if (fax.Id == null)
                    {
                        fax.Id = _client.SendFax(fax.Faxline.Id, fax.Recipient, fax.DocumentPath).Result;
                        fax.Status = FaxStatus.Sending;
                    }
                    else
                    {
                        if (_client.AttemptFaxResend(fax.Id, fax.Faxline.Id).Result)
                        {
                            fax.Status = FaxStatus.Sending;
                        }
                    }
                    _pendingCompletion.Add(fax);
                }
                catch (Exception e)
                {
                    fax.FailureCause = e;
                    fax.Status = FaxStatus.Pending;
                }
            }
        }

        private void _trackThemAll()
        {
            while (!_shutdown)
            {
                var fax = _pendingSend.Take(_cancellation.Token);
                if (fax.Id != null)
                {
                    var historyEntry = _client.GetHistoryEntry(fax.Id);
                    Console.WriteLine(historyEntry);
                }
            }
        }
        
        public TrackedFax ScheduleFax(Faxline faxline, string recipient, string documentPath)
        {
            if (_shutdown)
            {
                throw new Exception("Already disposed!");
            }
            if (faxline.Id == null)
            {
                throw new Exception("Invalid faxline given: The ID is required!");
            }

            var fax = new TrackedFax(faxline, recipient, documentPath);
            _pendingSend.Add(fax);
            return fax;
        }
    }

    public class TrackedFax
    {
        public Faxline Faxline { get; }
        public string Recipient { get; }
        public string DocumentPath { get; }
        public string? Id { get; protected internal set; }
        public FaxStatus Status { get; protected internal set; }
        public Exception? FailureCause { get; protected internal set; }

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