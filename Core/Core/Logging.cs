using System.Diagnostics;

namespace SipgateVirtualFax.Core
{
    public static class Logging
    {
        public static EventLog CreateEventLog(string? component = null)
        {
            string name = "sipgate-virtual-fax";
            if (component != null)
            {
                name = $"{name}-{component}";
            }
            return new EventLog("Application")
            {
                Source = name
            };
        }

        public static void Info(this EventLog log, string msg)
        {
            log.WriteEntry(msg, EventLogEntryType.Information);
        }

        public static void Warning(this EventLog log, string msg)
        {
            log.WriteEntry(msg, EventLogEntryType.Warning);
        }

        public static void Error(this EventLog log, string msg)
        {
            log.WriteEntry(msg, EventLogEntryType.Error);
        }

        public static void SuccessAudit(this EventLog log, string msg)
        {
            log.WriteEntry(msg, EventLogEntryType.SuccessAudit);
        }

        public static void FailureAudit(this EventLog log, string msg)
        {
            log.WriteEntry(msg, EventLogEntryType.FailureAudit);
        }
    }
}