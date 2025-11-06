using System.Text.Json.Serialization;

namespace Sakin.Common.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ThreatIntelHashType
    {
        Md5,
        Sha1,
        Sha256
    }
}
