using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sakin.Correlation.Configuration;

namespace Sakin.Correlation.Validation;

public interface IConfigurationValidator
{
    void ValidateConfiguration(IConfiguration configuration);
}

public class ConfigurationValidator : IConfigurationValidator
{
    private readonly ILogger<ConfigurationValidator> _logger;

    public ConfigurationValidator(ILogger<ConfigurationValidator> logger)
    {
        _logger = logger;
    }

    public void ValidateConfiguration(IConfiguration configuration)
    {
        var errors = new List<string>();

        // Validate Rules configuration
        var rulesSection = configuration.GetSection(RulesOptions.SectionName);
        if (!rulesSection.Exists())
        {
            errors.Add("Rules configuration section is missing");
        }
        else
        {
            var rulesOptions = rulesSection.Get<RulesOptions>();
            if (rulesOptions == null)
            {
                errors.Add("Failed to bind Rules configuration");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(rulesOptions.RulesPath))
                {
                    errors.Add("Rules.RulesPath is required");
                }

                if (rulesOptions.DebounceMilliseconds < 0)
                {
                    errors.Add("Rules.DebounceMilliseconds must be non-negative");
                }
                
                if (rulesOptions.DebounceMilliseconds > 10000)
                {
                    _logger.LogWarning("Rules.DebounceMilliseconds is unusually high: {Value}ms", 
                        rulesOptions.DebounceMilliseconds);
                }
            }
        }

        // Validate Redis configuration
        var redisSection = configuration.GetSection(RedisOptions.SectionName);
        if (!redisSection.Exists())
        {
            errors.Add("Redis configuration section is missing");
        }
        else
        {
            var redisOptions = redisSection.Get<RedisOptions>();
            if (redisOptions == null)
            {
                errors.Add("Failed to bind Redis configuration");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
                {
                    errors.Add("Redis.ConnectionString is required");
                }

                if (string.IsNullOrWhiteSpace(redisOptions.KeyPrefix))
                {
                    _logger.LogWarning("Redis.KeyPrefix is empty - this may cause key conflicts");
                }

                if (redisOptions.DefaultTTL <= 0)
                {
                    errors.Add("Redis.DefaultTTL must be positive");
                }

                if (redisOptions.DefaultTTL > 604800)
                {
                    _logger.LogWarning("Redis.DefaultTTL is unusually high: {Value} seconds (>7 days)", 
                        redisOptions.DefaultTTL);
                }
            }
        }

        // Validate Aggregation configuration
        var aggregationSection = configuration.GetSection(AggregationOptions.SectionName);
        if (!aggregationSection.Exists())
        {
            errors.Add("Aggregation configuration section is missing");
        }
        else
        {
            var aggregationOptions = aggregationSection.Get<AggregationOptions>();
            if (aggregationOptions == null)
            {
                errors.Add("Failed to bind Aggregation configuration");
            }
            else
            {
                if (aggregationOptions.MaxWindowSize <= 0)
                {
                    errors.Add("Aggregation.MaxWindowSize must be positive");
                }

                if (aggregationOptions.MaxWindowSize > 604800)
                {
                    _logger.LogWarning("Aggregation.MaxWindowSize is unusually high: {Value} seconds (>7 days)", 
                        aggregationOptions.MaxWindowSize);
                }

                if (aggregationOptions.CleanupInterval <= 0)
                {
                    errors.Add("Aggregation.CleanupInterval must be positive");
                }

                if (aggregationOptions.CleanupInterval < 60)
                {
                    _logger.LogWarning("Aggregation.CleanupInterval is very low: {Value} seconds (<1 minute)", 
                        aggregationOptions.CleanupInterval);
                }

                if (aggregationOptions.CleanupInterval > 3600)
                {
                    _logger.LogWarning("Aggregation.CleanupInterval is unusually high: {Value} seconds (>1 hour)", 
                        aggregationOptions.CleanupInterval);
                }
            }
        }

        // Validate Kafka configuration
        var kafkaSection = configuration.GetSection(KafkaWorkerOptions.SectionName);
        if (!kafkaSection.Exists())
        {
            errors.Add("Kafka configuration section is missing");
        }
        else
        {
            var kafkaOptions = kafkaSection.Get<KafkaWorkerOptions>();
            if (kafkaOptions == null)
            {
                errors.Add("Failed to bind Kafka configuration");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(kafkaOptions.BootstrapServers))
                {
                    errors.Add("Kafka.BootstrapServers is required");
                }

                if (string.IsNullOrWhiteSpace(kafkaOptions.Topic))
                {
                    errors.Add("Kafka.Topic is required");
                }

                if (string.IsNullOrWhiteSpace(kafkaOptions.ConsumerGroup))
                {
                    errors.Add("Kafka.ConsumerGroup is required");
                }
            }
        }

        if (errors.Count > 0)
        {
            var errorMessage = "Configuration validation failed:\n  " + string.Join("\n  ", errors);
            _logger.LogError("Configuration validation failed with {ErrorCount} error(s)", errors.Count);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformation("Configuration validation passed");
    }
}
