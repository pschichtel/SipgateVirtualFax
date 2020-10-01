using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using static SipgateVirtualFax.Core.Sipgate.HistoryEntry;

namespace SipgateVirtualFax.Core.Sipgate
{
    public class FaxScheduler : IDisposable
    {
        private readonly SipgateFaxClient _client;
        private readonly BlockingCollection<TrackedFax> _pendingSend;
        private readonly BlockingCollection<TrackedFax> _pendingCompletion;

        private volatile bool _shutdown;
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
                TrackedFax fax;
                try
                {
                    Console.WriteLine("Polling the pending queue...");
                    fax = _pendingSend.Take(_cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Send thread terminated!");
                    return;
                }

                try
                {
                    Console.WriteLine($"Got a pending fax: {fax}");
                    if (fax.Id == null)
                    {
                        _client.SendFax(fax.Faxline.Id, fax.Recipient, fax.DocumentPath)
                            .ContinueWith(async faxId =>
                            {
                                fax.Id = await faxId;
                                fax.ChangeStatus(this, FaxStatus.Sending);
                                _scheduleCompletionCheck(fax);
                            });
                    }
                    else
                    {
                        _client.AttemptFaxResend(fax.Id, fax.Faxline.Id)
                            .ContinueWith(async success =>
                            {
                                if (await success)
                                {
                                    fax.ChangeStatus(this, FaxStatus.Sending);
                                    _scheduleCompletionCheck(fax);
                                }
                            });
                    }
                }
                catch (Exception e)
                {
                    fax.FailureCause = e;
                    fax.ChangeStatus(this, FaxStatus.Failed);
                }
            }
        }

        private void _scheduleCompletionCheck(TrackedFax fax)
        {
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                _pendingCompletion.Add(fax);
            });
        }

        private void _trackThemAll()
        {
            while (!_shutdown)
            {
                TrackedFax fax;
                try
                {
                    Console.WriteLine("Polling the completion queue...");
                    fax = _pendingCompletion.Take(_cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Tracking thread terminated!");
                    return;
                }

                Console.WriteLine($"Got a sending fax: {fax}");

                if (fax.Id == null)
                {
                    continue;
                }
                
                try
                {
                    var historyEntry = _client.GetHistoryEntry(fax.Id);
                    Console.WriteLine($"History: {historyEntry}");
                    switch(historyEntry.FaxStatus)
                    {
                        case FaxEntryStatus.Sent:
                            fax.ChangeStatus(this, FaxStatus.SuccessfullySent);
                            break;
                        case FaxEntryStatus.Failed:
                            fax.ChangeStatus(this, FaxStatus.Failed);
                            break;
                        case FaxEntryStatus.Sending:
                            fax.ChangeStatus(this, FaxStatus.Sending);
                            _scheduleCompletionCheck(fax);
                            break;
                        default:
                            fax.ChangeStatus(this, FaxStatus.Unknown);
                            break;
                    }
                }
                catch (Exception e)
                {
                    fax.FailureCause = e;
                    fax.ChangeStatus(this, FaxStatus.Unknown);
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
        public FaxStatus Status { get; private set; }
        public Exception? FailureCause { get; protected internal set; }

        public delegate void StatusChangedHandler(FaxScheduler sender, FaxStatus newStatus);
        public event StatusChangedHandler? StatusChanged;

        private readonly TaskCompletionSource<object?> _completed;

        public TrackedFax(Faxline faxline, string recipient, string documentPath)
        {
            Faxline = faxline;
            Recipient = recipient;
            DocumentPath = documentPath;
            Id = null;
            Status = FaxStatus.Pending;
            _completed = new TaskCompletionSource<object?>();
        }

        protected internal void ChangeStatus(FaxScheduler scheduler, FaxStatus newStatus)
        {
            if (newStatus != Status)
            {
                Status = newStatus;
                if (newStatus.IsComplete())
                {
                    _completed.SetResult(null);
                }
                StatusChanged?.Invoke(scheduler, newStatus);
            }
        }

        public Task AwaitCompletion()
        {
            return _completed.Task;
        }

        public override string ToString()
        {
            return $"{nameof(Faxline)}: {Faxline}, {nameof(Recipient)}: {Recipient}, {nameof(DocumentPath)}: {DocumentPath}, {nameof(Id)}: {Id}, {nameof(Status)}: {Status}, {nameof(FailureCause)}: {FailureCause}";
        }
    }

    public enum FaxStatus
    {
        Pending,
        Sending,
        SuccessfullySent,
        Failed,
        Unknown
    }

    public static class FaxStatusExtensions
    {
        public static bool IsComplete(this FaxStatus status)
        {
            switch (status)
            {
                case FaxStatus.Failed:
                case FaxStatus.SuccessfullySent:
                case FaxStatus.Unknown:
                    return true;
                default:
                    return false;
            }
        }
        public static bool CanResend(this FaxStatus status)
        {
            switch (status)
            {
                case FaxStatus.Failed:
                case FaxStatus.Unknown:
                    return true;
                default:
                    return false;
            }
        }
    }
}