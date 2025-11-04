using System.Text.Json;
using System.Text.Json.Serialization;
using Sakin.Common.Models;

namespace Sakin.Common.Serialization
{
    /// <summary>
    /// Handles serialization and deserialization of event envelopes with versioning support
    /// </summary>
    public class EventEnvelopeSerializer
    {
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly Dictionary<string, JsonSerializerOptions> _versionedOptions;

        public EventEnvelopeSerializer()
        {
            _serializerOptions = CreateSerializerOptions();
            _versionedOptions = new Dictionary<string, JsonSerializerOptions>
            {
                { "1.0.0", CreateSerializerOptions() }
            };
        }

        /// <summary>
        /// Serializes an event envelope to JSON
        /// </summary>
        public string Serialize(EventEnvelope envelope)
        {
            var options = _versionedOptions.GetValueOrDefault(envelope.SchemaVersion, _serializerOptions);
            return JsonSerializer.Serialize(envelope, options);
        }

        /// <summary>
        /// Deserializes JSON to an event envelope
        /// </summary>
        public EventEnvelope? Deserialize(string json)
        {
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(json, _serializerOptions);
            
            // Handle version-specific deserialization if needed
            if (envelope != null)
            {
                var versionOptions = _versionedOptions.GetValueOrDefault(envelope.SchemaVersion, _serializerOptions);
                envelope = JsonSerializer.Deserialize<EventEnvelope>(json, versionOptions);
            }
            
            return envelope;
        }

        /// <summary>
        /// Deserializes JSON to an event envelope with a specific version
        /// </summary>
        public EventEnvelope? Deserialize(string json, string expectedVersion)
        {
            var versionOptions = _versionedOptions.GetValueOrDefault(expectedVersion, _serializerOptions);
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(json, versionOptions);
            
            if (envelope != null && envelope.SchemaVersion != expectedVersion)
            {
                throw new InvalidOperationException($"Schema version mismatch. Expected: {expectedVersion}, Actual: {envelope.SchemaVersion}");
            }
            
            return envelope;
        }

        /// <summary>
        /// Creates an event envelope from a normalized event
        /// </summary>
        public EventEnvelope CreateEnvelope(
            NormalizedEvent normalizedEvent, 
            string source, 
            SourceType sourceType, 
            object? raw = null,
            string schemaVersion = "1.0.0")
        {
            return new EventEnvelope
            {
                Source = source,
                SourceType = sourceType,
                Raw = raw,
                Normalized = normalizedEvent,
                SchemaVersion = schemaVersion,
                Enrichment = new Dictionary<string, object>(),
                Metadata = new Dictionary<string, object>()
            };
        }

        /// <summary>
        /// Validates if an envelope is compatible with a given schema version
        /// </summary>
        public bool IsVersionCompatible(string envelopeVersion, string requiredVersion)
        {
            // Simple semantic versioning compatibility check
            // Major version must match, minor version can be higher or equal
            var envelopeParts = envelopeVersion.Split('.').Select(int.Parse).ToArray();
            var requiredParts = requiredVersion.Split('.').Select(int.Parse).ToArray();

            if (envelopeParts.Length != 3 || requiredParts.Length != 3)
                return false;

            // Major version must match
            if (envelopeParts[0] != requiredParts[0])
                return false;

            // Minor version must be >= required
            if (envelopeParts[1] < requiredParts[1])
                return false;

            // Patch version doesn't matter for compatibility
            return true;
        }

        /// <summary>
        /// Gets the latest supported schema version
        /// </summary>
        public string GetLatestVersion()
        {
            return _versionedOptions.Keys.OrderByDescending(v => 
            {
                var parts = v.Split('.').Select(int.Parse).ToArray();
                return (parts[0] << 20) + (parts[1] << 10) + parts[2];
            }).First();
        }

        /// <summary>
        /// Registers a new schema version with custom serializer options
        /// </summary>
        public void RegisterVersion(string version, JsonSerializerOptions options)
        {
            _versionedOptions[version] = options;
        }

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
                Converters = { 
                    new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                    new NormalizedEventConverter()
                }
            };
        }
    }
}