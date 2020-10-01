using System.Collections.Generic;
using Newtonsoft.Json;

namespace SipgateVirtualFax.Core.Sipgate
{
    public class FaxlinesResponse
    {
        [JsonProperty("items")]
        public Faxline[] Items { get; }

        [JsonConstructor]
        public FaxlinesResponse(Faxline[] items)
        {
            Items = items;
        }

        public override string ToString()
        {
            return $"{nameof(Items)}: {Items}";
        }
    }

    public class Faxline
    {
        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("alias")]
        public string Alias { get; }

        [JsonProperty("groupId")]
        public string GroupId { get; }

        [JsonProperty("canSend")]
        public bool CanSend { get; }

        [JsonConstructor]
        public Faxline(string id, string alias, string groupId, bool canSend)
        {
            Id = id;
            Alias = alias;
            GroupId = groupId;
            CanSend = canSend;
        }

        public override string ToString()
        {
            return $"{nameof(Id)}: {Id}, {nameof(Alias)}: {Alias}, {nameof(GroupId)}: {GroupId}";
        }
    }

    public class SendFaxRequest
    {
        [JsonProperty("faxlineId")]
        public string FaxlineId { get; }

        [JsonProperty("recipient")]
        public string Recipient { get; }

        [JsonProperty("filename")]
        public string Filename { get; }

        [JsonProperty("base64Content")]
        public string Content { get; }

        [JsonConstructor]
        public SendFaxRequest(string faxlineId, string recipient, string filename,
            [JsonProperty("base64Content")] string content)
        {
            FaxlineId = faxlineId;
            Recipient = recipient;
            Filename = filename;
            Content = content;
        }

        public override string ToString()
        {
            return $"{nameof(FaxlineId)}: {FaxlineId}, {nameof(Recipient)}: {Recipient}, {nameof(Filename)}: {Filename}, {nameof(Content)}: {Content}";
        }
    }

    public class SendFaxResponse
    {
        [JsonProperty("sessionId")]
        public string SessionId { get; }

        [JsonConstructor]
        public SendFaxResponse(string sessionId)
        {
            SessionId = sessionId;
        }

        public override string ToString()
        {
            return $"{nameof(SessionId)}: {SessionId}";
        }
    }

    public class ResendFaxRequest
    {
        [JsonProperty("faxId")]
        public string FaxId { get; }

        [JsonProperty("faxlineId")]
        public string FaxlineId { get; }

        [JsonConstructor]
        public ResendFaxRequest(string faxId, string faxlineId)
        {
            FaxId = faxId;
            FaxlineId = faxlineId;
        }

        public override string ToString()
        {
            return $"{nameof(FaxId)}: {FaxId}, {nameof(FaxlineId)}: {FaxlineId}";
        }
    }

    public class HistoryEntry
    {
        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("source")]
        public string Source { get; }

        [JsonProperty("target")]
        public string Target { get; }

        [JsonProperty("type")]
        public string Type { get; }

        [JsonProperty("status")]
        public string Status { get; }

        [JsonConstructor]
        public HistoryEntry(string id, string source, string target, string type, string status)
        {
            Id = id;
            Source = source;
            Target = target;
            Type = type;
            Status = status;
        }

        public override string ToString()
        {
            return $"{nameof(Id)}: {Id}, {nameof(Source)}: {Source}, {nameof(Target)}: {Target}, {nameof(Type)}: {Type}, {nameof(Status)}: {Status}";
        }
    }
}