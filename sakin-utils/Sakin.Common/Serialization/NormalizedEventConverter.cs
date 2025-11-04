using System.Text.Json;
using System.Text.Json.Serialization;
using Sakin.Common.Models;

namespace Sakin.Common.Serialization
{
    /// <summary>
    /// Custom JSON converter for NormalizedEvent that handles type discrimination
    /// </summary>
    public class NormalizedEventConverter : JsonConverter<NormalizedEvent>
    {
        private const string TypeDiscriminator = "eventType";

        public override NormalizedEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var jsonDoc = JsonDocument.ParseValue(ref reader);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty(TypeDiscriminator, out var eventTypeElement))
            {
                return new NormalizedEvent(); // Default fallback
            }

            var eventTypeString = eventTypeElement.GetString();
            var eventType = Enum.Parse<EventType>(eventTypeString!, true);

            return eventType switch
            {
                EventType.NetworkTraffic => JsonSerializer.Deserialize<NetworkEvent>(root.GetRawText(), options),
                EventType.HttpRequest => JsonSerializer.Deserialize<NetworkEvent>(root.GetRawText(), options),
                EventType.TlsHandshake => JsonSerializer.Deserialize<NetworkEvent>(root.GetRawText(), options),
                EventType.SshConnection => JsonSerializer.Deserialize<NetworkEvent>(root.GetRawText(), options),
                EventType.DnsQuery => JsonSerializer.Deserialize<NormalizedEvent>(root.GetRawText(), options),
                EventType.SystemLog => JsonSerializer.Deserialize<NormalizedEvent>(root.GetRawText(), options),
                EventType.SecurityAlert => JsonSerializer.Deserialize<NetworkEvent>(root.GetRawText(), options),
                _ => JsonSerializer.Deserialize<NormalizedEvent>(root.GetRawText(), options)
            };
        }

        public override void Write(Utf8JsonWriter writer, NormalizedEvent value, JsonSerializerOptions options)
        {
            var actualType = value.GetType();
            
            if (actualType == typeof(NetworkEvent))
            {
                JsonSerializer.Serialize(writer, (NetworkEvent)value, options);
            }
            else
            {
                JsonSerializer.Serialize(writer, value, options);
            }
        }
    }
}