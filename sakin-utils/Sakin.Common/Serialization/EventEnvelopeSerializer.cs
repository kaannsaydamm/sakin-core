using System.Text.Json;
using System.Text.Json.Serialization;
using Sakin.Common.Models;

namespace Sakin.Common.Serialization
{
    public static class EventEnvelopeSerializer
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public static string Serialize(EventEnvelope envelope)
        {
            return JsonSerializer.Serialize(envelope, _options);
        }

        public static EventEnvelope Deserialize(string json)
        {
            return JsonSerializer.Deserialize<EventEnvelope>(json, _options) 
                ?? throw new JsonException("Failed to deserialize EventEnvelope");
        }

        public static T DeserializeNormalized<T>(string json) where T : NormalizedEvent
        {
            return JsonSerializer.Deserialize<T>(json, _options) 
                ?? throw new JsonException($"Failed to deserialize {typeof(T).Name}");
        }

        public static string SerializeNormalized(NormalizedEvent normalizedEvent)
        {
            return JsonSerializer.Serialize(normalizedEvent, _options);
        }

        public static bool TryDeserialize(string json, out EventEnvelope? envelope)
        {
            try
            {
                envelope = JsonSerializer.Deserialize<EventEnvelope>(json, _options);
                return envelope != null;
            }
            catch
            {
                envelope = null;
                return false;
            }
        }

        public static bool TryDeserializeNormalized<T>(string json, out T? normalizedEvent) where T : NormalizedEvent
        {
            try
            {
                normalizedEvent = JsonSerializer.Deserialize<T>(json, _options);
                return normalizedEvent != null;
            }
            catch
            {
                normalizedEvent = null;
                return false;
            }
        }
    }
}