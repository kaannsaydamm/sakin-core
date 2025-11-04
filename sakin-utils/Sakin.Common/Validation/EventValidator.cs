using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Sakin.Common.Models;

namespace Sakin.Common.Validation
{
    public class EventValidator
    {
        private readonly JsonSchema _schema;
        private readonly JsonSerializerOptions _serializerOptions;

        public EventValidator(string schemaJson)
        {
            _schema = JsonSchema.FromText(schemaJson);
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
        }

        public static EventValidator FromFile(string schemaFilePath)
        {
            var schemaJson = File.ReadAllText(schemaFilePath);
            return new EventValidator(schemaJson);
        }

        public ValidationResult Validate(NormalizedEvent evt)
        {
            var json = JsonSerializer.Serialize(evt, evt.GetType(), _serializerOptions);
            var jsonNode = JsonNode.Parse(json);
            
            var result = _schema.Evaluate(jsonNode);

            var errors = new List<string>();
            if (!result.IsValid)
            {
                CollectErrors(result, errors);
            }

            return new ValidationResult
            {
                IsValid = result.IsValid,
                Errors = errors
            };
        }

        public ValidationResult ValidateJson(string json)
        {
            var jsonNode = JsonNode.Parse(json);
            
            var result = _schema.Evaluate(jsonNode);

            var errors = new List<string>();
            if (!result.IsValid)
            {
                CollectErrors(result, errors);
            }

            return new ValidationResult
            {
                IsValid = result.IsValid,
                Errors = errors
            };
        }

        public string Serialize(NormalizedEvent evt)
        {
            return JsonSerializer.Serialize(evt, evt.GetType(), _serializerOptions);
        }

        public T? Deserialize<T>(string json) where T : NormalizedEvent
        {
            return JsonSerializer.Deserialize<T>(json, _serializerOptions);
        }

        public ValidationResult ValidateEnvelope(EventEnvelope envelope)
        {
            var json = JsonSerializer.Serialize(envelope, _serializerOptions);
            var jsonNode = JsonNode.Parse(json);
            
            var result = _schema.Evaluate(jsonNode);

            var errors = new List<string>();
            if (!result.IsValid)
            {
                CollectErrors(result, errors);
            }

            return new ValidationResult
            {
                IsValid = result.IsValid,
                Errors = errors
            };
        }

        public ValidationResult ValidateEnvelopeJson(string json)
        {
            var jsonNode = JsonNode.Parse(json);
            
            var result = _schema.Evaluate(jsonNode);

            var errors = new List<string>();
            if (!result.IsValid)
            {
                CollectErrors(result, errors);
            }

            return new ValidationResult
            {
                IsValid = result.IsValid,
                Errors = errors
            };
        }

        public string SerializeEnvelope(EventEnvelope envelope)
        {
            return JsonSerializer.Serialize(envelope, _serializerOptions);
        }

        public EventEnvelope? DeserializeEnvelope(string json)
        {
            return JsonSerializer.Deserialize<EventEnvelope>(json, _serializerOptions);
        }

        private void CollectErrors(EvaluationResults result, List<string> errors)
        {
            if (result.HasErrors)
            {
                var errorMessage = $"{result.InstanceLocation}: Validation failed";
                if (result.Errors != null)
                {
                    foreach (var error in result.Errors)
                    {
                        errors.Add($"{result.InstanceLocation}: {error.Key} - {error.Value}");
                    }
                }
                else
                {
                    errors.Add(errorMessage);
                }
            }

            if (result.Details != null)
            {
                foreach (var detail in result.Details)
                {
                    CollectErrors(detail, errors);
                }
            }
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
