using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CredentialManagement;
using Newtonsoft.Json;

namespace SipgateVirtualFax.Core.Sipgate
{
    public class FaxlinesResponse
    {
        [JsonProperty("items")]
        public IEnumerable<Faxline>? Items { get; set; }
    }

    public class Faxline
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("alias")]
        public string? Alias { get; set; }

        [JsonProperty("groupId")]
        public string? GroupId { get; set; }

        public override string ToString()
        {
            return $"Id: {Id}, Alias: {Alias}, GroupId: {GroupId}";
        }
    }

    public class SendFaxRequest
    {
        [JsonProperty("faxlineId")]
        public string? FaxlineId { get; set; }

        [JsonProperty("recipient")]
        public string? Recipient { get; set; }

        [JsonProperty("filename")]
        public string? Filename { get; set; }

        [JsonProperty("base64Content")]
        public string? Content { get; set; }
    }

    public class SendFaxResponse
    {
        [JsonProperty("sessionId")]
        public string? SessionId { get; set; }
    }

    public class ResendFaxRequest
    {
        [JsonProperty("faxId")]
        public string? FaxId { get; set; }

        [JsonProperty("faxlineId")]
        public string? FaxlineId { get; set; }
    }

    public class HistoryEntry
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("source")]
        public string? Source { get; set; }

        [JsonProperty("target")]
        public string? Target { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }
    }
}