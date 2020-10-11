using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using static SipgateVirtualFax.Core.Sipgate.HistoryEntry;

namespace SipgateVirtualFax.Core.Sipgate
{
    public class FaxScheduler : IDisposable
    {
        private readonly SipgateFaxClient _client;
        private readonly BlockingCollection<TrackedFax> _trackingQueue;

        private volatile bool _shutdown;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly Logger _logger = Logging.GetLogger("fax-scheduler");

        public FaxScheduler(SipgateFaxClient client)
        {
            _client = client;
            _trackingQueue = new BlockingCollection<TrackedFax>(new ConcurrentQueue<TrackedFax>());

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

        private void _scheduleCompletionCheck(TrackedFax fax)
        {
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                _trackingQueue.Add(fax);
            });
        }

        private void _trackThemAll()
        {
            while (!_shutdown)
            {
                TrackedFax fax;
                try
                {
                    fax = _trackingQueue.Take(_cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                switch (fax.Status)
                {
                    case FaxStatus.Pending:
                        _sendFax(fax);
                        break;
                    case FaxStatus.Sending:
                        _pollFaxStatus(fax);
                        break;
                }
            }
        }

        private async void _sendFax(TrackedFax fax)
        {
            try
            {
                if (fax.Id != null)
                {
                    var success = await _client.AttemptFaxResend(fax.Id, fax.Faxline.Id);
                    if (!success)
                    {
                        throw new Exception("Unable to resend the fax!");
                    }
                }
                else
                {
                    fax.Id = await _client.SendFax(fax.Faxline.Id, fax.Recipient, fax.DocumentPath);
                }

                fax.ChangeStatus(this, FaxStatus.Sending);
                _scheduleCompletionCheck(fax);
            }
            catch (Exception e)
            {
                _logger.Error(e,
                    fax.Id == null
                        ? $"Failed to resend the fax with id {fax.Id}"
                        : $"Failed to send a fax to {fax.Recipient}");
                fax.FailureCause = e;
                fax.ChangeStatus(this, FaxStatus.Failed);
            }
        }

        private async void _pollFaxStatus(TrackedFax fax)
        {
            try
            {
                if (fax.Id == null)
                {
                    return;
                }

                var historyEntry = await _client.GetHistoryEntry(fax.Id);
                switch (historyEntry.FaxStatus)
                {
                    case FaxEntryStatus.Sent:
                        fax.ChangeStatus(this, FaxStatus.SuccessfullySent);
                        break;
                    case FaxEntryStatus.Failed:
                        fax.ChangeStatus(this, FaxStatus.Failed);
                        break;
                    case FaxEntryStatus.Sending:
                    case FaxEntryStatus.Pending:
                        fax.ChangeStatus(this, FaxStatus.Sending);
                        _scheduleCompletionCheck(fax);
                        break;
                    default:
                        _logger.Warn(
                            $"Fax {fax.Id} (to {fax.Recipient}) is in an unexpected state, marking it as such");
                        fax.ChangeStatus(this, FaxStatus.Unknown);
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to poll the history of fax {fax.Id} (to {fax.Recipient})");
                fax.FailureCause = e;
                fax.ChangeStatus(this, FaxStatus.Unknown);
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

            if (!faxline.CanSend)
            {
                throw new Exception("Invalid faxline given: Faxline cannot send!");
            }

            var fax = new TrackedFax(faxline, recipient, documentPath, ResendFax);
            _trackingQueue.Add(fax);
            return fax;
        }

        public TrackedFax ResendFax(TrackedFax fax)
        {
            var newFax = new TrackedFax(fax.Faxline, fax.Recipient, fax.DocumentPath, ResendFax) {Id = fax.Id};
            _trackingQueue.Add(newFax);
            return newFax;
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