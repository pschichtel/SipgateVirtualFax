using System;
using System.Net;

namespace SipgateVirtualFax.Core.Sipgate
{
    public class TrackedFax
    {
        public Faxline Faxline { get; }
        public string Recipient { get; }
        public string DocumentPath { get; }
        public FaxStatus Status { get; private set; }
        public Exception? FailureCause { get; protected internal set; }
        public DateTime ScheduleTime { get; } = DateTime.Now;
        public DateTime? SendTime { get; private set; }
        public DateTime? CompleteTime { get; private set; }

        public delegate void StatusChangedHandler(TrackedFax sender, FaxStatus newStatus);

        public event StatusChangedHandler? StatusChanged;

        protected internal string? Id { get; set; }
        private readonly Func<TrackedFax, TrackedFax> _resendCallback;

        public TrackedFax(Faxline faxline, string recipient, string documentPath,
            Func<TrackedFax, TrackedFax> resendCallback)
        {
            _resendCallback = resendCallback;
            Faxline = faxline;
            Recipient = recipient;
            DocumentPath = documentPath;
            Status = FaxStatus.Pending;

            Id = null;
        }

        public void ChangeStatus(FaxStatus newStatus)
        {
            if (newStatus != Status)
            {
                var oldStatus = Status;
                Status = newStatus;

                if (newStatus == FaxStatus.Pending)
                {
                    SendTime = null;
                    CompleteTime = null;
                }

                if (oldStatus == FaxStatus.Pending && newStatus == FaxStatus.Sending)
                {
                    SendTime = DateTime.Now;
                }

                if (newStatus.IsComplete())
                {
                    CompleteTime = DateTime.Now;
                }

                StatusChanged?.Invoke(this, newStatus);
            }
        }

        public TrackedFax Resend()
        {
            return _resendCallback(this);
        }

        public bool MayResend
        {
            get
            {
                if (!Status.IsComplete())
                {
                    return false;
                }

                if (!Status.CanResend())
                {
                    return false;
                }
                if (FailureCause != null && FailureCause is SipgateApiHttpException e &&
                    e.Status == HttpStatusCode.ProxyAuthenticationRequired)
                {
                    return false;
                }
                return true;
            }
        }

        public override string ToString()
        {
            return
                $"{nameof(Faxline)}: {Faxline}, {nameof(Recipient)}: {Recipient}, {nameof(DocumentPath)}: {DocumentPath}, {nameof(Id)}: {Id}, {nameof(Status)}: {Status}, {nameof(FailureCause)}: {FailureCause}";
        }
    }
}