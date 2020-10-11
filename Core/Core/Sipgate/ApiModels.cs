using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        public string? GroupId { get; }

        [JsonProperty("canSend")]
        public bool CanSend { get; }

        [JsonConstructor]
        public Faxline(string id, string alias, string? groupId, bool canSend)
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

        protected bool Equals(Faxline other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Faxline) obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
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
        public EntryType Type { get; }

        [JsonProperty("status")]
        [JsonConverter(typeof(EntryStatusEnumConverter))]
        public EntryStatus Status { get; }

        [JsonProperty("faxStatusType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public FaxEntryStatus? FaxStatus { get; }

        [JsonConstructor]
        public HistoryEntry(string id, string source, string target, EntryType type, EntryStatus status, [JsonProperty("faxStatusType")] FaxEntryStatus? faxStatus)
        {
            Id = id;
            Source = source;
            Target = target;
            Type = type;
            Status = status;
            FaxStatus = faxStatus;
        }

        public override string ToString()
        {
            return $"{nameof(Id)}: {Id}, {nameof(Source)}: {Source}, {nameof(Target)}: {Target}, {nameof(Type)}: {Type}, {nameof(Status)}: {Status}, {nameof(FaxStatus)}: {FaxStatus}";
        }

        public enum EntryType
        {
            Call,
            Voicemail,
            Sms,
            Fax
        }

        public class EntryTypeEnumConverter : JsonConverter<EntryType>
        {
            public override void WriteJson(JsonWriter writer, EntryType value, JsonSerializer serializer)
            {
                string strValue = value switch
                {
                    EntryType.Call => "CALL",
                    EntryType.Voicemail => "VOICEMAIL",
                    EntryType.Sms => "SMS",
                    EntryType.Fax => "FAX",
                    _ => throw new JsonWriterException($"Unknown enum value: {value}")
                };
                
                writer.WriteValue(strValue);
            }

            public override EntryType ReadJson(JsonReader reader, Type objectType, EntryType existingValue, bool hasExistingValue,
                JsonSerializer serializer)
            {
                var val = reader.Value;
                return val switch
                {
                    "CALL" => EntryType.Call,
                    "VOICEMAIL" => EntryType.Voicemail,
                    "SMS" => EntryType.Sms,
                    "FAX" => EntryType.Fax,
                    _ => throw new JsonReaderException($"Value was not a valid entry type: {val}")
                };
            }
        }

        public enum EntryStatus
        {
            Pickup,
            NoPickup,
            Busy,
            Forward
        }

        public class EntryStatusEnumConverter : JsonConverter<EntryStatus>
        {
            public override void WriteJson(JsonWriter writer, EntryStatus value, JsonSerializer serializer)
            {
                string strValue = value switch
                {
                    EntryStatus.Pickup => "PICKUP",
                    EntryStatus.NoPickup => "NOPICKUP",
                    EntryStatus.Busy => "BUSY",
                    EntryStatus.Forward => "FORWARD",
                    _ => throw new JsonWriterException($"Unknown enum value: {value}")
                };
                
                writer.WriteValue(strValue);
            }

            public override EntryStatus ReadJson(JsonReader reader, Type objectType, EntryStatus existingValue, bool hasExistingValue,
                JsonSerializer serializer)
            {
                var val = reader.Value;
                return val switch
                {
                    "PICKUP" => EntryStatus.Pickup,
                    "NOPICKUP" => EntryStatus.NoPickup,
                    "BUSY" => EntryStatus.Busy,
                    "FORWARD" => EntryStatus.Forward,
                    _ => throw new JsonReaderException($"Value was not a valid entry status: {val}")
                };
            }
        }

        public enum FaxEntryStatus
        {
            Sent,
            Failed,
            Sending,
            Pending
        }
        
        public class FaxEntryStatusEnumConverter : JsonConverter<FaxEntryStatus>
        {
            public override void WriteJson(JsonWriter writer, FaxEntryStatus value, JsonSerializer serializer)
            {
                string strValue = value switch
                {
                    FaxEntryStatus.Sent => "SENT",
                    FaxEntryStatus.Failed => "FAILED",
                    FaxEntryStatus.Sending => "SENDING",
                    FaxEntryStatus.Pending => "PENDING",
                    _ => throw new JsonWriterException($"Unknown enum value: {value}")
                };
                
                writer.WriteValue(strValue);
            }

            public override FaxEntryStatus ReadJson(JsonReader reader, Type objectType, FaxEntryStatus existingValue, bool hasExistingValue,
                JsonSerializer serializer)
            {
                var val = reader.Value;
                return val switch
                {
                    "SENT" => FaxEntryStatus.Sent,
                    "FAILED" => FaxEntryStatus.Failed,
                    "SENDING" => FaxEntryStatus.Sending,
                    "PENDING" => FaxEntryStatus.Pending,
                    _ => throw new JsonReaderException($"Value was not a valid fax entry status: {val}")
                };
            }
        }
    }

    public class UserInfoResponse
    {
        [JsonProperty("sub")]
        public String Id { get; }

        [JsonProperty("locale")]
        public String Locale { get; }

        public UserInfoResponse([JsonProperty("sub")] string id, string locale)
        {
            Id = id;
            Locale = locale;
        }
    }
    
    public class GroupMembersResponse
    {
        [JsonProperty("items")]
        public GroupMember[] Items { get; }

        public GroupMembersResponse(GroupMember[] items)
        {
            Items = items;
        }
    }

    public class GroupMember
    {
        [JsonProperty("id")]
        public string Id { get; }

        public GroupMember(string id)
        {
            Id = id;
        }
    }
}