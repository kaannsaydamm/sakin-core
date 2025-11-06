using System.Text.Json.Serialization;

namespace Sakin.Common.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ThreatIntelIndicatorType
    {
        Ipv4,
        Ipv6,
        Domain,
        Url,
        FileHash
    }
}
